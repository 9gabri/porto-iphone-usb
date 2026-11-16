using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;
using Porto.Apple;

internal static class ApplePairingTlsTests
{
    private static readonly SignatureAndHashAlgorithm Sha256=new(Org.BouncyCastle.Tls.HashAlgorithm.sha256,SignatureAlgorithm.rsa);
    private static readonly SignatureAndHashAlgorithm Sha1=new(Org.BouncyCastle.Tls.HashAlgorithm.sha1,SignatureAlgorithm.rsa);
    internal static void Add(List<(string,Action)> tests)
    {
        tests.Add(("apple pairing TLS factory preserves DER metadata copies and default parser policy",CheckFactoryIsolation));
        tests.Add(("apple pairing TLS factory rejects duplicate extensions in exact paired certificate",CheckDuplicateExtensions));
        tests.Add(("apple pairing TLS accepts empty names and serial zero",()=>Run(serial:0)));
        tests.Add(("apple pairing TLS accepts empty names and serial one",()=>Run(serial:1)));
        tests.Add(("apple pairing TLS accepts empty names serial zero SHA1 certificate SHA256 handshake",()=>Run(serial:0,legacyCertificate:true)));
        tests.Add(("apple pairing TLS accepts absent RSA algorithm parameters",()=>Run(serial:0,omitParameters:true)));
        tests.Add(("apple pairing TLS rejects wrong pinned certificate with empty names",()=>Run(serial:0,wrongPeer:true)));
        tests.Add(("apple pairing TLS rejects different paired certificate with identical key",()=>Run(serial:0,sameKeyWrongPin:true)));
        tests.Add(("apple pairing TLS rejects device certificate without digitalSignature usage",()=>Run(serial:0,signingUsage:false)));
        tests.Add(("apple pairing TLS rejects SHA1 server handshake",()=>Run(serial:0,weakServer:true)));
        tests.Add(("apple pairing TLS rejects SHA1 client authentication request",()=>Run(serial:0,weakClient:true,expectedCode:"APPLE-TLS-CLIENT-SIGNATURE")));
        tests.Add(("apple pairing TLS rejects missing mutual authentication",()=>Run(serial:0,requestClient:false,expectedCode:"APPLE-TLS-CLIENT-AUTH")));
        tests.Add(("apple pairing TLS rejects exact-pinned malformed certificate",()=>Run(serial:0,malformed:true)));
    }
    private static void CheckFactoryIsolation()
    {
        using var key=RSA.Create(2048);
        byte[] original=MakeCertificate(key,key,7,false,false,false);
        var crypto=new ApplePairingCrypto(original,original);
        var adapted=crypto.CreateCertificate(CertificateType.X509,original);
        Fixtures.Check(adapted.GetEncoded().SequenceEqual(original));
        Fixtures.Check(adapted.SerialNumber.IntValue==7&&adapted.SigAlgOid==Org.BouncyCastle.Asn1.Pkcs.PkcsObjectIdentifiers.Sha256WithRsaEncryption.Id);
        byte[] copy=adapted.GetEncoded();copy[0]^=1;
        Fixtures.Check(adapted.GetEncoded().SequenceEqual(original));
        var oid=Org.BouncyCastle.Asn1.X509.X509Extensions.KeyUsage;
        byte[] usage=adapted.GetExtension(oid),expected=usage.ToArray();usage[0]^=1;
        Fixtures.Check(adapted.GetExtension(oid).SequenceEqual(expected));
        using var loaded=X509CertificateLoader.LoadCertificate(original);
        Fixtures.Check(loaded.Extensions[oid.Id]!.RawData.SequenceEqual(expected));
        Fixtures.Throws<TlsFatalAlert>(()=>new BcTlsCrypto().CreateCertificate(original));
    }
    private static void CheckDuplicateExtensions()
    {
        using var key=RSA.Create(2048);
        byte[] original=MakeCertificate(key,key,0,false,false,false);
        var outer=Org.BouncyCastle.Asn1.Asn1Sequence.GetInstance(original);
        var tbs=Org.BouncyCastle.Asn1.Asn1Sequence.GetInstance(outer[0]);
        var extensions=Org.BouncyCastle.Asn1.Asn1Sequence.GetInstance(Org.BouncyCastle.Asn1.Asn1TaggedObject.GetInstance(tbs[7]).GetExplicitBaseObject());
        var duplicated=new Org.BouncyCastle.Asn1.Asn1EncodableVector();
        foreach(var item in extensions)duplicated.Add(item);
        duplicated.Add(extensions[0]);
        var fields=new Org.BouncyCastle.Asn1.Asn1EncodableVector();
        for(int i=0;i<7;i++)fields.Add(tbs[i]);
        fields.Add(new Org.BouncyCastle.Asn1.DerTaggedObject(true,3,new Org.BouncyCastle.Asn1.DerSequence(duplicated)));
        var malformedTbs=new Org.BouncyCastle.Asn1.DerSequence(fields);
        byte[] signature=key.SignData(malformedTbs.GetEncoded(),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        byte[] malformed=new Org.BouncyCastle.Asn1.DerSequence(malformedTbs,outer[1],new Org.BouncyCastle.Asn1.DerBitString(signature)).GetEncoded();
        var crypto=new ApplePairingCrypto(malformed,malformed);
        Fixtures.Throws<TlsFatalAlert>(()=>crypto.CreateCertificate(CertificateType.X509,malformed));
    }
    private static void Run(int serial,bool legacyCertificate=false,bool omitParameters=false,bool wrongPeer=false,bool sameKeyWrongPin=false,
        bool signingUsage=true,bool weakServer=false,bool weakClient=false,bool requestClient=true,bool malformed=false,string? expectedCode=null)
    {
        using var rootKey=RSA.Create(2048);using var deviceKey=RSA.Create(2048);
        byte[] rootDer=MakeCertificate(rootKey,rootKey,serial,true,legacyCertificate,omitParameters);
        byte[] deviceDer=MakeCertificate(deviceKey,rootKey,serial,false,legacyCertificate,omitParameters,signingUsage);
        using var root=X509Certificate2.CreateFromPem(PemEncoding.WriteString("CERTIFICATE",rootDer),rootKey.ExportPkcs8PrivateKeyPem());
        using var device=X509Certificate2.CreateFromPem(PemEncoding.WriteString("CERTIFICATE",deviceDer),deviceKey.ExportPkcs8PrivateKeyPem());
        byte[] expectedPeer=wrongPeer?rootDer:sameKeyWrongPin?MakeCertificate(deviceKey,rootKey,serial+1,false,legacyCertificate,omitParameters):malformed?[..deviceDer,0]:deviceDer;
        var first=Channel.CreateBounded<byte[]>(32);var second=Channel.CreateBounded<byte[]>(32);
        using var clientWire=new FragmentedWire(first.Reader,second.Writer);
        using var serverWire=new FragmentedWire(second.Reader,first.Writer);
        var serverProtocol=new TlsServerProtocol();
        var config=new MatrixServer(device,root.RawData,weakServer?Sha1:Sha256,weakClient?Sha1:Sha256,requestClient,malformed);
        using var server=new AppleTlsStream(serverWire,serverProtocol);
        serverProtocol.Accept(config);
        using var cancellation=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var handshake=server.Handshake(()=>config.Complete,cancellation.Token);
        bool expectedFailure=wrongPeer||sameKeyWrongPin||!signingUsage||weakServer||weakClient||!requestClient||malformed;
        try
        {
            if(expectedFailure)
            {
                IOException? failure=null;
                try{using var unexpected=AppleTls.Protect(clientWire,root,expectedPeer,cancellation.Token);}
                catch(IOException exception){failure=exception;}
                Fixtures.Check(failure!=null,"Expected TLS rejection");
                Fixtures.Check(expectedCode==null||failure!.Message.Contains(expectedCode,StringComparison.Ordinal),failure!.Message);
                Fixtures.Check(failure.InnerException==null&&!failure.Message.Contains("Synthetic",StringComparison.Ordinal)&&
                    !failure.Message.Contains("CERTIFICATE",StringComparison.Ordinal)&&!failure.Message.Contains("PRIVATE KEY",StringComparison.Ordinal),"Diagnostic exposed certificate material");
            }
            else
            {
                using var client=AppleTls.Protect(clientWire,root,expectedPeer,cancellation.Token);
                handshake.GetAwaiter().GetResult();
                Fixtures.Check(config.ClientObserved,"Server did not verify client identity");
                byte[] sent=Enumerable.Range(0,1000).Select(i=>(byte)i).ToArray(),received=new byte[1000];
                client.Write(sent);server.ReadExactly(received);Fixtures.Check(sent.SequenceEqual(received));
                server.Write([6,5,4]);byte[] response=new byte[3];client.ReadExactly(response);
                Fixtures.Check(response.SequenceEqual(new byte[]{6,5,4}));
            }
        }
        finally
        {
            cancellation.Cancel();
            try{handshake.GetAwaiter().GetResult();}
            catch(Exception exception) when(expectedFailure||exception is OperationCanceledException or IOException){}
        }
    }
    // Synthetic DER follows the public pairing layouts in libimobiledevice userpref.c
    // and pymobiledevice3 ca.py; no real pairing record or certificate is read.
    private static byte[] MakeCertificate(RSA key,RSA signer,int serial,bool root,bool legacy,bool omitParameters,bool signingUsage=true)
    {
        var signature=new Org.BouncyCastle.Asn1.X509.AlgorithmIdentifier(legacy?Org.BouncyCastle.Asn1.Pkcs.PkcsObjectIdentifiers.Sha1WithRsaEncryption:
            Org.BouncyCastle.Asn1.Pkcs.PkcsObjectIdentifiers.Sha256WithRsaEncryption,Org.BouncyCastle.Asn1.DerNull.Instance);
        var spki=Org.BouncyCastle.X509.SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(Org.BouncyCastle.Security.DotNetUtilities.GetRsaPublicKey(key));
        if(omitParameters)spki=new(new Org.BouncyCastle.Asn1.X509.AlgorithmIdentifier(Org.BouncyCastle.Asn1.Pkcs.PkcsObjectIdentifiers.RsaEncryption),spki.ParsePublicKey());
        var extensions=new Org.BouncyCastle.Asn1.X509.X509ExtensionsGenerator();
        extensions.AddExtension(Org.BouncyCastle.Asn1.X509.X509Extensions.BasicConstraints,true,new Org.BouncyCastle.Asn1.X509.BasicConstraints(root));
        if(!root)extensions.AddExtension(Org.BouncyCastle.Asn1.X509.X509Extensions.KeyUsage,true,new Org.BouncyCastle.Asn1.X509.KeyUsage(
            (signingUsage?Org.BouncyCastle.Asn1.X509.KeyUsage.DigitalSignature:0)|Org.BouncyCastle.Asn1.X509.KeyUsage.KeyEncipherment));
        var tbs=new Org.BouncyCastle.Asn1.DerSequence(new Org.BouncyCastle.Asn1.DerTaggedObject(true,0,Org.BouncyCastle.Asn1.DerInteger.ValueOf(2)),
            Org.BouncyCastle.Asn1.DerInteger.ValueOf(serial),signature,new Org.BouncyCastle.Asn1.DerSequence(),
            new Org.BouncyCastle.Asn1.DerSequence(new Org.BouncyCastle.Asn1.DerUtcTime(DateTime.UtcNow.AddMinutes(-1),2049),new Org.BouncyCastle.Asn1.DerUtcTime(DateTime.UtcNow.AddYears(10),2049)),
            new Org.BouncyCastle.Asn1.DerSequence(),spki,new Org.BouncyCastle.Asn1.DerTaggedObject(true,3,extensions.Generate()));
        byte[] signed=signer.SignData(tbs.GetEncoded(),legacy?HashAlgorithmName.SHA1:HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        return new Org.BouncyCastle.Asn1.DerSequence(tbs,signature,new Org.BouncyCastle.Asn1.DerBitString(signed)).GetEncoded();
    }
    // The test server supplies independently encoded certificates without routing through
    // production parsing. Its only job is to preserve the synthetic bytes on the wire.
    private sealed class PairingServerCrypto:BcTlsCrypto
    {
        public override TlsCertificate CreateCertificate(byte[] encoding)=>new RawServerCertificate(this,encoding);
        public override TlsCertificate CreateCertificate(short type,byte[] encoding)=>new RawServerCertificate(this,encoding);
    }
    private sealed class RawServerCertificate:BcTlsRawKeyCertificate
    {
        private readonly byte[] der;internal bool Malformed;private readonly Org.BouncyCastle.Asn1.Asn1Sequence cert,tbs;
        internal RawServerCertificate(BcTlsCrypto crypto,byte[] der):base(crypto,
            Org.BouncyCastle.Asn1.X509.SubjectPublicKeyInfo.GetInstance(Org.BouncyCastle.Asn1.Asn1Sequence.GetInstance(
                Org.BouncyCastle.Asn1.Asn1Sequence.GetInstance(der)[0])[6]))
        {this.der=der;cert=Org.BouncyCastle.Asn1.Asn1Sequence.GetInstance(der);tbs=Org.BouncyCastle.Asn1.Asn1Sequence.GetInstance(cert[0]);}
        public override byte[] GetEncoded()=>Malformed?[..der,0]:der.ToArray();
        public override string SigAlgOid=>Org.BouncyCastle.Asn1.X509.AlgorithmIdentifier.GetInstance(cert[1]).Algorithm.Id;
        public override Org.BouncyCastle.Asn1.Asn1Encodable GetSigAlgParams()=>Org.BouncyCastle.Asn1.X509.AlgorithmIdentifier.GetInstance(cert[1]).Parameters;
        public override Org.BouncyCastle.Math.BigInteger SerialNumber=>Org.BouncyCastle.Asn1.DerInteger.GetInstance(tbs[1]).Value;
        public override byte[] GetExtension(Org.BouncyCastle.Asn1.DerObjectIdentifier oid)=>
            Org.BouncyCastle.Asn1.X509.X509Extensions.GetInstance(Org.BouncyCastle.Asn1.Asn1TaggedObject.GetInstance(tbs[7]),true).GetExtension(oid)?.Value.GetOctets()!;
    }
    private sealed class MatrixServer(X509Certificate2 device,byte[] root,SignatureAndHashAlgorithm signature,
        SignatureAndHashAlgorithm clientSignature,bool requestClient,bool malformed):DefaultTlsServer(new PairingServerCrypto())
    {
        internal bool Complete {get;private set;}
        internal bool ClientObserved {get;private set;}
        protected override ProtocolVersion[] GetSupportedVersions()=>[ProtocolVersion.TLSv12];
        protected override int[] GetSupportedCipherSuites()=>[CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256];
        protected override TlsCredentialedSigner GetRsaSignerCredentials()
        {
            var leaf=new RawServerCertificate((BcTlsCrypto)Crypto,device.RawData){Malformed=malformed};
            return new BcDefaultTlsCredentialedSigner(new TlsCryptoParameters(m_context),(BcTlsCrypto)Crypto,
                AppleTls.PrivateKey(device),new Certificate([leaf,Crypto.CreateCertificate(root)]),signature);
        }
        public override Org.BouncyCastle.Tls.CertificateRequest GetCertificateRequest()=>requestClient
            ?new([ClientCertificateType.rsa_sign],new List<SignatureAndHashAlgorithm>{clientSignature},null):null!;
        public override void NotifyClientCertificate(Certificate certificate)
        {
            if(certificate.IsEmpty||!certificate.GetCertificateAt(0).GetEncoded().SequenceEqual(root))throw new TlsFatalAlert(AlertDescription.bad_certificate);
            ClientObserved=true;
        }
        public override void NotifyHandshakeComplete(){base.NotifyHandshakeComplete();Complete=true;}
    }
    private sealed class FragmentedWire(ChannelReader<byte[]> reader,ChannelWriter<byte[]> writer):Stream
    {
        private byte[]? pending;private int offset;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken=default)
        {
            if(buffer.Length==0)return 0;
            if(pending==null||offset==pending.Length)
            {try{pending=await reader.ReadAsync(cancellationToken);offset=0;}catch(ChannelClosedException){return 0;}}
            int count=Math.Min(7,Math.Min(buffer.Length,pending.Length-offset));
            pending.AsMemory(offset,count).CopyTo(buffer);offset+=count;return count;
        }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken cancellationToken=default)=>writer.WriteAsync(buffer.ToArray(),cancellationToken);
        public override int Read(byte[] buffer,int offset,int count)=>ReadAsync(buffer.AsMemory(offset,count)).AsTask().GetAwaiter().GetResult();
        public override void Write(byte[] buffer,int offset,int count)=>WriteAsync(buffer.AsMemory(offset,count)).AsTask().GetAwaiter().GetResult();
        protected override void Dispose(bool disposing){writer.TryComplete();base.Dispose(disposing);}
        public override bool CanRead=>true;public override bool CanWrite=>true;public override bool CanSeek=>false;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush(){}public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();
    }
}
