using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

namespace Porto.Apple;

// Managed TLS avoids Schannel's requirement to persist an imported private key on this Windows build.
// Narrow protocol: TLS 1.2, ephemeral ECDHE and AES-GCM, paired certificate pinned exactly. No fallback.
internal static class AppleTls
{
    internal static Stream Protect(Stream stream,X509Certificate2 certificate,byte[] peerCertificate,CancellationToken cancel)
    {
        var protocol=new TlsClientProtocol();var client=new Client(certificate,peerCertificate);
        var encrypted=new AppleTlsStream(stream,protocol);
        try
        {
            protocol.Connect(client);
            return AppleWire.Command(cancel,token=>{encrypted.Handshake(()=>client.Complete,token).GetAwaiter().GetResult();return (Stream)encrypted;});
        }
        catch(TlsFatalAlert ex){encrypted.Dispose();throw client.Failure(ex.AlertDescription,false);}
        catch(TlsFatalAlertReceived ex){encrypted.Dispose();throw client.Failure(ex.AlertDescription,true);}
        catch{encrypted.Dispose();throw;}
    }
    internal static AsymmetricKeyParameter PrivateKey(X509Certificate2 certificate)
    {
        using var rsa=certificate.GetRSAPrivateKey()??throw new IOException("Tipo di chiave Apple non supportato.");
        byte[] bytes=rsa.ExportPkcs8PrivateKey();
        try{return PrivateKeyFactory.CreateKey(bytes);}finally{CryptographicOperations.ZeroMemory(bytes);}
    }
    internal static readonly SignatureAndHashAlgorithm Signature=new(Org.BouncyCastle.Tls.HashAlgorithm.sha256,SignatureAlgorithm.rsa);
    private static readonly SignatureAndHashAlgorithm[] StrongSignatures=
    [Signature, new(Org.BouncyCastle.Tls.HashAlgorithm.sha384,SignatureAlgorithm.rsa),
        new(Org.BouncyCastle.Tls.HashAlgorithm.sha512,SignatureAlgorithm.rsa),
        SignatureAndHashAlgorithm.rsa_pss_rsae_sha256,SignatureAndHashAlgorithm.rsa_pss_rsae_sha384,
        SignatureAndHashAlgorithm.rsa_pss_rsae_sha512];
    private sealed class Client(X509Certificate2 certificate,byte[] peerCertificate) : DefaultTlsClient(new ApplePairingCrypto(certificate.RawData,peerCertificate))
    {
        private readonly X509Certificate2 ownCertificate=certificate;
        private readonly byte[] expectedPeer=peerCertificate;
        internal bool Complete {get;private set;}
        private bool credentialsProvided;
        private string? failureCode;
        protected override ProtocolVersion[] GetSupportedVersions()=>[ProtocolVersion.TLSv12];
        protected override IList<SignatureAndHashAlgorithm> GetSupportedSignatureAlgorithms()=>StrongSignatures.ToList();
        protected override int[] GetSupportedCipherSuites()=>[CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256,CipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384];
        public override int GetMaxHandshakeMessageSize()=>65536;
        public override int GetHandshakeTimeoutMillis()=>30000;
        public override void NotifySecureRenegotiation(bool secureRenegotiation)
        {
            if(!secureRenegotiation)failureCode="RENEGOTIATION";
            base.NotifySecureRenegotiation(secureRenegotiation);
        }
        internal IOException Failure(short alert,bool received)
        {
            // Only our fixed categories and a protocol alert number. Never log certificates, keys or peer text.
            string code=received?$"PEER-{alert}":failureCode??$"LOCAL-{alert}";
            string detail=code switch
            {
                "PIN"=>"Il certificato ricevuto non coincide con quello dell'iPhone abbinato.",
                "CLIENT-AUTH"=>"Il collegamento non ha richiesto l'autenticazione del computer.",
                "CLIENT-TYPE"=>"Il tipo di autenticazione richiesto non è supportato.",
                "CLIENT-SIGNATURE"=>"Non è stata concordata una firma di autenticazione supportata (SHA-256 o superiore).",
                "RENEGOTIATION"=>"Il collegamento non offre l'estensione di sicurezza richiesta per TLS 1.2.",
                _ when received=>"L'iPhone ha rifiutato la negoziazione TLS.",
                _=>"Porto ha rifiutato la negoziazione TLS."
            };
            return new IOException($"Autenticazione USB Apple non riuscita [APPLE-TLS-{code}]. {detail} Conserva questo messaggio. Nessuna protezione è stata ridotta.");
        }
        public override void NotifyHandshakeComplete()
        {
            base.NotifyHandshakeComplete();
            if(!credentialsProvided){failureCode="CLIENT-AUTH";throw new TlsFatalAlert(AlertDescription.handshake_failure);}
            Complete=true;
        }
        public override TlsAuthentication GetAuthentication()=>new Authentication(this);
        private sealed class Authentication(Client owner) : TlsAuthentication
        {
            public void NotifyServerCertificate(TlsServerCertificate serverCertificate)
            {
                var chain=serverCertificate.Certificate;
                if(chain.IsEmpty||!CryptographicOperations.FixedTimeEquals(chain.GetCertificateAt(0).GetEncoded(),owner.expectedPeer))
                {owner.failureCode="PIN";throw new TlsFatalAlert(AlertDescription.bad_certificate);}
            }
            public TlsCredentials GetClientCredentials(Org.BouncyCastle.Tls.CertificateRequest request)
            {
                if(request.CertificateTypes==null||!request.CertificateTypes.Contains(ClientCertificateType.rsa_sign))
                {owner.failureCode="CLIENT-TYPE";throw new TlsFatalAlert(AlertDescription.handshake_failure);}
                var signature=StrongSignatures.FirstOrDefault(s=>request.SupportedSignatureAlgorithms?.Contains(s)==true);
                if(signature==null)
                {owner.failureCode="CLIENT-SIGNATURE";throw new TlsFatalAlert(AlertDescription.handshake_failure);}
                var crypto=(BcTlsCrypto)owner.Crypto;
                var signer=new BcDefaultTlsCredentialedSigner(new TlsCryptoParameters(owner.m_context),crypto,PrivateKey(owner.ownCertificate),
                    new Certificate([crypto.CreateCertificate(owner.ownCertificate.RawData)]),signature);
                owner.credentialsProvided=true;return signer;
            }
        }
    }
}

