using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Porto.Core;
using Porto.Usb;

internal static unsafe class Rc5InventoryTests
{
    private static string fileName="";
    private static Guid contentType,formatType;
    private static nint iteratorPointer;
    public static void Add(List<(string,Action)> tests)
    {
        foreach(var sample in new[]{
            (Name:"IMG_0001.AAE",Video:false,VideoFormat:false,Expected:MediaKind.Other,Complete:true),
            (Name:"IMG_0002.aae",Video:false,VideoFormat:false,Expected:MediaKind.Other,Complete:true),
            (Name:"opaque.bin",Video:false,VideoFormat:false,Expected:MediaKind.Unknown,Complete:false),
            (Name:"contradiction.AAE",Video:true,VideoFormat:false,Expected:MediaKind.Video,Complete:true),
            (Name:"format-video.AAE",Video:false,VideoFormat:true,Expected:MediaKind.Unknown,Complete:true)})
        {
            tests.Add(($"rc5 inventory classifies sidecars without hiding contrary video metadata: {sample.Name}",()=>
            {
                fileName=sample.Name;
                contentType=sample.Video?new("9261b03c-3d78-4519-85e3-02c5e1f50bb9"):Guid.Empty;
                formatType=sample.VideoFormat?new("b9820000-ae6c-4804-98ba-c57b46965fe7"):Guid.Empty;
                Com.Check(Com.CoInitializeEx(0,0));
                try
                {
                    using var device=new Native();using var content=new Native();using var properties=new Native();
                    using var resources=new Native();using var iterator=new Native();iteratorPointer=iterator.Pointer;
                    content.Set(3,(nint)(delegate* unmanaged[Stdcall]<nint,uint,char*,nint,nint*,int>)&Enumerate);
                    properties.Set(5,(nint)(delegate* unmanaged[Stdcall]<nint,char*,nint,nint*,int>)&Values);
                    iterator.Set(3,(nint)(delegate* unmanaged[Stdcall]<nint,uint,nint*,uint*,int>)&Next);
                    using var source=new WpdSource(new(device.Pointer),new(content.Pointer),new(properties.Pointer),new(resources.Pointer));
                    var inventory=source.Inventory(default);var item=inventory.Items.Single();
                    Fixtures.Check(item.Kind==sample.Expected,$"Expected {sample.Expected}, received {item.Kind}");
                    Fixtures.Check(inventory.EnumerationComplete==sample.Complete,"Recognized sidecars must not produce an incomplete inventory.");
                    Fixtures.Check(inventory.Errors.Count==(sample.Complete?0:1));
                    Fixtures.Check(MediaInspector.Candidate(item)==(sample.Video||sample.VideoFormat),"Contrary content metadata must remain eligible for video verification.");
                }
                finally{Com.CoUninitialize();}
            }));
        }
        tests.Add(("rc5 inventory mixed resources separate transfer candidates and do not retry sidecars",()=>
        {
            byte[] video=Fixtures.Video();
            List<SourceItem> items=[
                new("photo1","dcim","IMG_0001.JPG",MediaKind.Image,10),
                new("photo2","dcim","IMG_0002.JPG",MediaKind.Image,10),
                new("sidecar1","dcim","IMG_0001.AAE",MediaKind.Other,10),
                new("sidecar2","dcim","IMG_0002.AAE",MediaKind.Other,10),
                new("companion1","dcim","IMG_0001.MOV",MediaKind.Video,100),
                new("companion2","dcim","IMG_0002.MOV",MediaKind.Video,100),
                new("standalone","dcim","IMG_0003.MOV",MediaKind.Video,video.LongLength)];
            int reads=0;
            using var destination=new Destination(Fixtures.Root());
            var session=new ImportSession(()=>new FakeSource(items,[]){Reader=(item,_)=>
            {
                Fixtures.Check(item.Id=="standalone","The importer opened a photo, sidecar or suspected companion.");
                reads++;return new MemoryStream(video,false);
            }},destination){RetryDelay=TimeSpan.Zero};
            var report=session.Run(false,default);
            Fixtures.Check(session.Attempts==1&&!report.RetryRecommended,"Known AAE sidecars must not force repeated inventories.");
            Fixtures.Check(report.UsbEnumerationComplete&&report.FinalInventoryChecked&&report.InventoryErrors.Count==0);
            Fixtures.Check(report.UsbItemCount==7&&report.ImageCount==2&&report.SidecarCount==2);
            Fixtures.Check(report.CandidateCount==3&&report.SuspectedCompanionCount==2&&report.TransferCandidateCount==1);
            Fixtures.Check(report.Count(Outcome.Imported)==1&&report.Count(Outcome.Ambiguous)==2&&report.Count(Outcome.Excluded)==4&&report.Count(Outcome.Error)==0);
            Fixtures.Check(reads==1&&Directory.GetFiles(destination.Videos).Length==1);
            Fixtures.Check(report.Items.Where(item=>item.Name.EndsWith(".AAE",StringComparison.Ordinal)).All(item=>item.Outcome==Outcome.Excluded&&item.Detail.Contains("AAE",StringComparison.Ordinal)));
            Fixtures.Check(!report.Status.StartsWith("Completati",StringComparison.Ordinal),"Suspected companions remain explicitly unverified.");
            string text=File.ReadAllText(session.ReportPath!);
            Fixtures.Check(text.Contains("file accessori AAE: 2",StringComparison.Ordinal)&&text.Contains("candidati alla copia: 1",StringComparison.Ordinal));
        }));
        tests.Add(("rc5 inventory sidecar classification does not erase legacy unknown obligations",()=>
        {
            using var destination=new Destination(Fixtures.Root());
            var legacy=new SourceItem("legacy-sidecar","dcim","IMG_0001.AAE",MediaKind.Unknown,10);
            using(var ledger=new WorkLedger(destination))
            {
                ledger.Observe([legacy],"synthetic-legacy");
                ledger.Record(legacy,new(legacy.Id,legacy.Name,Outcome.Ambiguous,"Synthetic old unresolved metadata."));
            }
            var report=new ImportEngine(new FakeSource([legacy with{Kind=MediaKind.Other}],[]),destination).Run(false,default);
            Fixtures.Check(report.Items.Any(item=>item.Outcome==Outcome.Ambiguous),"Extension alone cannot erase a historical unread observation.");
            Fixtures.Check(!report.Status.StartsWith("Completati",StringComparison.Ordinal));
        }));
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static uint Release(nint self)=>0;
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int Enumerate(nint self,uint flags,char* parent,nint filter,nint* output){*output=iteratorPointer;return 0;}
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int Next(nint self,uint count,nint* ids,uint* fetched)
    {ids[0]=Marshal.StringToCoTaskMemUni("synthetic-sidecar");*fetched=1;return 1;}
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int Values(nint self,char* id,nint keys,nint* output)
    {
        try
        {
            var values=Com.Create("0c15d503-d017-47ce-9016-7b3f978721cc","6848f6f2-3155-4f86-b6f5-263eeeab3143");
            Guid group=new("ef6b490d-5cd8-437a-affc-da8b60ee4a3c");
            values.SetString(new(group,12),fileName);
            var key=new PropertyKey(group,7);var kind=contentType;
            Com.Check(((delegate* unmanaged[Stdcall]<nint,PropertyKey*,Guid*,int>)values.Slot(27))(values.Pointer,&key,&kind));
            key=new(group,6);var format=formatType;
            Com.Check(((delegate* unmanaged[Stdcall]<nint,PropertyKey*,Guid*,int>)values.Slot(27))(values.Pointer,&key,&format));
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
