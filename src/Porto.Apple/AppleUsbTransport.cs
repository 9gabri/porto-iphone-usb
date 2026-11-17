using System.Buffers.Binary;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Porto.Core;

namespace Porto.Apple;

public static class AppleGalleryDiscovery
{
    // Explicit user actions invoke discovery; no startup/background probe or service installation exists.
    public static AppleDiscoveryResult Discover(CancellationToken cancel)
    {
        try
        {
            var devices=AppleUsbTransport.ListDevices(cancel).Select(d=>new AppleDevice(d.Serial,$"iPhone USB · {d.Serial[^6..]}")).ToArray();
            return new(devices,devices.Length==0?"Nessun iPhone USB esposto dal servizio Apple già installato.":null);
        }
        catch(OperationCanceledException){throw;}
        catch(IOException){return new([],"Servizio USB Apple non disponibile o non compatibile. Porto non installa né avvia servizi Windows. Usa l'installazione Apple già disponibile e riprova.");}
        catch(SocketException){return new([],"Servizio USB Apple non disponibile. Porto non installa né avvia servizi Windows.");}
    }
    public static AppleGalleryCatalog ListAlbums(AppleDevice device,CancellationToken cancel,bool compatibleUsb=false)
    {
        using var fileSystem=AppleUsbTransport.Connect(device,cancel,compatibleUsb);
        return AppleCatalogCapture.Read(fileSystem,cancel);
    }
}

