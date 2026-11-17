using System.Buffers.Binary;
using System.Globalization;
using System.Diagnostics;
using System.Text;
using Porto.Core;

namespace Porto.Apple;

// Only four wire operations exist here: stat, read-only open, read, close. No write-capable mode.
internal sealed class AppleAfcFileSystem(Stream stream,Stream? session=null,IDisposable? credential=null,SourceTransportMetrics? metrics=null) : IAppleFileSystem
{
    private ulong packet;
    private ulong? activeHandle;
    private string? activePath;
    private bool disposed;
    private bool poisoned;
    public bool IsUsable=>!disposed&&!poisoned;
    internal int ReadLimit {get;init;}=512*1024;
    public AppleFileInfo? Stat(string path,CancellationToken cancel)
    {
        ValidatePath(path);var reply=Exchange(10,PathBytes(path),cancel);
        if(reply.Operation==1)
        {
            ulong status=Status(reply);if(status==8)return null;throw StatusError(status);
        }
        if(reply.Operation!=2)throw Poison();
        Dictionary<string,string> fields;
        try{fields=DecodeFields(reply.Data);}
        catch(Exception ex) when(ex is IOException or DecoderFallbackException){throw Poison();}
        if(!fields.TryGetValue("st_size",out var size)||!long.TryParse(size,NumberStyles.None,CultureInfo.InvariantCulture,out var length)||length<0||
           !fields.TryGetValue("st_mtime",out var mtime)||!ulong.TryParse(mtime,NumberStyles.None,CultureInfo.InvariantCulture,out _)||
           !fields.TryGetValue("st_ifmt",out var type))throw Poison();
        return new(length,mtime,type=="S_IFREG",type switch
        {"S_IFDIR"=>AppleNonFileKind.Directory,"S_IFLNK"=>AppleNonFileKind.SymbolicLink,_=>AppleNonFileKind.Unknown});
    }
    public Stream OpenRead(string path,CancellationToken cancel)
    {
        ValidatePath(path);Check();
        if(activeHandle!=null)throw new IOException("Una lettura Apple è già attiva.");
        byte[] name=PathBytes(path),request=new byte[8+name.Length];BinaryPrimitives.WriteUInt64LittleEndian(request,1);name.CopyTo(request,8);
        var reply=Exchange(13,request,cancel);
        if(reply.Operation==1)throw StatusError(Status(reply));
        if(reply.Operation!=14||reply.Data.Length!=8)throw Poison();
        ulong handle=BinaryPrimitives.ReadUInt64LittleEndian(reply.Data);if(handle==0)throw Poison();
        activeHandle=handle;activePath=path;return new Reader(this,handle,cancel);
    }
    private byte[] Read(ulong handle,int count,CancellationToken cancel)
    {
        if(activeHandle!=handle)throw new IOException("La lettura Apple non appartiene alla sessione attiva.");
        byte[] request=new byte[16];BinaryPrimitives.WriteUInt64LittleEndian(request,handle);BinaryPrimitives.WriteUInt64LittleEndian(request.AsSpan(8),(ulong)count);
        var reply=Exchange(15,request,cancel);
        if(reply.Operation==1)
        {
            ulong status=Status(reply);if(status is 0 or 14)return [];throw StatusError(status);
        }
        if(reply.Operation!=2||reply.Data.Length>count)throw Poison();
        if(metrics!=null)
        {if(activePath?.StartsWith("/PhotoData/",StringComparison.Ordinal)==true)metrics.CatalogBytes+=reply.Data.Length;else metrics.MediaBytes+=reply.Data.Length;}
        return reply.Data;
    }
    private void Close(ulong handle,CancellationToken cancel)
    {
        if(activeHandle!=handle)return;
        activeHandle=null;
        if(disposed||poisoned)return;
        try
        {
            byte[] request=new byte[8];BinaryPrimitives.WriteUInt64LittleEndian(request,handle);
            var reply=Exchange(20,request,cancel);if(reply.Operation!=1||Status(reply)!=0)throw Poison();
        }
        catch{poisoned=true;Dispose();}
    }
    private (ulong Operation,byte[] Data) Exchange(ulong operation,byte[] request,CancellationToken cancel)
    {
        Check();
        if(operation is not(10 or 13 or 15 or 20))throw new InvalidOperationException("Operazione AFC non autorizzata.");
        ulong sequence=++packet;
        long started=Stopwatch.GetTimestamp();
        if(metrics!=null)
        {
            metrics.Commands++;metrics.ReadBlockBytes=ReadLimit;
            if(operation==15)metrics.ReadRequests++;
            else if(operation==13)metrics.OpenRequests++;
            else if(operation==10)metrics.StatRequests++;
        }
        byte[] header=new byte[40];"CFA6LPAA"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(8),(ulong)(40+request.Length));
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(16),(ulong)(40+request.Length));
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(24),sequence);BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(32),operation);
        try
        {
            return AppleWire.Command(cancel,token=>
            {
                AppleWire.Write(stream,header,token);AppleWire.Write(stream,request,token);AppleWire.Read(stream,header,token);
                ulong whole=BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(8)),first=BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(16)),number=BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(24)),kind=BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(32));
                if(!header.AsSpan(0,8).SequenceEqual("CFA6LPAA"u8)||whole<40||whole>1024*1024||first<40||first>whole||number!=sequence||kind is not(1 or 2 or 14))throw ApplePlist.Bad();
                byte[] body=new byte[whole-40];AppleWire.Read(stream,body,token);return(kind,body);
            });
        }
        catch{if(metrics!=null)metrics.FailedExchanges++;poisoned=true;Dispose();throw;}
        finally{if(metrics!=null)metrics.CommandMilliseconds+=Stopwatch.GetElapsedTime(started).TotalMilliseconds;}
    }
    internal static void ValidatePath(string path)
    {
        bool catalog=path is "/PhotoData/Photos.sqlite" or "/PhotoData/Photos.sqlite-wal" or "/PhotoData/Photos.sqlite-journal";
        if(path.Length>4096||(!catalog&&!path.StartsWith("/DCIM/",StringComparison.Ordinal))||
           path.Contains('\\')||path.Contains(':')||path.Any(char.IsControl)||path[1..].Split('/').Any(p=>p is "" or "." or ".."))
            throw new IOException("Percorso Apple fuori dall'ambito Foto consentito.");
    }
    private static byte[] PathBytes(string path)=>Encoding.UTF8.GetBytes(path+'\0');
    private static Dictionary<string,string> DecodeFields(byte[] data)
    {
        if(data.Length==0||data[^1]!=0)throw ApplePlist.Bad();
        string[] values=new UTF8Encoding(false,true).GetString(data).TrimEnd('\0').Split('\0');
        if(values.Length%2!=0)throw ApplePlist.Bad();var result=new Dictionary<string,string>(StringComparer.Ordinal);
        for(int i=0;i<values.Length;i+=2)if(values[i].Length==0||!result.TryAdd(values[i],values[i+1]))throw ApplePlist.Bad();return result;
    }
    private ulong Status((ulong Operation,byte[] Data) reply)=>reply.Data.Length==8?BinaryPrimitives.ReadUInt64LittleEndian(reply.Data):throw Poison();
    private static IOException StatusError(ulong status)=>new($"L'iPhone non rende disponibile la risorsa Foto (AFC {status}).");
    private IOException Poison(){poisoned=true;Dispose();return ApplePlist.Bad();}
    private void Check(){if(disposed||poisoned)throw new SourceUnavailableException("Sessione Apple non più utilizzabile; ripeti l'inventario.");}
    public void Dispose(){if(disposed)return;disposed=true;activeHandle=null;stream.Dispose();session?.Dispose();credential?.Dispose();}
    private sealed class Reader(AppleAfcFileSystem owner,ulong handle,CancellationToken cancel) : Stream
    {
        private bool closed,ended;
        public override int Read(byte[] buffer,int offset,int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);ArgumentOutOfRangeException.ThrowIfNegative(offset);ArgumentOutOfRangeException.ThrowIfNegative(count);
            if(offset>buffer.Length-count)throw new ArgumentException("Buffer non valido.");
            ObjectDisposedException.ThrowIf(closed,this);cancel.ThrowIfCancellationRequested();if(count==0||ended)return 0;
            byte[] data=owner.Read(handle,Math.Min(count,owner.ReadLimit),cancel);data.CopyTo(buffer,offset);ended=data.Length==0;return data.Length;
        }
        protected override void Dispose(bool disposing){if(!closed){closed=true;if(disposing)owner.Close(handle,cancel);}base.Dispose(disposing);}
        public override bool CanRead=>!closed;public override bool CanSeek=>false;public override bool CanWrite=>false;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush(){}public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    }
}
