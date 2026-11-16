using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Porto.Core;
using Porto.Usb;

// All trees, object IDs and subprocesses are synthetic. No real device is discovered.
internal static unsafe class UsbNavigationTests
{
    private static readonly Dictionary<string,SourceItem[]> tree=new(StringComparer.Ordinal);
    private static readonly List<string> enumerated=[];
    private static SourceItem[] current=[];
    private static int position;
    private static nint iteratorPointer;
    private static string? failedMetadata;
    private static readonly Dictionary<string,string[]> references=new(StringComparer.Ordinal);
    private static readonly Dictionary<string,Guid> albumTypes=new(StringComparer.Ordinal);
    private static readonly List<(string Id,uint[] Keys)> propertyRequests=[];
    private static readonly UsbFolderChoice selected=new("Internal storage / DCIM",
        [new("Internal storage","persistent-storage"),new("DCIM","persistent-camera")]);
    private static readonly UsbCollectionChoice selectedAlbum=new("Internal storage / Trip",
        [new("Internal storage","persistent-storage"),new("Trip","persistent-album")]);

    public static int Child(string mode)
    {
        string scenario=mode["--usb-navigation-".Length..];
        using var writer=new BinaryWriter(Console.OpenStandardOutput());
        void Frame(byte kind,byte[] bytes){writer.Write(kind);writer.Write(bytes.Length);writer.Write(bytes);writer.Flush();}
        void Json<T>(T value)=>Frame(0,JsonSerializer.SerializeToUtf8Bytes(value));
        void Error(string message)=>Frame(3,Encoding.UTF8.GetBytes(message));
        var media=new SourceItem("video","camera","one.mp4",MediaKind.Video,scenario=="changed:2"?4:3);
        int offset=0;string? line;
        while((line=Console.ReadLine())!=null)
        {
            var command=JsonSerializer.Deserialize<Command>(line)!;
            switch(command.Op)
            {
                case "connect":if(command.Device=="synthetic-navigation-device")Json(true);else Error("Wrong device.");break;
                case "folders":
                    if(scenario=="hang")Thread.Sleep(30000);
                    if(command.Folder==null)Json(new[]{new UsbFolderChoice("Internal storage",[selected.Segments[0]])});
                    else if(command.Folder.Segments.SequenceEqual(selected.Segments.Take(1)))Json(new[]{selected});
                    else Error("Unexpected navigation parent.");
                    break;
                case "inventory":
                    if(scenario.StartsWith("album",StringComparison.Ordinal))
                    {
                        if(command.Folder!=null||command.Collection==null||!command.Collection.Segments.SequenceEqual(selectedAlbum.Segments))
                        {Error("Native collection lost its exact selection.");break;}
                        var member=media with{ParentId="album",Size=scenario=="albumchanged:2"?4:3};
                        Json(new Inventory([Folder("storage","DEVICE","Internal storage"),
                            new("album","storage","Trip",MediaKind.Collection,PersistentId:"persistent-album"),member],[],true));break;
                    }
                    if(command.Folder==null||!command.Folder.Segments.SequenceEqual(selected.Segments))
                    {Error("Scoped inventory became an unscoped or unrelated inventory.");break;}
                    Json(new Inventory([Folder("storage","DEVICE","Internal storage"),Folder("camera","storage","DCIM"),media],[],true));break;
                case "collections":
                    if(command.Folder?.Segments.SequenceEqual(selectedAlbum.Segments.Take(1))==true)Json(new[]{selectedAlbum});
                    else Error("Collection discovery widened its parent.");break;
                case "browse":
                    if(command.Folder?.Segments.SequenceEqual(selectedAlbum.Segments.Take(1))==true)Json(new UsbNavigationLevel([selected],[selectedAlbum]));
                    else Error("Combined navigation widened its parent.");break;
                case "open":if(command.Object==media.Id){offset=0;Json(true);}else Error("Obsolete object opened.");break;
                case "read":byte[] data=[1,2,3];int count=Math.Min(command.Count,data.Length-offset);Frame(2,data.AsSpan(offset,count).ToArray());offset+=count;break;
                default:Error("Unexpected operation.");break;
            }
        }
        return 0;
    }
    public static void Add(List<(string,Action)> tests)
    {
        tests.Add(("USB navigation opens immediate folders without scanning 60000 unrelated files",()=>WithNative(source=>
        {
            var folders=(object)source as IUsbFolderSource??throw new Exception("USB source has no lazy folder navigation.");
            var storage=folders.ListFolders(null,default).Single();
            Fixtures.Check(enumerated.SequenceEqual(["DEVICE"]),"Opening navigation descended below the device root.");
            enumerated.Clear();
            var camera=folders.ListFolders(storage,default).Single(folder=>folder.Segments.Last().Name=="DCIM");
            Fixtures.Check(enumerated.SequenceEqual(["DEVICE","storage"]),"A folder listing descended into an unrelated directory.");
            enumerated.Clear();
            var inventory=folders.Inventory(camera,default);
            Fixtures.Check(inventory.EnumerationComplete&&inventory.Items.Select(item=>item.Id).ToHashSet().SetEquals(
                ["storage","camera","photo","subfolder","video"]));
            Fixtures.Check(enumerated.SequenceEqual(["DEVICE","storage","camera","subfolder"]),"Scoped inventory touched an unrelated subtree.");
        })));
        tests.Add(("USB navigation resolves remapped object IDs from a fresh physical path",()=>WithNative(source=>
        {
            var choice=source.ListFolders(source.ListFolders(null,default).Single(),default).Single(folder=>folder.Segments.Last().Name=="DCIM");
            var old=tree["storage"][0];
            tree["storage"][0]=old with{Id="new-camera"};
            tree["new-camera"]=tree["camera"].Select(item=>item with{ParentId="new-camera"}).ToArray();
            tree.Remove("camera");enumerated.Clear();
            var inventory=source.Inventory(choice,default);
            Fixtures.Check(inventory.EnumerationComplete&&inventory.Items.Any(item=>item.Id=="new-camera")&&!enumerated.Contains("camera"));
        })));
        foreach(string mutation in new[]{"missing","changed-persistent-id","ambiguous"})
            tests.Add(($"USB navigation rejects {mutation} selected folders without widening scope",()=>WithNative(source=>
            {
                if(mutation=="missing")tree["storage"]=[tree["storage"][1]];
                if(mutation=="changed-persistent-id")tree["storage"][0]=tree["storage"][0] with{PersistentId="different"};
                if(mutation=="ambiguous")tree["storage"]=[..tree["storage"],Folder("duplicate","storage","DCIM")];
                Fixtures.Throws<IOException>(()=>source.Inventory(selected,default));
                Fixtures.Check(enumerated.SequenceEqual(["DEVICE","storage"]),"Failed path resolution scanned descendants.");
                if(mutation=="ambiguous")Fixtures.Check(source.ListFolders(new("Internal storage",[selected.Segments[0]]),default)
                    .All(folder=>folder.Segments.Last().Name!="DCIM"),"Ambiguous siblings were offered as an exact selection.");
                if(mutation=="ambiguous")Fixtures.Check(source.ReadLevel(new("Internal storage",[selected.Segments[0]]),default).Warnings is {Count:1},
                    "Omitted ambiguous names must be visible as a navigation warning.");
            })));
        tests.Add(("USB navigation refuses partial folder listings and pre-cancelled operations",()=>WithNative(source=>
        {
            Fixtures.Throws<OperationCanceledException>(()=>source.ListFolders(null,new(true)));
            Fixtures.Check(enumerated.Count==0,"Cancelled navigation touched native enumeration.");
            failedMetadata="camera";
            Fixtures.Throws<IOException>(()=>source.Inventory(selected,default));
            Fixtures.Check(enumerated.SequenceEqual(["DEVICE","storage"]));
        })));
        tests.Add(("USB navigation scoped enumeration rejects cycles and keeps progress monotonic",()=>WithNative(source=>
        {
            List<int> progress=[];source.InventoryProgress=progress.Add;
            tree["subfolder"]=[tree["storage"][0] with{ParentId="subfolder"}];
            var inventory=source.Inventory(selected,default);
            Fixtures.Check(!inventory.EnumerationComplete&&inventory.Errors.Count>0&&!enumerated.Contains("unrelated"));
            Fixtures.Check(progress.SequenceEqual(progress.Order()),"Scoped inventory progress moved backwards between folders.");
        })));
        tests.Add(("USB navigation worker transports only the requested parent and scope",()=>
        {
            using var source=new ReconnectingUsbSource(()=>new(Environment.ProcessPath!,"--usb-navigation-same:1"),"synthetic-navigation-device");
            var storage=source.ListFolders(null,default).Single();
            var camera=source.ListFolders(storage,default).Single();
            var inventory=source.Inventory(camera,default);
            using(var stream=source.OpenRead(inventory.Items.Last(),default))Fixtures.Check(stream.ReadByte()==1);
            source.ListFolders(null,default);
            Fixtures.Throws<SourceUnavailableException>(()=>source.OpenRead(inventory.Items.Last(),default));
        }));
        foreach(bool changed in new[]{false,true})
            tests.Add(($"USB navigation recovery revalidates the same scope and rejects changed media={changed}",()=>
            {
                int generation=0;
                using var source=new ReconnectingUsbSource(()=>new(Environment.ProcessPath!,"--usb-navigation-"+(changed?"changed:":"same:")+ ++generation),"synthetic-navigation-device");
                var inventory=source.Inventory(selected,default);source.InvalidateRead();
                if(changed)Fixtures.Throws<PreviousReadCleanupException>(()=>source.OpenRead(inventory.Items.Last(),default));
                else{using var stream=source.OpenRead(inventory.Items.Last(),default);Fixtures.Check(stream.ReadByte()==1);}
                Fixtures.Check(generation==2&&source.SessionRestarts==1);
            }));
        tests.Add(("USB navigation failed scoped refresh invalidates previous read authorization",()=>
        {
            using var source=new ReconnectingUsbSource(()=>new(Environment.ProcessPath!,"--usb-navigation-same:1"),"synthetic-navigation-device");
            var inventory=source.Inventory(selected,default);
            Fixtures.Throws<IOException>(()=>source.Inventory(new UsbFolderChoice("wrong",[new("wrong",null)]),default));
            Fixtures.Throws<SourceUnavailableException>(()=>source.OpenRead(inventory.Items.Last(),default));
        }));
        foreach(bool cancel in new[]{false,true})
            tests.Add(($"USB navigation stuck folder listing terminates on {(cancel?"cancellation":"timeout")}",()=>
            {
                using var worker=new WorkerClient(Environment.ProcessPath!,"--usb-navigation-hang"){Timeout=TimeSpan.FromMilliseconds(500)};
                worker.Connect("synthetic-navigation-device",default);
                using var cancellation=new CancellationTokenSource();if(cancel)cancellation.CancelAfter(150);
                var timer=Stopwatch.StartNew();
                if(cancel)Fixtures.Throws<OperationCanceledException>(()=>worker.ListFolders(null,cancellation.Token));
                else Fixtures.Throws<SourceUnavailableException>(()=>worker.ListFolders(null,default));
                Fixtures.Check(worker.IsDisposed&&timer.Elapsed<TimeSpan.FromSeconds(5));
            }));
        foreach(string type in new[]{"75793148-15f5-4a30-a813-54ed8a37e226","012b0db7-d4c1-45d6-b081-94b87779614f","00f0c3ac-a593-49ac-9219-24abca5a2563"})
            tests.Add(($"USB navigation native album {type} reads only its explicit references",()=>WithNative(source=>
            {
                AddAlbum(new(type),["photo","remote-image"]);
                tree["unrelated"][17]=new("remote-image","unrelated","elsewhere.jpg",MediaKind.Image);
                var storage=source.ListFolders(null,default).Single();
                var albums=source.ListCollections(storage,default);
                Fixtures.Check(albums.Count==1&&source.ListFolders(storage,default).All(folder=>folder.Segments.Last().Name!="Trip"));
                Fixtures.Check(propertyRequests.All(request=>request.Keys.SequenceEqual(new uint[]{4,5,6,7,11,12})),
                    "Browsing fetched every native property or downloaded collection membership prematurely.");
                enumerated.Clear();propertyRequests.Clear();
                var inventory=source.Inventory(albums.Single(),default);
                Fixtures.Check(inventory.EnumerationComplete&&inventory.Items.Select(item=>item.Id).ToHashSet().SetEquals(
                    ["storage","album","photo","remote-image"]));
                Fixtures.Check(inventory.Items.Where(item=>item.Kind==MediaKind.Image).All(item=>item.ParentId=="album"));
                Fixtures.Check(enumerated.SequenceEqual(["DEVICE","storage"]),"Album membership traversed physical siblings or the entire phone.");
                Fixtures.Check(propertyRequests.Count(request=>request.Keys.SequenceEqual(new uint[]{14}))==1);
            })));
        tests.Add(("USB navigation whole-device inventory does not descend albums or duplicate their references",()=>WithNative(source=>
        {
            AddAlbum(new("75793148-15f5-4a30-a813-54ed8a37e226"),["photo"]);
            tree["unrelated"]=[];
            var inventory=source.Inventory(default);
            Fixtures.Check(inventory.EnumerationComplete&&inventory.Items.Count(item=>item.Id=="photo")==1&&!enumerated.Contains("album"));
            Fixtures.Check(propertyRequests.All(request=>!request.Keys.Contains(14u)));
        })));
        foreach(string shape in new[]{"empty","missing","duplicate","cycle","nested","unknown","missing-member","invalid-type"})
            tests.Add(($"USB navigation native album {shape} references never widen to folders",()=>WithNative(source=>
            {
                AddAlbum(new("00f0c3ac-a593-49ac-9219-24abca5a2563"),shape switch
                {
                    "empty" or "missing"=>[],"duplicate"=>["photo","photo"],"cycle"=>["album"],"nested"=>["camera"],
                    "unknown"=>["unknown"],"missing-member"=>["absent"],_=>["invalid-type"]
                });
                if(shape=="missing")references.Remove("album");
                if(shape=="unknown")tree["camera"]=[..tree["camera"],new("unknown","camera","unknown.bin",MediaKind.Unknown)];
                if(shape is "missing" or "invalid-type")Fixtures.Throws<IOException>(()=>source.Inventory(selectedAlbum,default));
                else
                {
                    var inventory=source.Inventory(selectedAlbum,default);
                    Fixtures.Check(inventory.EnumerationComplete==(shape=="empty"));
                    if(shape=="unknown")Fixtures.Check(inventory.Items.Any(item=>item.Id=="unknown"&&item.Kind==MediaKind.Unknown));
                }
                Fixtures.Check(enumerated.SequenceEqual(["DEVICE","storage"]));
            })));
        tests.Add(("USB navigation album selection rejects a replacement physical folder",()=>WithNative(source=>
        {
            AddAlbum(new("75793148-15f5-4a30-a813-54ed8a37e226"),["photo"]);
            tree["storage"][^1]=tree["storage"][^1] with{Kind=MediaKind.Container};albumTypes.Clear();
            Fixtures.Throws<IOException>(()=>source.Inventory(selectedAlbum,default));
            Fixtures.Check(propertyRequests.All(request=>!request.Keys.Contains(14u))&&!enumerated.Contains("album"));
        })));
        foreach(bool changed in new[]{false,true})
            tests.Add(($"USB navigation native album recovery preserves membership scope; changed={changed}",()=>
            {
                int generation=0;
                using var source=new ReconnectingUsbSource(()=>new(Environment.ProcessPath!,"--usb-navigation-"+(changed?"albumchanged:":"album:")+ ++generation),"synthetic-navigation-device");
                var album=source.ListCollections(new("Internal storage",[selected.Segments[0]]),default).Single();
                var inventory=source.Inventory(album,default);source.InvalidateRead();
                if(changed)Fixtures.Throws<PreviousReadCleanupException>(()=>source.OpenRead(inventory.Items.Last(),default));
                else{using var stream=source.OpenRead(inventory.Items.Last(),default);Fixtures.Check(stream.ReadByte()==1);}
                Fixtures.Check(generation==2&&source.SessionRestarts==1);
            }));
        tests.Add(("USB navigation combines folders and albums in one native level read and worker call",()=>
        {
            WithNative(source=>
            {
                AddAlbum(new("75793148-15f5-4a30-a813-54ed8a37e226"),["photo"]);
                var level=source.ReadLevel(new("Internal storage",[selected.Segments[0]]),default);
                Fixtures.Check(level.Folders.Count==2&&level.Collections.Count==1&&enumerated.SequenceEqual(["DEVICE","storage"]));
                Fixtures.Check(propertyRequests.Count==4&&propertyRequests.All(request=>!request.Keys.Contains(14u)));
            });
            using var worker=new ReconnectingUsbSource(()=>new(Environment.ProcessPath!,"--usb-navigation-album:1"),"synthetic-navigation-device");
            var remote=worker.ReadLevel(new("Internal storage",[selected.Segments[0]]),default);
            Fixtures.Check(remote.Folders.Count==1&&remote.Collections.Count==1);
        }));
    }