// Nonblocking TLS engine, explicit async transport: caller cancellation covers handshake and each command.
internal sealed class AppleTlsStream(Stream transport,TlsProtocol protocol) : Stream
{
    private readonly byte[] incoming=new byte[18432],outgoing=new byte[18432],plain=new byte[16384];
    private bool disposed;
    internal async Task Handshake(Func<bool> completed,CancellationToken cancel)
    {
        int received=0;
        while(!completed())
        {
            await Drain(cancel).ConfigureAwait(false);
            received+=await Pump(cancel).ConfigureAwait(false);
            if(protocol.IsClosed&&!completed())throw new IOException("Sessione TLS chiusa prima dell'autenticazione.");
            if(received>1024*1024)throw new IOException("Handshake Apple oltre il limite di dati.");
        }
        await Drain(cancel).ConfigureAwait(false);
    }
    private async Task<int> Pump(CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();int count=await transport.ReadAsync(incoming,cancel).ConfigureAwait(false);
        if(count==0){protocol.CloseInput();if(!protocol.IsClosed)throw new EndOfStreamException("Sessione TLS Apple interrotta.");return 0;}
        protocol.OfferInput(incoming,0,count);return count;
    }
    private async Task Drain(CancellationToken cancel)
    {
        while(protocol.GetAvailableOutputBytes()>0)
        {
            cancel.ThrowIfCancellationRequested();int count=protocol.ReadOutput(outgoing,0,outgoing.Length);
            await transport.WriteAsync(outgoing.AsMemory(0,count),cancel).ConfigureAwait(false);
        }
    }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken=default)
    {
        ObjectDisposedException.ThrowIf(disposed,this);cancellationToken.ThrowIfCancellationRequested();if(buffer.Length==0)return 0;
        while(protocol.GetAvailableInputBytes()==0)
        {
            await Drain(cancellationToken).ConfigureAwait(false);if(protocol.IsClosed)return 0;
            await Pump(cancellationToken).ConfigureAwait(false);
        }
        int count=protocol.ReadInput(plain,0,Math.Min(buffer.Length,plain.Length));plain.AsMemory(0,count).CopyTo(buffer);plain.AsSpan(0,count).Clear();return count;
    }
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken cancellationToken=default)
    {
        ObjectDisposedException.ThrowIf(disposed,this);cancellationToken.ThrowIfCancellationRequested();
        for(int offset=0;offset<buffer.Length;)
        {
            int count=Math.Min(plain.Length,buffer.Length-offset);buffer.Span.Slice(offset,count).CopyTo(plain);
            protocol.WriteApplicationData(plain,0,count);plain.AsSpan(0,count).Clear();offset+=count;
            await Drain(cancellationToken).ConfigureAwait(false);
        }
    }
    public override int Read(byte[] buffer,int offset,int count)=>ReadAsync(buffer.AsMemory(offset,count)).AsTask().GetAwaiter().GetResult();
    public override void Write(byte[] buffer,int offset,int count)=>WriteAsync(buffer.AsMemory(offset,count)).AsTask().GetAwaiter().GetResult();
    protected override void Dispose(bool disposing)
    {
        if(!disposed)
        {
            disposed=true;
            if(disposing){try{protocol.Close();}catch(IOException){}finally{transport.Dispose();}}
            CryptographicOperations.ZeroMemory(incoming);CryptographicOperations.ZeroMemory(outgoing);CryptographicOperations.ZeroMemory(plain);
        }
        base.Dispose(disposing);
    }
    public override bool CanRead=>!disposed;public override bool CanWrite=>!disposed;public override bool CanSeek=>false;
    public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
    public override void Flush(){}public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();
}
