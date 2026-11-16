using System.Buffers.Binary;
using System.Text;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;
using Porto.Apple;

internal static class AppleTransportTests
{
    internal static void Add(List<(string,Action)> tests)
    {
        tests.Add(("apple TLS uses ephemeral existing credentials and authenticates the pinned device in memory",()=>TlsTest(false)));
        tests.Add(("apple TLS rejects a different device certificate in memory",()=>TlsTest(true)));
        tests.Add(("apple TLS rejects pinned server that does not request client authentication",()=>TlsTest(false,false)));
        tests.Add(("apple TLS rejects SHA1 handshake signatures without reducing protection",()=>TlsTest(false,true,true)));
        tests.Add(("apple TLS pinned SHA1 device certificate still requires SHA256 handshake signatures",()=>
        {
            TlsTest(false,legacyCertificate:true);TlsTest(false,true,true,legacyCertificate:true);
        }));
        tests.Add(("apple TLS blocked handshake honors cancellation without credentials on disk",()=>
        {
            using var key=RSA.Create(2048);var request=new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=Synthetic",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
            using var certificate=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddHours(1));
            using var stream=new BlockedStream();using var cancel=new CancellationTokenSource(25);
            Fixtures.Throws<OperationCanceledException>(()=>AppleTls.Protect(stream,certificate,certificate.RawData,cancel.Token));
        }));
        tests.Add(("apple plist rejects duplicate keys external entities and malformed binary",()=>
        {
            foreach(string xml in new[]{"<plist><dict><key>x</key><integer>1</integer><key>x</key><integer>2</integer></dict></plist>",
                "<!DOCTYPE plist [<!ENTITY e SYSTEM 'file:///private'>]><plist><dict><key>x</key><string>&e;</string></dict></plist>"})
                Fixtures.Throws<IOException>(()=>ApplePlist.Decode(Encoding.UTF8.GetBytes(xml)));
            Fixtures.Throws<IOException>(()=>ApplePlist.Decode("bplist00"u8.ToArray()));
            var value=ApplePlist.Decode(ApplePlist.Encode(new(){{"Request","test"},{"Count",42UL},{"Enabled",true},{"Data",new byte[]{1,2}}}));
            Fixtures.Check((string)value["Request"]=="test"&&(ulong)value["Count"]==42&&(bool)value["Enabled"]);
        }));
        tests.Add(("apple binary plist shares repeated objects without materialization explosion",()=>
        {
            var decoded=ApplePlist.Decode(SharedBinary());var entries=(List<object>)decoded["x"];
            Fixtures.Check(entries.Count==512&&ReferenceEquals(entries[0],entries[511])&&((byte[])entries[0]).Length==1024*1024);
        }));
        tests.Add(("apple mux validates framing and is cancellable without any network",()=>
        {
            byte[] payload=ApplePlist.Encode(new(){{"MessageType","Result"},{"Number",0UL}});byte[] header=new byte[16];
            BinaryPrimitives.WriteUInt32LittleEndian(header,(uint)(payload.Length+16));BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4),1);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8),8);BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12),1);
            using var wire=new ScriptedStream([..header,..payload]);
            Fixtures.Check(ApplePlist.Integer(AppleUsbTransport.Mux(wire,new(){{"MessageType","ListDevices"}},default),"Number")==0);
            header[12]=2;using var invalid=new ScriptedStream([..header,..payload]);
            Fixtures.Throws<IOException>(()=>AppleUsbTransport.Mux(invalid,new(){{"MessageType","ListDevices"}},default));
            using var blocked=new BlockedStream();using var cancellation=new CancellationTokenSource(25);
            Fixtures.Throws<OperationCanceledException>(()=>AppleUsbTransport.Mux(blocked,new(){{"MessageType","ListDevices"}},cancellation.Token));
        }));
        tests.Add(("apple AFC only reads file with mode one and validates every packet",()=>
        {
            byte[] stat=Encoding.UTF8.GetBytes("st_size\0"+"3\0st_mtime\0"+"1\0st_ifmt\0S_IFREG\0");
            using var wire=new ScriptedStream([..Packet(1,2,stat),..Packet(2,14,U64(42)),..Packet(3,2,[1,2,3]),..Packet(4,2,[]),..Packet(5,1,U64(0))]);
            using var fs=new AppleAfcFileSystem(wire);
            Fixtures.Check(fs.Stat("/DCIM/100APPLE/IMG_1.MOV",default)==new AppleFileInfo(3,"1",true));
            using(var input=fs.OpenRead("/DCIM/100APPLE/IMG_1.MOV",default))
            {
                byte[] bytes=new byte[8];Fixtures.Check(input.Read(bytes,0,8)==3&&bytes[2]==3&&input.Read(bytes,0,8)==0);
                Fixtures.Throws<NotSupportedException>(()=>input.Write(bytes,0,1));
            }
            byte[] sent=wire.Sent.ToArray();int offset=0;var operations=new List<ulong>();
            while(offset<sent.Length)
            {
                ulong operation=BinaryPrimitives.ReadUInt64LittleEndian(sent.AsSpan(offset+32));operations.Add(operation);
                if(operation==13)Fixtures.Check(BinaryPrimitives.ReadUInt64LittleEndian(sent.AsSpan(offset+40))==1);
                offset+=checked((int)BinaryPrimitives.ReadUInt64LittleEndian(sent.AsSpan(offset+8)));
            }
            Fixtures.Check(operations.SequenceEqual(new ulong[]{10,13,15,15,20}));
        }));
        tests.Add(("apple AFC rejects forbidden paths malformed lengths and sequence drift",()=>
        {
            foreach(string path in new[]{"/PhotoData/private.sqlite","/DCIM/../private","/DCIM/a\\b","/private","","/DCIM/x:ads"})
                Fixtures.Throws<IOException>(()=>AppleAfcFileSystem.ValidatePath(path));
            using var wire=new ScriptedStream(Packet(2,1,U64(8)));using var fs=new AppleAfcFileSystem(wire);
            Fixtures.Throws<IOException>(()=>fs.Stat("/PhotoData/Photos.sqlite",default));
            Fixtures.Throws<IOException>(()=>fs.Stat("/PhotoData/Photos.sqlite",default));
        }));
        tests.Add(("apple AFC malformed UTF8 metadata is normalized and poisons the session",()=>
        {
            using var wire=new ScriptedStream(Packet(1,2,[0xff,0]));using var fs=new AppleAfcFileSystem(wire);
            Fixtures.Throws<IOException>(()=>fs.Stat("/PhotoData/Photos.sqlite",default));Fixtures.Check(!fs.IsUsable);
        }));
        tests.Add(("apple AFC malformed status poisons stat open and read before another command",()=>
        {
            foreach(int operation in new[]{10,13,15})
            foreach(int length in new[]{0,7,9})
            {
                byte[] malformed=Packet(operation==15?2UL:1UL,1,new byte[length]);
                using var wire=new ScriptedStream(operation==15?[..Packet(1,14,U64(42)),..malformed]:malformed);
                using var fs=new AppleAfcFileSystem(wire);
                if(operation==10)Fixtures.Throws<IOException>(()=>fs.Stat("/PhotoData/Photos.sqlite",default));
                else if(operation==13)Fixtures.Throws<IOException>(()=>fs.OpenRead("/DCIM/100APPLE/IMG_1.MOV",default));
                else
                {
                    using var input=fs.OpenRead("/DCIM/100APPLE/IMG_1.MOV",default);
                    Fixtures.Throws<IOException>(()=>input.ReadByte());
                    Fixtures.Check(!fs.IsUsable);
                }
                Fixtures.Check(!fs.IsUsable);
                long sent=wire.Sent.Length;
                Fixtures.Throws<IOException>(()=>fs.Stat("/PhotoData/Photos.sqlite",default));
                Fixtures.Check(wire.Sent.Length==sent);
            }
        }));
        tests.Add(("apple AFC valid per-file errors preserve the session and exact packet boundary",()=>
        {
            byte[] stat=Encoding.UTF8.GetBytes("st_size\0"+"3\0st_mtime\0"+"1\0st_ifmt\0S_IFREG\0");
            using var wire=new ScriptedStream([..Packet(1,1,U64(8)),..Packet(2,1,U64(10)),..Packet(3,2,stat)]);
            using var fs=new AppleAfcFileSystem(wire);
            Fixtures.Check(fs.Stat("/DCIM/100APPLE/IMG_1.MOV",default)==null&&fs.IsUsable);
            Fixtures.Throws<IOException>(()=>fs.Stat("/DCIM/100APPLE/IMG_2.MOV",default));
            Fixtures.Check(fs.IsUsable&&fs.Stat("/DCIM/100APPLE/IMG_3.MOV",default)==new AppleFileInfo(3,"1",true));
        }));
        tests.Add(("apple WAL verifies both checksum endiannesses and applies committed pages only",()=>
        {
            byte[] db=AppleGalleryTests.Database();
            foreach(bool big in new[]{false,true})
            {
                byte[] wal=Wal(db,big);var image=AppleSqliteImage.ApplyWal((byte[])db.Clone(),wal,default);
                Fixtures.Check(BinaryPrimitives.ReadUInt32BigEndian(image.AsSpan(60))==123);
                Fixtures.Check(AppleCatalogDecoder.Decode(image,default).VisibleVideoCount==1);
                wal[wal.Length-1]^=1;
                Fixtures.Throws<IOException>(()=>AppleSqliteImage.ApplyWal((byte[])db.Clone(),wal,default));
            }
        }));
        tests.Add(("apple WAL rejects short header short frame and altered salt header",()=>
        {
            byte[] db=AppleGalleryTests.Database(),wal=Wal(db,false);
            Fixtures.Throws<IOException>(()=>AppleSqliteImage.ApplyWal((byte[])db.Clone(),wal[..20],default));
            Fixtures.Throws<IOException>(()=>AppleSqliteImage.ApplyWal((byte[])db.Clone(),wal[..^1],default));
            wal[16]^=1;Fixtures.Throws<IOException>(()=>AppleSqliteImage.ApplyWal((byte[])db.Clone(),wal,default));
        }));
    }
    private static void TlsTest(bool wrongPeer,bool requestClient=true,bool weakSignature=false,bool legacyCertificate=false)
    {
        using var rootKey=RSA.Create(2048);using var deviceKey=RSA.Create(2048);
        var rootRequest=new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=Synthetic Apple Root",rootKey,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));
        using var generatedRoot=rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddHours(1));
        using var root=X509Certificate2.CreateFromPem(generatedRoot.ExportCertificatePem(),rootKey.ExportPkcs8PrivateKeyPem());
        var deviceRequest=new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=Synthetic Device",deviceKey,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        deviceRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false,false,0,true));
        deviceRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature|X509KeyUsageFlags.KeyEncipherment,true));
        using var generatedDevice=deviceRequest.Create(root,DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddMinutes(30),[1]);
        byte[] deviceDer=generatedDevice.RawData;
        if(legacyCertificate)
        {
            var generator=new Org.BouncyCastle.X509.X509V3CertificateGenerator(new Org.BouncyCastle.X509.X509CertificateParser().ReadCertificate(deviceDer));
            deviceDer=generator.Generate(new Org.BouncyCastle.Crypto.Operators.Asn1SignatureFactory("SHA1WITHRSA",AppleTls.PrivateKey(root))).GetEncoded();
        }
        using var device=X509Certificate2.CreateFromPem(PemEncoding.WriteString("CERTIFICATE",deviceDer),deviceKey.ExportPkcs8PrivateKeyPem());
        Fixtures.Check(device.SignatureAlgorithm.Value==(legacyCertificate?"1.2.840.113549.1.1.5":"1.2.840.113549.1.1.11"));
        var first=Channel.CreateBounded<byte[]>(16);var second=Channel.CreateBounded<byte[]>(16);
        using var clientStream=new DuplexStream(first.Reader,second.Writer);using var serverStream=new DuplexStream(second.Reader,first.Writer);
        var serverProtocol=new TlsServerProtocol();var serverConfig=new SyntheticTlsServer(device,root.RawData,requestClient,weakSignature);
        using var server=new AppleTlsStream(serverStream,serverProtocol);serverProtocol.Accept(serverConfig);
        using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var task=server.Handshake(()=>serverConfig.Complete,cancel.Token);
        try
        {
            if(wrongPeer||!requestClient||weakSignature)
            {
                Fixtures.Throws<IOException>(()=>AppleTls.Protect(clientStream,root,wrongPeer?root.RawData:device.RawData,cancel.Token));
            }
            else
            {
                using var client=AppleTls.Protect(clientStream,root,device.RawData,cancel.Token);task.GetAwaiter().GetResult();
                client.Write([1,2,3]);byte[] bytes=new byte[3];server.ReadExactly(bytes);Fixtures.Check(bytes.SequenceEqual(new byte[]{1,2,3}));
                using(var stopRead=new CancellationTokenSource(25))
                    Fixtures.Throws<OperationCanceledException>(()=>client.ReadAsync(new byte[1],stopRead.Token).AsTask().GetAwaiter().GetResult());
                using(var stopWrite=new CancellationTokenSource(25))
                    Fixtures.Throws<OperationCanceledException>(()=>client.WriteAsync(new byte[1024*1024],stopWrite.Token).AsTask().GetAwaiter().GetResult());
            }
        }
        finally
        {
            cancel.Cancel();try{task.GetAwaiter().GetResult();}catch(Exception ex) when(wrongPeer||!requestClient||weakSignature||ex is OperationCanceledException){}
        }
    }
    private sealed class SyntheticTlsServer(X509Certificate2 certificate,byte[] expectedClient,bool requestClient,bool weakSignature) : DefaultTlsServer(new BcTlsCrypto())
    {
        internal bool Complete {get;private set;}
        protected override ProtocolVersion[] GetSupportedVersions()=>[ProtocolVersion.TLSv12];
        protected override int[] GetSupportedCipherSuites()=>[CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256];
        protected override TlsCredentialedSigner GetRsaSignerCredentials()=>new BcDefaultTlsCredentialedSigner(new TlsCryptoParameters(m_context),(BcTlsCrypto)Crypto,
            AppleTls.PrivateKey(certificate),new Certificate([Crypto.CreateCertificate(certificate.RawData)]),weakSignature?new(Org.BouncyCastle.Tls.HashAlgorithm.sha1,SignatureAlgorithm.rsa):AppleTls.Signature);
        public override Org.BouncyCastle.Tls.CertificateRequest GetCertificateRequest()=>requestClient?new([ClientCertificateType.rsa_sign],new List<SignatureAndHashAlgorithm>{AppleTls.Signature},null):null!;
        public override void NotifyClientCertificate(Certificate clientCertificate)
        {if(clientCertificate.IsEmpty||!clientCertificate.GetCertificateAt(0).GetEncoded().SequenceEqual(expectedClient))throw new TlsFatalAlert(AlertDescription.bad_certificate);}
        public override void NotifyHandshakeComplete(){base.NotifyHandshakeComplete();Complete=true;}
    }
    private static byte[] SharedBinary()
    {
        using var stream=new MemoryStream();stream.Write("bplist00"u8);var offsets=new List<int>();
        offsets.Add((int)stream.Length);stream.Write([0x51,(byte)'x']);
        offsets.Add((int)stream.Length);stream.Write([0x4f,0x12,0,0x10,0,0]);stream.Write(new byte[1024*1024]);
        offsets.Add((int)stream.Length);stream.Write([0xaf,0x11,2,0]);stream.Write(Enumerable.Repeat((byte)1,512).ToArray());
        offsets.Add((int)stream.Length);stream.Write([0xd1,0,2]);int table=(int)stream.Length;
        foreach(int offset in offsets){byte[] item=new byte[4];BinaryPrimitives.WriteInt32BigEndian(item,offset);stream.Write(item);}
        byte[] trailer=new byte[32];trailer[6]=4;trailer[7]=1;BinaryPrimitives.WriteUInt64BigEndian(trailer.AsSpan(8),4);
        BinaryPrimitives.WriteUInt64BigEndian(trailer.AsSpan(16),3);BinaryPrimitives.WriteUInt64BigEndian(trailer.AsSpan(24),(ulong)table);stream.Write(trailer);return stream.ToArray();
    }
    private static byte[] U64(ulong value){byte[] bytes=new byte[8];BinaryPrimitives.WriteUInt64LittleEndian(bytes,value);return bytes;}
    private static byte[] Packet(ulong sequence,ulong operation,byte[] body)
    {
        byte[] bytes=new byte[40+body.Length];"CFA6LPAA"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(8),(ulong)bytes.Length);BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(16),40);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(24),sequence);BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(32),operation);body.CopyTo(bytes,40);return bytes;
    }
    private static byte[] Wal(byte[] database,bool big)
    {
        int page=AppleSqliteImage.ValidateDatabase(database);byte[] bytes=new byte[32+2*(page+24)];
        void Put(int offset,uint value)=>BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset),value);
        Put(0,big?0x377f0683U:0x377f0682U);Put(4,3007000);Put(8,(uint)page);Put(16,123);Put(20,456);
        uint a=0,b=0;
        void Sum(int offset,int length)
        {
            unchecked{for(int i=offset;i<offset+length;i+=8){a+=(big?BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(i)):BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i)))+b;b+=(big?BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(i+4)):BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i+4)))+a;}}
        }
        Sum(0,24);Put(24,a);Put(28,b);
        for(int frame=0;frame<2;frame++)
        {
            int offset=32+frame*(page+24);Put(offset,1);Put(offset+4,frame==0?(uint)(database.Length/page):0);Put(offset+8,123);Put(offset+12,456);
            database.AsSpan(0,page).CopyTo(bytes.AsSpan(offset+24));Put(offset+24+60,(uint)(123+frame));
            Sum(offset,8);Sum(offset+24,page);Put(offset+16,a);Put(offset+20,b);
        }
        return bytes;
    }
    private sealed class ScriptedStream(byte[] reply) : Stream
    {
        private readonly MemoryStream response=new(reply,false);
        internal MemoryStream Sent {get;}=new();
        public override int Read(byte[] buffer,int offset,int count)=>response.Read(buffer,offset,Math.Min(count,7));
        public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken=default)
        {cancellationToken.ThrowIfCancellationRequested();return ValueTask.FromResult(response.Read(buffer.Span[..Math.Min(buffer.Length,7)]));}
        public override void Write(byte[] buffer,int offset,int count)=>Sent.Write(buffer,offset,count);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken cancellationToken=default)
        {cancellationToken.ThrowIfCancellationRequested();Sent.Write(buffer.Span);return ValueTask.CompletedTask;}
        public override bool CanRead=>true;public override bool CanWrite=>true;public override bool CanSeek=>false;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush(){}public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();
    }
    private sealed class BlockedStream : Stream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken=default){await Task.Delay(Timeout.Infinite,cancellationToken);return 0;}
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken cancellationToken=default)=>ValueTask.CompletedTask;
        public override int Read(byte[] buffer,int offset,int count)=>throw new InvalidOperationException();public override void Write(byte[] buffer,int offset,int count){}
        public override bool CanRead=>true;public override bool CanWrite=>true;public override bool CanSeek=>false;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush(){}public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();
    }
    private sealed class DuplexStream(ChannelReader<byte[]> reader,ChannelWriter<byte[]> writer) : Stream
    {
        private byte[]? current;private int offset;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken=default)
        {
            if(buffer.Length==0)return 0;
            if(current==null||offset==current.Length){try{current=await reader.ReadAsync(cancellationToken);offset=0;}catch(ChannelClosedException){return 0;}}
            int length=Math.Min(buffer.Length,current.Length-offset);current.AsMemory(offset,length).CopyTo(buffer);offset+=length;return length;
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