    private static void AddAlbum(Guid type,string[] members)
    {
        tree["storage"]=[..tree["storage"],new("album","storage","Trip",MediaKind.Collection,PersistentId:"persistent-album")];
        references["album"]=members;albumTypes["album"]=type;
    }

    private static void WithNative(Action<WpdSource> inspect)
    {
        Com.Check(Com.CoInitializeEx(0,0));
        try
        {
            tree.Clear();enumerated.Clear();references.Clear();albumTypes.Clear();propertyRequests.Clear();failedMetadata=null;
            tree["DEVICE"]=[Folder("storage","DEVICE","Internal storage")];
            tree["storage"]=[Folder("camera","storage","DCIM"),Folder("unrelated","storage","Application cache")];
            tree["camera"]=[new("photo","camera","one.jpg",MediaKind.Image),Folder("subfolder","camera","Trip")];
            tree["subfolder"]=[new("video","subfolder","one.mp4",MediaKind.Video)];
            tree["unrelated"]=Enumerable.Range(0,60000).Select(index=>new SourceItem("cache-"+index,"unrelated",index+".bin",MediaKind.Other)).ToArray();
            using var device=new Native();using var content=new Native();using var properties=new Native();using var resources=new Native();using var iterator=new Native();
            iteratorPointer=iterator.Pointer;
            content.Set(3,(nint)(delegate* unmanaged[Stdcall]<nint,uint,char*,nint,nint*,int>)&Enumerate);
            properties.Set(5,(nint)(delegate* unmanaged[Stdcall]<nint,char*,nint,nint*,int>)&Values);
            iterator.Set(3,(nint)(delegate* unmanaged[Stdcall]<nint,uint,nint*,uint*,int>)&Next);
            using var source=new WpdSource(new(device.Pointer),new(content.Pointer),new(properties.Pointer),new(resources.Pointer));
            inspect(source);
        }
        finally{Com.CoUninitialize();}
    }
    private static SourceItem Folder(string id,string parent,string name)=>new(id,parent,name,MediaKind.Container,PersistentId:"persistent-"+id);
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static uint Release(nint self)=>0;
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int Enumerate(nint self,uint flags,char* parent,nint filter,nint* output)
    {
        string id=new(parent);enumerated.Add(id);position=0;
        if(!tree.TryGetValue(id,out var children))return unchecked((int)0x80004005);
        current=children;*output=iteratorPointer;return 0;
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int Next(nint self,uint count,nint* ids,uint* fetched)
    {
        int remaining=Math.Min((int)count,current.Length-position);
        for(int index=0;index<remaining;index++)ids[index]=Marshal.StringToCoTaskMemUni(current[position++].Id);
        *fetched=(uint)remaining;return position==current.Length?1:0;
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int Values(nint self,char* id,nint keys,nint* output)
    {
        try
        {
            if(new string(id)==failedMetadata)return unchecked((int)0x80004005);
            string objectId=new(id);
            if(keys==0)throw new IOException("Synthetic driver refuses an unbounded all-properties query.");
            uint count=0;Com keyView=new(keys);
            Com.Check(((delegate* unmanaged[Stdcall]<nint,uint*,int>)keyView.Slot(3))(keys,&count));
            var requested=new uint[count];
            for(uint index=0;index<count;index++)
            {
                PropertyKey property=default;
                Com.Check(((delegate* unmanaged[Stdcall]<nint,uint,PropertyKey*,int>)keyView.Slot(4))(keys,index,&property));requested[index]=property.Id;
            }
            propertyRequests.Add((objectId,requested));
            var item=tree.Values.SelectMany(items=>items).First(item=>item.Id==objectId);
            var values=Com.Create("0c15d503-d017-47ce-9016-7b3f978721cc","6848f6f2-3155-4f86-b6f5-263eeeab3143");
            Guid group=new("ef6b490d-5cd8-437a-affc-da8b60ee4a3c");
            values.SetString(new(group,12),item.Name);
            if(item.PersistentId!=null)values.SetString(new(group,5),item.PersistentId);
            var key=new PropertyKey(group,7);var kind=albumTypes.GetValueOrDefault(objectId,item.Kind switch
            {
                MediaKind.Container=>new Guid("27e2e392-a111-48e0-ab0c-e17705a05f85"),
                MediaKind.Image=>new Guid("ef2107d5-a52a-4243-a26b-62d4176d7603"),
                MediaKind.Video=>new Guid("9261b03c-3d78-4519-85e3-02c5e1f50bb9"),
                MediaKind.Unknown=>Guid.Empty,
                _=>new Guid("0085e0a6-8d34-45d7-bc5c-447e59c73d48")
            });
            Com.Check(((delegate* unmanaged[Stdcall]<nint,PropertyKey*,Guid*,int>)values.Slot(27))(values.Pointer,&key,&kind));
            if(requested.Contains(14u)&&references.TryGetValue(objectId,out var members))
            {
                using var collection=Com.Create("08a99e2f-6d6d-4b80-af5a-baf2bcbe4cb9","89b2e422-4f1b-4316-bcef-a44afea83eb3");
                foreach(string member in members)
                {
                    var value=new PropVariant{Type=(ushort)(member=="invalid-type"?3:31),Pointer=member=="invalid-type"?0:Marshal.StringToCoTaskMemUni(member)};
                    try{Com.Check(((delegate* unmanaged[Stdcall]<nint,PropVariant*,int>)collection.Slot(5))(collection.Pointer,&value));}
                    finally{if(value.Pointer!=0)Marshal.FreeCoTaskMem(value.Pointer);}
                }
                key=new(group,14);
                Com.Check(((delegate* unmanaged[Stdcall]<nint,PropertyKey*,nint,int>)values.Slot(33))(values.Pointer,&key,collection.Pointer));
            }
            *output=values.Pointer;return 0;
        }
        catch{return unchecked((int)0x80004005);}
    }
    private sealed class Native:IDisposable
    {
        private readonly nint table=(nint)NativeMemory.AllocZeroed(40,(nuint)IntPtr.Size);
        public nint Pointer {get;}=(nint)NativeMemory.AllocZeroed((nuint)IntPtr.Size);
        public Native(){*(nint*)Pointer=table;Set(2,(nint)(delegate* unmanaged[Stdcall]<nint,uint>)&Release);}
        public void Set(int index,nint method)=>((nint*)table)[index]=method;
        public void Dispose(){NativeMemory.Free((void*)Pointer);NativeMemory.Free((void*)table);}
    }
}