internal static class AppleUsbTransport
{
    internal sealed record MuxDevice(uint Handle,string Serial);
    internal static IReadOnlyList<MuxDevice> ListDevices(CancellationToken cancel)
    {
        using var socket=OpenLoopback(cancel);
        var response=Mux(socket,new(){["MessageType"]="ListDevices"},cancel);
        if(!response.TryGetValue("DeviceList",out var value)||value is not List<object> list||list.Count>128)throw ApplePlist.Bad();
        var devices=new List<MuxDevice>();var serials=new HashSet<string>(StringComparer.Ordinal);var handles=new HashSet<uint>();
        foreach(var item in list)
        {
            var entry=ApplePlist.Dictionary(item);
            if(!entry.TryGetValue("Properties",out var rawProperties))throw ApplePlist.Bad();
            var properties=ApplePlist.Dictionary(rawProperties);
            if(ApplePlist.Text(properties,"ConnectionType")!="USB")continue;
            string serial=ApplePlist.Text(properties,"SerialNumber");
            ulong handle=ApplePlist.Integer(entry,"DeviceID");
            if(!ValidSerial(serial)||handle==0||handle>uint.MaxValue||!serials.Add(serial)||!handles.Add((uint)handle))throw ApplePlist.Bad();
            devices.Add(new((uint)handle,serial));
        }
        return devices;
    }
    internal static IAppleFileSystem Connect(AppleDevice device,CancellationToken cancel,bool compatibleUsb=false,SourceTransportMetrics? metrics=null)
    {
        if(metrics!=null)metrics.ConnectionAttempts++;
        if(!ValidSerial(device.Id))throw new IOException("Identificativo iPhone non valido.");
        IReadOnlyList<MuxDevice> devices;
        try{devices=ListDevices(cancel);}
        catch(SocketException){throw new IOException("Il servizio USB Apple non è raggiungibile.");}
        var matches=devices.Where(d=>d.Serial==device.Id).ToArray();
        if(matches.Length!=1)throw new IOException("L'iPhone selezionato non è collegato via USB; nessun altro telefono verrà selezionato.");
        PairIdentity? identity=null;
        Stream? lockdown=null,afc=null;
        try
        {
            identity=PairIdentity.Read(device.Id,cancel);
            lockdown=ConnectPort(matches[0].Handle,62078,cancel);
            var session=Lockdown(lockdown,new(){["Request"]="StartSession",["HostID"]=identity.HostId,["SystemBUID"]=identity.SystemBuid},cancel);
            if(!session.TryGetValue("EnableSessionSSL",out var enable)||enable is not true)throw new IOException("Sessione Apple senza protezione TLS: collegamento rifiutato.");
            lockdown=identity.Protect(lockdown,cancel);
            var service=Lockdown(lockdown,new(){["Request"]="StartService",["Service"]="com.apple.afc"},cancel);
            ulong port=ApplePlist.Integer(service,"Port");if(port is 0 or >65535)throw ApplePlist.Bad();
            afc=ConnectPort(matches[0].Handle,(ushort)port,cancel);
            if(service.TryGetValue("EnableServiceSSL",out var serviceSsl))
            {
                if(serviceSsl is not bool)throw ApplePlist.Bad();
                if(serviceSsl is true)afc=identity.Protect(afc,cancel);
            }
            var result=new AppleAfcFileSystem(afc,lockdown,identity,metrics){ReadLimit=compatibleUsb?64*1024:256*1024};afc=null;lockdown=null;identity=null;return result;
        }
        catch(AuthenticationException){throw new IOException("Il collegamento Apple non soddisfa le impostazioni TLS di Windows o il certificato abbinato. Nessuna protezione è stata ridotta.");}
        catch(SocketException){throw new IOException("Il collegamento USB Apple si è interrotto.");}
        catch(CryptographicException){throw new IOException("Le credenziali dell'abbinamento Apple non sono utilizzabili.");}
        finally{afc?.Dispose();lockdown?.Dispose();identity?.Dispose();}
    }
    private static bool ValidSerial(string value)=>value.Length is >=8 and <=128&&value.All(c=>char.IsAsciiLetterOrDigit(c)||c=='-');
    private static Stream OpenLoopback(CancellationToken cancel)
    {
        var socket=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp){NoDelay=true};
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancel);deadline.CancelAfter(TimeSpan.FromSeconds(5));
        try{socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback,27015),deadline.Token).AsTask().GetAwaiter().GetResult();return new NetworkStream(socket,true);}
        catch(OperationCanceledException) when(!cancel.IsCancellationRequested){socket.Dispose();throw new IOException("Il servizio USB Apple non risponde.");}
        catch{socket.Dispose();throw;}
    }
    private static Stream ConnectPort(uint device,ushort port,CancellationToken cancel)
    {
        var stream=OpenLoopback(cancel);
        try
        {
            var response=Mux(stream,new(){["MessageType"]="Connect",["DeviceID"]=device,["PortNumber"]=(ulong)BinaryPrimitives.ReverseEndianness(port)},cancel);
            if(ApplePlist.Integer(response,"Number")!=0)throw new IOException("Il servizio richiesto sull'iPhone non è disponibile.");
            return stream;
        }
        catch{stream.Dispose();throw;}
    }
    internal static Dictionary<string,object> Mux(Stream stream,Dictionary<string,object> request,CancellationToken cancel)
    {
        request["ClientVersionString"]="Porto";request["ProgName"]="Porto";request["kLibUSBMuxVersion"]=3UL;
        byte[] payload=ApplePlist.Encode(request);byte[] header=new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(header,(uint)(payload.Length+16));BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4),1);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8),8);BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12),1);
        return AppleWire.Command(cancel,token=>
        {
            AppleWire.Write(stream,header,token);AppleWire.Write(stream,payload,token);AppleWire.Read(stream,header,token);
            uint length=BinaryPrimitives.ReadUInt32LittleEndian(header),version=BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4)),type=BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8)),tag=BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12));
            if(length<16||length>ApplePlist.MaximumLength+16||version!=1||tag!=1)throw ApplePlist.Bad();
            byte[] reply=new byte[length-16];AppleWire.Read(stream,reply,token);
            try
            {
                if(type==1&&reply.Length==4)return new Dictionary<string,object>{{"Number",(ulong)BinaryPrimitives.ReadUInt32LittleEndian(reply)}};
                if(type!=8)throw ApplePlist.Bad();return ApplePlist.Decode(reply);
            }
            finally{CryptographicOperations.ZeroMemory(reply);}
        });
    }
    private static Dictionary<string,object> Lockdown(Stream stream,Dictionary<string,object> request,CancellationToken cancel)
    {
        request["Label"]="Porto";byte[] bytes=ApplePlist.Encode(request);
        return AppleWire.Command(cancel,token=>
        {
            byte[] header=new byte[4];BinaryPrimitives.WriteUInt32BigEndian(header,(uint)bytes.Length);
            AppleWire.Write(stream,header,token);AppleWire.Write(stream,bytes,token);AppleWire.Read(stream,header,token);
            uint size=BinaryPrimitives.ReadUInt32BigEndian(header);if(size==0||size>ApplePlist.MaximumLength)throw ApplePlist.Bad();
            byte[] reply=new byte[size];AppleWire.Read(stream,reply,token);var response=ApplePlist.Decode(reply);
            if(response.ContainsKey("Error"))throw new IOException("L'iPhone non consente la sessione Foto. Sbloccalo e usa un abbinamento già autorizzato negli strumenti Apple. Porto non crea nuovi abbinamenti.");
            if(ApplePlist.Text(response,"Request")!=ApplePlist.Text(request,"Request"))throw ApplePlist.Bad();
            return response;
        });
    }
    private sealed class PairIdentity : IDisposable
    {
        internal string HostId {get;}
        internal string SystemBuid {get;}
        private readonly X509Certificate2 certificate;
        private readonly byte[] peerCertificate;
        private PairIdentity(string host,string buid,X509Certificate2 certificate,byte[] peer){HostId=host;SystemBuid=buid;this.certificate=certificate;peerCertificate=peer;}
        internal static PairIdentity Read(string serial,CancellationToken cancel)
        {
            using var socket=OpenLoopback(cancel);
            var response=Mux(socket,new(){["MessageType"]="ReadPairRecord",["PairRecordID"]=serial},cancel);
            if(!response.TryGetValue("PairRecordData",out var raw)||raw is not byte[] bytes)throw new IOException("L'iPhone non risulta già abbinato. Porto non crea né modifica l'autorizzazione del computer.");
            Dictionary<string,object>? record=null;
            try
            {
                record=ApplePlist.Decode(bytes);
                string host=ApplePlist.Text(record,"HostID"),buid=ApplePlist.Text(record,"SystemBUID");
                if(host.Length is 0 or >128||buid.Length is 0 or >128)throw ApplePlist.Bad();
                byte[] cert=Data(record,"RootCertificate"),key=Data(record,"RootPrivateKey"),peer=Data(record,"DeviceCertificate");
                var own=X509Certificate2.CreateFromPem(Encoding.UTF8.GetString(cert),Encoding.UTF8.GetString(key));
                try
                {
                    using var expected=X509Certificate2.CreateFromPem(Encoding.UTF8.GetString(peer));
                    return new(host,buid,own,expected.RawData);
                }
                catch{own.Dispose();throw;}
            }
            catch(CryptographicException){throw new IOException("Credenziali dell'abbinamento Apple non utilizzabili; nessuna credenziale è stata modificata.");}
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
                if(record!=null)foreach(var value in record.Values)if(value is byte[] blob)CryptographicOperations.ZeroMemory(blob);
            }
        }
        private static byte[] Data(Dictionary<string,object> record,string key)=>record.TryGetValue(key,out var value)&&value is byte[] bytes?bytes:throw ApplePlist.Bad();
        internal Stream Protect(Stream stream,CancellationToken cancel)
            =>AppleTls.Protect(stream,certificate,peerCertificate,cancel);
        public void Dispose(){certificate.Dispose();CryptographicOperations.ZeroMemory(peerCertificate);}
    }
}

internal static class AppleWire
{
    internal static T Command<T>(CancellationToken cancel,Func<CancellationToken,T> operation)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancel);deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try{return operation(deadline.Token);}
        catch(OperationCanceledException) when(!cancel.IsCancellationRequested){throw new IOException("Il collegamento USB Apple ha superato il limite di tempo.");}
    }
    internal static void Read(Stream stream,Memory<byte> memory,CancellationToken cancel)=>stream.ReadExactlyAsync(memory,cancel).AsTask().GetAwaiter().GetResult();
    internal static void Write(Stream stream,ReadOnlyMemory<byte> memory,CancellationToken cancel)=>stream.WriteAsync(memory,cancel).AsTask().GetAwaiter().GetResult();
}
