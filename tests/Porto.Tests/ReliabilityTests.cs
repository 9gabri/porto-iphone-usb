using Porto.Core;
using Porto.Usb;
using static Fixtures;

internal static class ReliabilityTests
{
    public static void Add(List<(string,Action)> tests)
    {
        tests.Add(("discovery retries S_FALSE instead of silently losing Apple device",()=>{
            int calls=0;var result=DeviceDiscovery.Read(()=>++calls==1?new(1,[]):new(0,[new("usb#vid_05ac","iPhone","Apple")]));
            Fixtures.Check(calls==2&&result.Devices.Count==1);
            Fixtures.Throws<IOException>(()=>DeviceDiscovery.Read(()=>new(1,[])));
        }));
        tests.Add(("discovery distinguishes Windows empty, non Apple and incomplete metadata",()=>{
            var empty=DeviceDiscovery.Read(()=>new(0,[]));
            var other=DeviceDiscovery.Read(()=>new(0,[new("other","Camera","Vendor")]));
            var unknown=DeviceDiscovery.Read(()=>new(0,[new("other","Camera","",true)]));
            Fixtures.Check(empty.WindowsDeviceCount==0&&other.WindowsDeviceCount==1&&unknown.MetadataFailures==1);
            Fixtures.Check(empty.Detail!=other.Detail&&other.Detail!=unknown.Detail);
        }));
        tests.Add(("fresh equal-sized videos use one source read each",()=>{
            using var d=new Destination(Root());var s=Several(5);
            Fixtures.Check(new ImportEngine(s,d).Run(false,default).Count(Outcome.Imported)==5);Fixtures.Check(s.Reads==5,$"Reads {s.Reads}");
            using var deep=new Destination(Root());var ds=Several(5);
            Fixtures.Check(new ImportEngine(ds,deep).Run(true,default).Count(Outcome.Imported)==5);Fixtures.Check(ds.Reads==10);
        }));
        tests.Add(("isolated damaged source does not block healthy subsequent videos",()=>{
            using var d=new Destination(Root());var s=Several(3);
            s.Reader=(item,_)=>item.Id=="1"?throw new IOException("synthetic unavailable item"):new MemoryStream(Video(int.Parse(item.Id)));
            var report=new ImportEngine(s,d).Run(false,default);
            Fixtures.Check(report.Count(Outcome.Imported)==2&&report.Count(Outcome.Error)==1&&report.RetryRecommended);
        }));
        tests.Add(("one session retries transient error and completes without duplicate outputs",()=>{
            using var d=new Destination(Root());int pass=0;
            var session=new ImportSession(()=>{var s=Several(3);if(++pass==1)s.Reader=(item,_)=>item.Id=="1"?throw new IOException("transient"):new MemoryStream(Video(int.Parse(item.Id)));return s;},d){RetryDelay=TimeSpan.Zero};
            var report=session.Run(false,default);
            Fixtures.Check(pass==2&&report.Count(Outcome.Error)==0&&report.Count(Outcome.Pending)==0,report.Status);
            Fixtures.Check(report.Status.StartsWith("Completati")&&Directory.GetFiles(d.Videos).Length==3);
        }));
        tests.Add(("unreached inventory items persist when next inventory is empty",()=>{
            using var d=new Destination(Root());var s=Several(5);s.Reader=(_,_)=>throw new SourceUnavailableException("disconnected");
            var first=new ImportEngine(s,d).Run(false,default);Fixtures.Check(first.Count(Outcome.Pending)==4&&first.Count(Outcome.Error)==1);
            var next=new ImportEngine(new FakeSource([],[]),d).Run(false,default);
            Fixtures.Check(next.Count(Outcome.Pending)==4&&next.Count(Outcome.Error)==1&&!next.Status.StartsWith("Completati"));
        }));
        tests.Add(("legacy Copying and quarantine cannot disappear under empty inventory",()=>{
            using var d=new Destination(Root());var source=new SourceItem("legacy","p","clip.mov",MediaKind.Video,50);
            string id=Guid.NewGuid().ToString("N");
            using(var c=new Catalog(d))c.Save(new(id,"Copying",Path.Combine(".porto",id+".part"),"",null,0,source));
            File.WriteAllBytes(Path.Combine(d.State,id+".part"),[1,2,3]);
            Fixtures.Check(new ImportEngine(new FakeSource([],[]),d).Run(false,default).Count(Outcome.Pending)==1);
            new ImportEngine(FakeSource.One(Video(marker:"com.apple.quicktime.content.identifier")),d).Run(false,default);
            var report=new ImportEngine(new FakeSource([],[]),d).Run(false,default);
            Fixtures.Check(report.Count(Outcome.Ambiguous)==1&&report.Count(Outcome.Pending)==1);
        }));
        tests.Add(("final inventory changes trigger another automatic pass",()=>{
            using var d=new Destination(Root());int pass=0;
            var session=new ImportSession(()=>++pass==1?new ChangingSource():Several(2),d){RetryDelay=TimeSpan.Zero};
            var report=session.Run(false,default);
            Fixtures.Check(pass==2&&report.FinalInventoryChecked&&report.Status.StartsWith("Completati"),report.Status);
            Fixtures.Check(Directory.GetFiles(d.Videos).Length==2);
        }));
        tests.Add(("retry budget and user pause are bounded",()=>{
            using var d=new Destination(Root());int attempts=0;
            var session=new ImportSession(()=>{attempts++;var s=FakeSource.One();s.Reader=(_,_)=>throw new IOException("permanent");return s;},d){RetryDelay=TimeSpan.Zero};
            var report=session.Run(false,default);Fixtures.Check(attempts==3&&!report.Status.StartsWith("Completati"));
            using var cancel=new CancellationTokenSource();int again=0;
            var paused=new ImportSession(()=>{again++;var s=FakeSource.One();s.Reader=(_,_)=>throw new IOException("permanent");return s;},d){RetryDelay=TimeSpan.FromSeconds(30),Progress=p=>{if(p.Phase=="Recupero automatico")cancel.Cancel();}};
            paused.Run(false,cancel.Token);Fixtures.Check(again==1);
        }));
        tests.Add(("resume compares all prefix bytes across short source reads",()=>{
            byte[] bytes=Video();using var partial=new MemoryStream();partial.Write(bytes,0,10003);
            using var input=new ShortStream(bytes,257);var result=VerifiedCopy.Resume(input,partial,bytes.Length,null,null,default);
            Fixtures.Check(partial.ToArray().SequenceEqual(bytes));partial.Position=0;VerifiedCopy.Verify(result,VerifiedCopy.Hash(partial,default));
        }));
        tests.Add(("diverging or longer partial is unchanged and never accepted",()=>{
            byte[] bytes=Video();byte[] bad=bytes[..10003];bad[^1]^=1;
            using var partial=new MemoryStream();partial.Write(bad);
            Fixtures.Throws<PartialMismatchException>(()=>VerifiedCopy.Resume(new MemoryStream(bytes),partial,bytes.Length,null,null,default));
            Fixtures.Check(partial.ToArray().SequenceEqual(bad));
            using var longer=new MemoryStream();longer.Write(bytes);
            Fixtures.Throws<PartialMismatchException>(()=>VerifiedCopy.Resume(new MemoryStream(bytes[..100]),longer,null,null,null,default));
            Fixtures.Check(longer.ToArray().SequenceEqual(bytes));
        }));
        tests.Add(("pause during prefix verification does not append",()=>{
            byte[] bytes=Video();using var partial=new MemoryStream();partial.Write(bytes,0,10003);
            using var cancel=new CancellationTokenSource();using var input=new ShortStream(bytes,257);
            Fixtures.Throws<OperationCanceledException>(()=>VerifiedCopy.Resume(input,partial,bytes.Length,_=>cancel.Cancel(),null,cancel.Token));
            Fixtures.Check(partial.Length==10003);
        }));
        tests.Add(("repeated interruptions reuse one owned partial without deleting anything",()=>{
            byte[] bytes=Video().Concat(Atom("free",new byte[3*1024*1024])).ToArray();using var d=new Destination(Root());int pass=0;
            File.WriteAllText(Path.Combine(d.State,"foreign.part"),"untouched");
            var session=new ImportSession(()=>{var s=FakeSource.One(bytes);int current=++pass;if(current<3)s.Reader=(_,_)=>new FailingStream(bytes,current*1024*1024);return s;},d){RetryDelay=TimeSpan.Zero};
            var report=session.Run(false,default);Fixtures.Check(report.Status.StartsWith("Completati"),report.Status);
            Fixtures.Check(File.ReadAllBytes(Directory.GetFiles(d.Videos).Single()).SequenceEqual(bytes));
            Fixtures.Check(Directory.GetFiles(d.State,"*.part").Length==1&&File.ReadAllText(Path.Combine(d.State,"foreign.part"))=="untouched");
        }));
        tests.Add(("resume capacity estimate counts only remaining suffix",()=>{
            byte[] bytes=Video().Concat(Atom("free",new byte[3*1024*1024])).ToArray();using var d=new Destination(Root());
            var first=FakeSource.One(bytes);first.Reader=(_,_)=>new FailingStream(bytes,3*1024*1024);
            new ImportEngine(first,d).Run(false,default);
            var report=new ImportEngine(FakeSource.One(bytes),d,new FakeSpace(VerifiedCopy.ReserveBytes+VerifiedCopy.BufferSize+100)).Run(false,default);
            Fixtures.Check(report.Count(Outcome.Imported)==1,report.Status);
        }));
        tests.Add(("offline size does not prevent accessible video copy",()=>{
            using var d=new Destination(Root());byte[] bytes=Video();var source=new FakeSource([new("cloud","p","cloud.mov",MediaKind.Video,1L<<40,Offline:true),new("local","p","local.mov",MediaKind.Video,bytes.Length)],new(){{"local",bytes}});
            var report=new ImportEngine(source,d,new FakeSpace(VerifiedCopy.ReserveBytes+2*VerifiedCopy.BufferSize)).Run(false,default);
            Fixtures.Check(report.Count(Outcome.Imported)==1&&report.Count(Outcome.Missing)==1);
        }));
        tests.Add(("three unreadable videos do not starve following healthy items",()=>{
            using var d=new Destination(Root());var session=new ImportSession(()=>{var s=Several(5);s.Reader=(item,_)=>int.Parse(item.Id)<3?throw new IOException("unreadable item"):new MemoryStream(Video(int.Parse(item.Id)));return s;},d){RetryDelay=TimeSpan.Zero};
            var report=session.Run(false,default);Fixtures.Check(Directory.GetFiles(d.Videos).Length==2&&report.Count(Outcome.Error)==3&&report.Count(Outcome.Pending)==0);
        }));
        tests.Add(("claiming a prior copy cannot erase another current duplicate obligation",()=>{
            using var d=new Destination(Root());byte[] bytes=Video();var a=new SourceItem("a","p","same.mov",MediaKind.Video,bytes.Length);var b=a with{Id="b"};
            new ImportEngine(new FakeSource([a],new(){{"a",bytes}}),d).Run(false,default);
            using var cancel=new CancellationTokenSource();
            var two=new FakeSource([b,a],new(){{"a",bytes},{"b",bytes}});
            var interrupted=new ImportEngine(two,d){Progress=p=>{if(p.Phase=="Importazione"&&two.Reads>0)cancel.Cancel();}}.Run(false,cancel.Token);
            Fixtures.Check(interrupted.Count(Outcome.Pending)>=1);
            var later=new ImportEngine(new FakeSource([b],new(){{"b",bytes}}),d).Run(false,default);
            Fixtures.Check(later.Status.StartsWith("Completati")?Directory.GetFiles(d.Videos).Length==2:later.Count(Outcome.Pending)>=1,later.Status);
        }));
    }
    internal static FakeSource Several(int count)
    {
        var data=Enumerable.Range(0,count).ToDictionary(i=>i.ToString(),i=>Video(i));
        return new(data.Select(p=>new SourceItem(p.Key,"p",p.Key+".mov",MediaKind.Video,p.Value.Length)).ToList(),data);
    }
    private sealed class ChangingSource:IVideoSource
    {
        private int calls;
        public Inventory Inventory(CancellationToken cancel)=>Several(++calls==1?1:2).Inventory(cancel);
        public Stream OpenRead(SourceItem item,CancellationToken cancel)=>new MemoryStream(Video(int.Parse(item.Id)));
        public void Dispose(){}
    }
    private sealed class ShortStream(byte[] bytes,int chunk):MemoryStream(bytes)
    {public override int Read(byte[] buffer,int offset,int count)=>base.Read(buffer,offset,Math.Min(count,chunk));}
    private sealed class FailingStream(byte[] bytes,int limit):MemoryStream(bytes)
    {public override int Read(byte[] buffer,int offset,int count){if(Position>=limit)throw new IOException("synthetic disconnect");return base.Read(buffer,offset,(int)Math.Min(count,limit-Position));}}
}
