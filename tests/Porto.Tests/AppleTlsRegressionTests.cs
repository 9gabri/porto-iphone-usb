using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;
using Porto.Apple;

internal static class AppleTlsRegressionTests
{
    private static readonly SignatureAndHashAlgorithm Sha256=new(Org.BouncyCastle.Tls.HashAlgorithm.sha256,SignatureAlgorithm.rsa);
    private static readonly SignatureAndHashAlgorithm Sha384=new(Org.BouncyCastle.Tls.HashAlgorithm.sha384,SignatureAlgorithm.rsa);
    private static readonly SignatureAndHashAlgorithm Sha512=new(Org.BouncyCastle.Tls.HashAlgorithm.sha512,SignatureAlgorithm.rsa);
    private static readonly SignatureAndHashAlgorithm Sha1=new(Org.BouncyCastle.Tls.HashAlgorithm.sha1,SignatureAlgorithm.rsa);
    private static readonly SignatureAndHashAlgorithm Pss256=SignatureAndHashAlgorithm.rsa_pss_rsae_sha256;
    internal static void Add(List<(string,Action)> tests)
    {
        tests.Add(("apple TLS matrix accepts fragmented SHA256 with root in device certificate chain",()=>Run(Sha256,Sha256)));
        tests.Add(("apple TLS matrix accepts exactly pinned SHA1 leaf plus root with SHA256 handshake",()=>Run(Sha256,Sha256,legacyCertificate:true)));
        tests.Add(("apple TLS matrix accepts SHA384 server and client handshake signatures",()=>Run(Sha384,Sha384)));
        tests.Add(("apple TLS matrix accepts client certificate request with SHA384 only",()=>Run(Sha256,Sha384)));
        tests.Add(("apple TLS matrix accepts RSA PSS SHA256 server handshake signature",()=>Run(Pss256,Sha256)));
        tests.Add(("apple TLS matrix accepts RSA PSS SHA256 client certificate request",()=>Run(Sha256,Pss256)));
        tests.Add(("apple TLS matrix accepts SHA512 server and client handshake signatures",()=>Run(Sha512,Sha512)));
        tests.Add(("apple TLS matrix accepts RSA PSS SHA384 server and client signatures",()=>Run(SignatureAndHashAlgorithm.rsa_pss_rsae_sha384,SignatureAndHashAlgorithm.rsa_pss_rsae_sha384)));
        tests.Add(("apple TLS matrix accepts RSA PSS SHA512 server and client signatures",()=>Run(SignatureAndHashAlgorithm.rsa_pss_rsae_sha512,SignatureAndHashAlgorithm.rsa_pss_rsae_sha512)));
        tests.Add(("apple TLS matrix rejects wrong pinned device despite strong signatures",()=>Run(Sha384,Sha384,wrongPeer:true,expectedCode:"APPLE-TLS-PIN")));
        tests.Add(("apple TLS matrix rejects a different certificate with the same device key",()=>Run(Sha256,Sha256,sameKeyWrongPin:true,expectedCode:"APPLE-TLS-PIN")));
        tests.Add(("apple TLS matrix rejects SHA1-only client certificate request",()=>Run(Sha256,Sha1,reject:true,expectedCode:"APPLE-TLS-CLIENT-SIGNATURE")));
        tests.Add(("apple TLS matrix rejects SHA1 server signature with a two-certificate chain",()=>Run(Sha1,Sha256,reject:true,expectedCode:"APPLE-TLS-LOCAL-")));
        tests.Add(("apple TLS matrix rejects server omitting mutual authentication",()=>Run(Sha384,Sha384,requestClient:false,expectedCode:"APPLE-TLS-CLIENT-AUTH")));
        tests.Add(("apple TLS matrix reports peer fatal alert with only a fixed diagnostic code",()=>
        {
            using var key=RSA.Create(2048);
            var request=new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=Secret synthetic subject must not appear",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
            using var certificate=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddHours(1));
            using var wire=new ReceivedAlertWire();
            IOException? failure=null;
            try{using var unexpected=AppleTls.Protect(wire,certificate,certificate.RawData,default);}
            catch(IOException exception){failure=exception;}
            Fixtures.Check(failure!=null&&failure.Message.Contains("APPLE-TLS-PEER-40",StringComparison.Ordinal));
            Fixtures.Check(failure!.InnerException==null&&!failure.Message.Contains("Secret",StringComparison.Ordinal)&&
                !failure.Message.Contains("CERTIFICATE",StringComparison.Ordinal)&&!failure.Message.Contains("PRIVATE KEY",StringComparison.Ordinal));
        }));
    }
    private static void Run(SignatureAndHashAlgorithm serverSignature,SignatureAndHashAlgorithm requestedClient,
        bool wrongPeer=false,bool requestClient=true,bool reject=false,bool legacyCertificate=false,bool sameKeyWrongPin=false,string? expectedCode=null)
    {
        using var rootKey=RSA.Create(2048);using var deviceKey=RSA.Create(2048);
        var rootRequest=new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=Synthetic Matrix Root",rootKey,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));
        using var root=rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddHours(1));
        var deviceRequest=new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=Synthetic Matrix Device",deviceKey,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        deviceRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false,false,0,true));
        deviceRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature|X509KeyUsageFlags.KeyEncipherment,true));
        using var generated=deviceRequest.Create(root,DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddMinutes(30),[1]);
        using var otherDevice=deviceRequest.Create(root,DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddMinutes(30),[2]);
        byte[] deviceDer=generated.RawData;
        if(legacyCertificate)
        {
            var generator=new Org.BouncyCastle.X509.X509V3CertificateGenerator(new Org.BouncyCastle.X509.X509CertificateParser().ReadCertificate(deviceDer));
            deviceDer=generator.Generate(new Org.BouncyCastle.Crypto.Operators.Asn1SignatureFactory("SHA1WITHRSA",AppleTls.PrivateKey(root))).GetEncoded();
        }
        using var device=X509Certificate2.CreateFromPem(PemEncoding.WriteString("CERTIFICATE",deviceDer),deviceKey.ExportPkcs8PrivateKeyPem());
        var first=Channel.CreateBounded<byte[]>(32);var second=Channel.CreateBounded<byte[]>(32);
        using var clientWire=new FragmentedWire(first.Reader,second.Writer);
        using var serverWire=new FragmentedWire(second.Reader,first.Writer);
        var serverProtocol=new TlsServerProtocol();
        var config=new MatrixServer(device,root.RawData,serverSignature,requestedClient,requestClient);
        using var server=new AppleTlsStream(serverWire,serverProtocol);
        serverProtocol.Accept(config);
        using var cancellation=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var handshake=server.Handshake(()=>config.Complete,cancellation.Token);
        bool expectedFailure=wrongPeer||sameKeyWrongPin||!requestClient||reject;
        try
        {
            if(expectedFailure)
            {
                IOException? failure=null;
                try{using var unexpected=AppleTls.Protect(clientWire,root,wrongPeer?root.RawData:sameKeyWrongPin?otherDevice.RawData:device.RawData,cancellation.Token);}
                catch(IOException exception){failure=exception;}
                Fixtures.Check(failure!=null,"Expected TLS rejection");
                Fixtures.Check(expectedCode==null||failure!.Message.Contains(expectedCode,StringComparison.Ordinal),failure!.Message);
                Fixtures.Check(failure.InnerException==null&&!failure.Message.Contains("Synthetic",StringComparison.Ordinal)&&
                    !failure.Message.Contains("CERTIFICATE",StringComparison.Ordinal)&&!failure.Message.Contains("PRIVATE KEY",StringComparison.Ordinal),"Diagnostic exposed certificate material");
            }
            else
            {
                using var client=AppleTls.Protect(clientWire,root,device.RawData,cancellation.Token);
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
    private sealed class MatrixServer(X509Certificate2 device,byte[] root,SignatureAndHashAlgorithm signature,
        SignatureAndHashAlgorithm clientSignature,bool requestClient):DefaultTlsServer(new BcTlsCrypto())
    {
        internal bool Complete {get;private set;}
        internal bool ClientObserved {get;private set;}
        protected override ProtocolVersion[] GetSupportedVersions()=>[ProtocolVersion.TLSv12];
        protected override int[] GetSupportedCipherSuites()=>[CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256];
        protected override TlsCredentialedSigner GetRsaSignerCredentials()=>new BcDefaultTlsCredentialedSigner(new TlsCryptoParameters(m_context),(BcTlsCrypto)Crypto,
            AppleTls.PrivateKey(device),new Certificate([Crypto.CreateCertificate(device.RawData),Crypto.CreateCertificate(root)]),signature);
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
    private sealed class ReceivedAlertWire:Stream
    {
        private readonly MemoryStream reply=new([21,3,3,0,2,2,40],false);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken=default)=>reply.ReadAsync(buffer,cancellationToken);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken cancellationToken=default)=>ValueTask.CompletedTask;
        public override int Read(byte[] buffer,int offset,int count)=>reply.Read(buffer,offset,count);
        public override void Write(byte[] buffer,int offset,int count){}
        protected override void Dispose(bool disposing){if(disposing)reply.Dispose();base.Dispose(disposing);}
        public override bool CanRead=>true;public override bool CanWrite=>true;public override bool CanSeek=>false;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush(){}public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();
    }
}
