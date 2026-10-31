using System.Runtime.InteropServices;
using Porto.Core;

namespace Porto.Usb;

public sealed unsafe class WpdSource : IVideoSource
{
    private static readonly Guid ObjectKeys=new("ef6b490d-5cd8-437a-affc-da8b60ee4a3c");
    private static readonly Guid ClientKeys=new("204d9f0c-2292-4080-9f42-40664e70f859");
    private static readonly Guid Video=new("9261b03c-3d78-4519-85e3-02c5e1f50bb9");
    private static readonly Guid Image=new("ef2107d5-a52a-4243-a26b-62d4176d7603");
    private static readonly Guid Folder=new("27e2e392-a111-48e0-ab0c-e17705a05f85");
    private static readonly Guid Functional=new("99ed0160-17ff-4c44-9d98-1d7a6f941921");
    private readonly Com device,content,properties,resources;
    public Action<int>? InventoryProgress {get;set;}
    private static Com Values()=>Com.Create("0c15d503-d017-47ce-9016-7b3f978721cc","6848f6f2-3155-4f86-b6f5-263eeeab3143");
    private static Com Manager()=>Com.Create("0af10cec-2ecd-4b92-9581-34f6ae0637f3","a1567595-4c2f-4574-a6fa-ecef917b9a40");
    internal WpdSource(Com device,Com content,Com properties,Com resources)
    {this.device=device;this.content=content;this.properties=properties;this.resources=resources;}
    public WpdSource(string deviceId)
    {
        using var info=Values();
        info.SetString(new(ClientKeys,2),"Porto USB");
        info.SetUInt(new(ClientKeys,9),0x80000000); // GENERIC_READ; never retry with write access.
        device=Com.Create("f7c0039a-4762-488a-b4b3-760ef9a1ba9b","625e2df8-6392-4cf0-9ad1-3cfa5f17775c");
        try
        {
            fixed(char* id=deviceId)Com.Check(((delegate* unmanaged[Stdcall]<nint,char*,nint,int>)device.Slot(3))(device.Pointer,id,info.Pointer));
            content=device.Interface(5);properties=content.Interface(4);resources=content.Interface(5);
        }
        catch{resources?.Dispose();properties?.Dispose();content?.Dispose();device.Dispose();throw;}
    }
    public static List<DeviceInfo> Devices()=>Discover().Devices;
    public static DiscoveryResult Discover()=>DeviceDiscovery.Read(ReadDeviceBatch);
    private static DeviceBatch ReadDeviceBatch()
    {
        using var manager=Manager();return ReadDeviceBatch(manager);
    }
    internal static DeviceBatch ReadDeviceBatch(Com manager,Action<nint>? releaseString=null)
    {
        uint count=0;
        Com.Check(((delegate* unmanaged[Stdcall]<nint,nint*,uint*,int>)manager.Slot(3))(manager.Pointer,null,&count));
        if(count>1024)throw new IOException("Numero dispositivi non valido.");
        if(count==0)return new(0,[]);
        var pointers=new nint[count];List<DeviceDescriptor> devices=[];
        fixed(nint* ids=pointers)
        {
            uint capacity=count;
            try
            {
                int hr=((delegate* unmanaged[Stdcall]<nint,nint*,uint*,int>)manager.Slot(3))(manager.Pointer,ids,&count);
                Com.Check(hr);
                if(hr==1)return new(1,[]);
                if(count>capacity)throw new IOException("Elenco dispositivi cambiato: riprova.");
                for(int i=0;i<count;i++)
                {
                    string id=Marshal.PtrToStringUni(ids[i])??"";
                    string name=DeviceText(manager,id,5),manufacturer=DeviceText(manager,id,7);
                    devices.Add(new(id,name,manufacturer,string.IsNullOrWhiteSpace(name)||string.IsNullOrWhiteSpace(manufacturer)));
                }
            }
            finally{foreach(var p in pointers)if(p!=0){if(releaseString!=null)releaseString(p);else Marshal.FreeCoTaskMem(p);}}
        }
        return new(0,devices);
    }
    private static string DeviceText(Com manager,string id,int slot)
    {
        fixed(char* device=id)
        {
            uint length=0;var call=(delegate* unmanaged[Stdcall]<nint,char*,char*,uint*,int>)manager.Slot(slot);
            if(call(manager.Pointer,device,null,&length)<0||length==0||length>32768)return "";
            var chars=new char[length];fixed(char* text=chars){if(call(manager.Pointer,device,text,&length)<0)return "";return new string(chars).TrimEnd('\0').Trim();}
        }
    }
    private Com ReadProperties(string objectId)
    {
        nint result=0;
        fixed(char* id=objectId)Com.Check(((delegate* unmanaged[Stdcall]<nint,char*,nint,nint*,int>)properties.Slot(5))(properties.Pointer,id,0,&result));
        return new(result);
    }
    public Inventory Inventory(CancellationToken cancel)
    {
        List<SourceItem> items=[];List<string> errors=[];var guard=new EnumerationGuard();
        void Error(string message){guard.Error();errors.Add(message);}
        Queue<(string Id,int Depth)> pending=new();pending.Enqueue(("DEVICE",0));
        try{
        while(pending.TryDequeue(out var parent))
        {
            cancel.ThrowIfCancellationRequested();InventoryProgress?.Invoke(items.Count);
            if(parent.Depth>128){Error("Gerarchia USB troppo profonda.");continue;}
            try
            {
                nint p=0;
                fixed(char* id=parent.Id)Com.Check(((delegate* unmanaged[Stdcall]<nint,uint,char*,nint,nint*,int>)content.Slot(3))(content.Pointer,0,id,0,&p));
                using var enumeration=new Com(p);
                while(true)
                {
                    cancel.ThrowIfCancellationRequested();
                    var batch=new nint[64];uint fetched=0;int hr;
                    fixed(nint* ids=batch)hr=((delegate* unmanaged[Stdcall]<nint,uint,nint*,uint*,int>)enumeration.Slot(3))(enumeration.Pointer,64,ids,&fetched);
                    try
                    {
                        Com.Check(hr);if(fetched>64)throw new IOException("Risposta inventario USB non valida.");
                        for(int n=0;n<fetched;n++)
                        {
                            string objectId=Marshal.PtrToStringUni(batch[n])??"";
                            guard.Visit(objectId);
                            try
                            {
                                using var values=ReadProperties(objectId);
                                var kind=values.GuidValue(new(ObjectKeys,7));
                                MediaKind media=kind==Video?MediaKind.Video:kind==Image?MediaKind.Image:
                                    kind==Folder||kind==Functional?MediaKind.Container:MediaKind.Unknown;
                                string name=values.String(new(ObjectKeys,12))??values.String(new(ObjectKeys,4))??"Senza nome";
                                var item=new SourceItem(objectId,parent.Id,name,media,values.Length(new(ObjectKeys,11)),
                                    values.String(new(ObjectKeys,5)),values.GuidValue(new(ObjectKeys,6))?.ToString());
                                items.Add(item);
                                if(media==MediaKind.Container)pending.Enqueue((objectId,parent.Depth+1));
                                else if(media==MediaKind.Unknown&&!MediaInspector.Candidate(item))
                                    Error($"Tipo USB non riconosciuto per {name}; contenuto non esplorato.");
                            }
                            catch(IOException ex)when(ex is not SourceUnavailableException){Error($"Metadati elemento {items.Count+1}: {ex.Message}");}
                            InventoryProgress?.Invoke(items.Count);
                        }
                    }
                    finally{foreach(var id in batch)if(id!=0)Marshal.FreeCoTaskMem(id);}
                    InventoryProgress?.Invoke(items.Count);
                    if(fetched==0||hr==1)break;
                }
            }
            catch(IOException ex)when(ex is not SourceUnavailableException){Error("Enumerazione cartella USB: "+ex.Message);}
        }
        }catch(EnumerationStoppedException ex){errors.Add(ex.Message);}
        return new(items,errors,errors.Count==0);
    }
    public Stream OpenRead(SourceItem item,CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        var key=new PropertyKey(new("e81e79be-34f0-41bf-b53f-f1a06ae87842"),0);nint stream=0;uint buffer=0;
        fixed(char* id=item.Id)Com.Check(((delegate* unmanaged[Stdcall]<nint,char*,PropertyKey*,uint,uint*,nint*,int>)resources.Slot(5))
            (resources.Pointer,id,&key,0,&buffer,&stream)); // STGM_READ = 0.
        return new WpdReadStream(new(stream),Math.Clamp((int)Math.Min(buffer,int.MaxValue),4096,1024*1024));
    }
    public void Dispose(){resources.Dispose();properties.Dispose();content.Dispose();device.Dispose();}
    // No device discovery or connection: safe development smoke test for ABI and COM registration.
    public static void SmokeWithoutDevices()
    {
        Com.Check(Com.CoInitializeEx(0,0));
        try
        {
            using var manager=Manager();using var values=Values();
            values.SetString(new(ClientKeys,2),"Porto smoke");
            if(values.String(new(ClientKeys,2))!="Porto smoke")throw new IOException("COM ABI string roundtrip failed");
            values.SetUInt(new(ClientKeys,9),0x80000000);
            var key=new PropertyKey(ObjectKeys,6);var guid=Video;
            Com.Check(((delegate* unmanaged[Stdcall]<nint,PropertyKey*,Guid*,int>)values.Slot(27))(values.Pointer,&key,&guid));
            if(values.GuidValue(key)!=Video)throw new IOException("COM ABI GUID roundtrip failed");
            key=new(ObjectKeys,11);
            Com.Check(((delegate* unmanaged[Stdcall]<nint,PropertyKey*,ulong,int>)values.Slot(13))(values.Pointer,&key,4294967313UL));
            if(values.Length(key)!=4294967313L)throw new IOException("COM ABI UInt64 roundtrip failed");
            using var device=Com.Create("f7c0039a-4762-488a-b4b3-760ef9a1ba9b","625e2df8-6392-4cf0-9ad1-3cfa5f17775c");
        }
        finally{Com.CoUninitialize();}
    }
    private sealed class WpdReadStream(Com stream,int optimal):Stream
    {
        public override int Read(byte[] buffer,int offset,int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(offset);ArgumentOutOfRangeException.ThrowIfNegative(count);
            if(offset>buffer.Length-count)throw new ArgumentException("Buffer range");
            uint read=0;fixed(byte* data=buffer)Com.Check(((delegate* unmanaged[Stdcall]<nint,byte*,uint,uint*,int>)stream.Slot(3))
                (stream.Pointer,data+offset,(uint)Math.Min(count,optimal),&read));
            if(read>Math.Min(count,optimal))throw new IOException("USB returned an invalid length");
            return (int)read;
        }
        protected override void Dispose(bool disposing){if(disposing)stream.Dispose();base.Dispose(disposing);}
        public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush(){}public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();
        public override void SetLength(long v)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
    }
}
