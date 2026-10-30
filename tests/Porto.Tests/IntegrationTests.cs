using System.Diagnostics;
using System.Reflection;
using Porto.Core;
using Porto.Usb;
using static Fixtures;

internal static class IntegrationTests
{
    public static void Add(List<(string,Action)> tests)
    {
        tests.Add(("real SQLite roundtrip, copy, changed IDs and verified resume",()=>{
            string root=Root();using var d=new Destination(root);
            var a=new ImportEngine(FakeSource.One(),d).Run(false,default);Fixtures.Check(a.Count(Outcome.Imported)==1,a.Status);
            var b=new ImportEngine(FakeSource.One(id:"changed"),d).Run(false,default);Fixtures.Check(b.Count(Outcome.AlreadyPresent)==1,b.Status);
            Fixtures.Check(Directory.GetFiles(d.Videos).Length==1);
        }));
        tests.Add(("source duplicate multiplicity is preserved across ID changes",()=>{
            byte[] video=Video();string root=Root();using var d=new Destination(root);
            FakeSource Make(string prefix)=>new(Enumerable.Range(0,3).Select(i=>new SourceItem(prefix+i,"p","same.MOV",MediaKind.Video,video.Length)).ToList(),Enumerable.Range(0,3).ToDictionary(i=>prefix+i,_=>video));
            var a=new ImportEngine(Make("a"),d).Run(false,default);Fixtures.Check(a.Count(Outcome.Imported)==3);
            var b=new ImportEngine(Make("b"),d).Run(false,default);Fixtures.Check(b.Count(Outcome.AlreadyPresent)==3);Fixtures.Check(Directory.GetFiles(d.Videos).Length==3);
        }));
        tests.Add(("source filename collisions do not overwrite",()=>{
            using var d=new Destination(Root());var data=new Dictionary<string,byte[]>{{"1",Video(1)},{"2",Video(2)}};
            var source=new FakeSource(data.Select(x=>new SourceItem(x.Key,"p","same.mov",MediaKind.Video,x.Value.Length)).ToList(),data);
            Fixtures.Check(new ImportEngine(source,d).Run(false,default).Count(Outcome.Imported)==2);Fixtures.Check(Directory.GetFiles(d.Videos).Length==2);
        }));
        tests.Add(("missing completed file gets recopied",()=>{
            using var d=new Destination(Root());new ImportEngine(FakeSource.One(),d).Run(false,default);
            string final=Directory.GetFiles(d.Videos).Single();File.Move(final,final+".user-moved");
            var r=new ImportEngine(FakeSource.One(),d).Run(false,default);Fixtures.Check(r.Count(Outcome.Imported)==1);Fixtures.Check(File.Exists(final+".user-moved"));
        }));
        tests.Add(("corrupt completed copy is preserved and reported",()=>{
            using var d=new Destination(Root());new ImportEngine(FakeSource.One(),d).Run(false,default);
            string final=Directory.GetFiles(d.Videos).Single();File.WriteAllText(final,"synthetic corruption");
            var r=new ImportEngine(FakeSource.One(),d).Run(false,default);Fixtures.Check(r.Count(Outcome.Imported)==1);Fixtures.Check(r.Count(Outcome.Error)==1);Fixtures.Check(File.ReadAllText(final)=="synthetic corruption");
        }));
        tests.Add(("truncation reports error and no final filename",()=>{
            using var d=new Destination(Root());var r=new ImportEngine(FakeSource.One(size:999999),d).Run(false,default);
            Fixtures.Check(r.Count(Outcome.Error)==1);Fixtures.Check(Directory.GetFiles(d.Videos).Length==0);Fixtures.Check(r.Status.Contains("pausa"));
        }));
        tests.Add(("deep source corruption prevents publication",()=>{
            using var d=new Destination(Root());var s=FakeSource.One();int reads=0;s.Reader=(_,_)=>new MemoryStream(Video(++reads));
            var r=new ImportEngine(s,d).Run(true,default);Fixtures.Check(r.Count(Outcome.Error)==1);Fixtures.Check(Directory.GetFiles(d.Videos).Length==0);
        }));
        tests.Add(("disk full before transfer pauses without reading",()=>{
            using var d=new Destination(Root());var s=FakeSource.One();s.Reader=(_,_)=>throw new Exception("Should not read");
            var r=new ImportEngine(s,d,new FakeSpace(0)).Run(false,default);Fixtures.Check(r.Status.Contains("pausa"));Fixtures.Check(Directory.GetFiles(d.Videos).Length==0);
        }));
        tests.Add(("disk fills during copy and stops safely",()=>{
            using var d=new Destination(Root());var space=new FakeSpace(1L<<40);var source=FakeSource.One();
            source.Reader=(_,_)=>new SpaceFillingStream(Video(),space);var r=new ImportEngine(source,d,space).Run(false,default);
            Fixtures.Check(r.Count(Outcome.Error)==1);Fixtures.Check(Directory.GetFiles(d.Videos).Length==0);
        }));
        tests.Add(("read disconnect then reconnect",()=>{
            using var d=new Destination(Root());var s=FakeSource.One();s.Reader=(_,_)=>throw new IOException("Synthetic disconnect");
            Fixtures.Check(new ImportEngine(s,d).Run(false,default).Count(Outcome.Error)==1);
            Fixtures.Check(new ImportEngine(FakeSource.One(id:"reconnected"),d).Run(false,default).Count(Outcome.Imported)==1);
        }));
        tests.Add(("malicious names and catalog path escape rejected",()=>{
            using var d=new Destination(Root());Fixtures.Throws<IOException>(()=>d.Resolve("../escape"));Fixtures.Throws<IOException>(()=>d.Resolve(@"C:\escape"));
            var r=new ImportEngine(FakeSource.One(name:@"..\..\CON:evil.MOV"),d).Run(false,default);Fixtures.Check(r.Count(Outcome.Imported)==1);
            Fixtures.Check(Directory.GetFiles(d.Videos).All(p=>Path.GetFileName(p).StartsWith("V_")));
        }));
        tests.Add(("concurrent importer is excluded",()=>{string root=Root();using var d=new Destination(root);Fixtures.Throws<IOException>(()=>{using var second=new Destination(root);});}));
        tests.Add(("live photo hint and edited variants are ambiguous, not success",()=>{
            using var d=new Destination(Root());var r=new ImportEngine(FakeSource.One(Video(marker:"com.apple.quicktime.still-image-time")),d).Run(false,default);
            Fixtures.Check(r.Count(Outcome.Ambiguous)==1);Fixtures.Check(r.Status.Contains("incompleta"));Fixtures.Check(Directory.GetFiles(d.Videos).Length==0);
            var edited=new ImportEngine(FakeSource.One(name:"IMG_E1234.MOV"),d).Run(false,default);Fixtures.Check(edited.Count(Outcome.Ambiguous)==1);
        }));
        tests.Add(("paired photo ambiguity, photos excluded",()=>{
            byte[] video=Video();using var d=new Destination(Root());
            var s=new FakeSource([new("v","p","IMG_0001.MOV",MediaKind.Video,video.Length),new("p","p","IMG_0001.HEIC",MediaKind.Image,100)],new(){{"v",video}});
            var r=new ImportEngine(s,d).Run(false,default);Fixtures.Check(r.Count(Outcome.Ambiguous)==1);Fixtures.Check(r.Count(Outcome.Excluded)==1);
        }));
        tests.Add(("MOV MP4 M4V containers accepted; extension alone rejected",()=>{
            foreach(string ext in new[]{"MOV","MP4","M4V"}){using var d=new Destination(Root());Fixtures.Check(new ImportEngine(FakeSource.One(name:"clip."+ext),d).Run(false,default).Count(Outcome.Imported)==1);}
            using var bad=new Destination(Root());Fixtures.Check(new ImportEngine(FakeSource.One([1,2,3,4],name:"fake.mov"),bad).Run(false,default).Count(Outcome.Ambiguous)==1);
        }));
        tests.Add(("incomplete inventory and offline missing never claim full library",()=>{
            using var d=new Destination(Root());var s=FakeSource.One();s.Errors.Add("Synthetic inaccessible folder");var r=new ImportEngine(s,d).Run(false,default);
            Fixtures.Check(!r.UsbEnumerationComplete&&!r.EntireLibraryVerified&&r.Status.Contains("incompleta"));
            var unavailable=new FakeSource([new("v","p","cloud.mov",MediaKind.Video,100,Offline:true)],[]);
            Fixtures.Check(new ImportEngine(unavailable,d).Run(false,default).Count(Outcome.Missing)==1);
        }));
        foreach(string phase in new[]{"after-intent","after-flush","after-verification","after-ready","after-rename","after-complete"})
            tests.Add(($"process termination and recovery at {phase}",()=>CrashRecovery(phase)));
        tests.Add(("crash Ready collision preserves foreign file and recovers",()=>{
            string root=Root();Crash(root,"after-ready");using var d=new Destination(root);
            string target;using(var c=new Catalog(d)){target=d.Resolve(c.Entries().Single().Final);}File.WriteAllText(target,"foreign synthetic data");
            var r=new ImportEngine(FakeSource.One(),d).Run(false,default);Fixtures.Check(r.Count(Outcome.AlreadyPresent)==1);Fixtures.Check(File.ReadAllText(target)=="foreign synthetic data");
        }));
        tests.Add(("crash Ready temporary corruption can be recopied",()=>{
            string root=Root();Crash(root,"after-ready");using var d=new Destination(root);
            using(var c=new Catalog(d)){File.WriteAllText(d.Resolve(c.Entries().Single().Temp),"corrupt synthetic temp");}
            Fixtures.Check(new ImportEngine(FakeSource.One(),d).Run(false,default).Count(Outcome.Imported)==1);
        }));
        tests.Add(("COM ABI and class registration, no device enumeration",WpdSource.SmokeWithoutDevices));
        tests.Add(("unresponsive worker times out and is terminated",()=>{
            var timer=Stopwatch.StartNew();using var worker=new WorkerClient(Environment.ProcessPath!,"--hang"){Timeout=TimeSpan.FromMilliseconds(300)};
            Fixtures.Throws<IOException>(()=>worker.Devices(default));Fixtures.Check(timer.Elapsed<TimeSpan.FromSeconds(5));
        }));
        tests.Add(("paused worker cancels pending read and is terminated",()=>{
            using var cancel=new CancellationTokenSource(200);using var worker=new WorkerClient(Environment.ProcessPath!,"--hang");
            Fixtures.Throws<OperationCanceledException>(()=>worker.Devices(cancel.Token));
        }));
        tests.Add(("partial IPC payload timeout terminates worker",()=>{
            using var worker=new WorkerClient(Environment.ProcessPath!,"--partial"){Timeout=TimeSpan.FromMilliseconds(300)};
            Fixtures.Throws<IOException>(()=>worker.Devices(default));
        }));
        tests.Add(("ambiguous verified files are reused without duplicate temporary copies",()=>{
            using var d=new Destination(Root());byte[] data=Video(marker:"com.apple.quicktime.content.identifier");
            Fixtures.Check(new ImportEngine(FakeSource.One(data),d).Run(false,default).Count(Outcome.Ambiguous)==1);
            Fixtures.Check(new ImportEngine(FakeSource.One(data,id:"changed"),d).Run(false,default).Count(Outcome.Ambiguous)==1);
            Fixtures.Check(Directory.GetFiles(d.State,"*.part").Length==1);
        }));
        tests.Add(("repeated and unbounded failing enumeration is stopped",()=>{
            var repeated=new EnumerationGuard(10,2);repeated.Visit("id");Fixtures.Throws<EnumerationStoppedException>(()=>repeated.Visit("id"));
            var failed=new EnumerationGuard(2,10);failed.Visit("1");failed.Visit("2");Fixtures.Throws<EnumerationStoppedException>(()=>failed.Visit("3"));
            var errors=new EnumerationGuard(10,1);errors.Error();Fixtures.Throws<EnumerationStoppedException>(errors.Error);
        }));
        tests.Add(("missing copies remain visible across empty inventories",()=>{
            using var d=new Destination(Root());new ImportEngine(FakeSource.One(),d).Run(false,default);
            var path=Directory.GetFiles(d.Videos).Single();File.Move(path,path+".synthetic-moved");
            for(int i=0;i<2;i++){var r=new ImportEngine(new FakeSource([],[]),d).Run(false,default);Fixtures.Check(r.Count(Outcome.Missing)==1);Fixtures.Check(r.Status.Contains("incompleta"));}
            var repaired=new ImportEngine(FakeSource.One(),d).Run(false,default);Fixtures.Check(repaired.Count(Outcome.Imported)==1&&repaired.Count(Outcome.Missing)==0);
            Fixtures.Check(new ImportEngine(FakeSource.One(),d).Run(false,default).Count(Outcome.Missing)==0);
        }));
        tests.Add(("Ready without either file is reported missing immediately",()=>{
            string root=Root();Crash(root,"after-ready");using var d=new Destination(root);
            using(var c=new Catalog(d)){string path=d.Resolve(c.Entries().Single().Temp);File.Move(path,path+".synthetic-moved");}
            var r=new ImportEngine(new FakeSource([],[]),d).Run(false,default);Fixtures.Check(r.Count(Outcome.Missing)==1);
        }));
        foreach(string repairPhase in new[]{"after-ready","after-rename","after-complete"})
        tests.Add(($"crash during repair at {repairPhase} keeps missing resolution atomic",()=>{
            string root=Root();using(var d=new Destination(root)){new ImportEngine(FakeSource.One(),d).Run(false,default);var path=Directory.GetFiles(d.Videos).Single();File.Move(path,path+".synthetic-moved");}
            Crash(root,repairPhase);using var recovered=new Destination(root);var r=new ImportEngine(FakeSource.One(),recovered).Run(false,default);
            Fixtures.Check(r.Count(Outcome.Missing)==0&&r.Count(Outcome.AlreadyPresent)==1);
        }));
        foreach(bool corrupt in new[]{false,true})
        tests.Add(($"failed replacement attempt does not duplicate missing debt (corrupt={corrupt})",()=>{
            string root=Root();using(var d=new Destination(root)){new ImportEngine(FakeSource.One(),d).Run(false,default);string old=Directory.GetFiles(d.Videos).Single();File.Move(old,old+".synthetic-moved");}
            Crash(root,"after-ready");using var recovered=new Destination(root);
            using(var catalog=new Catalog(recovered))
            {string part=recovered.Resolve(catalog.Entries().Single(e=>e.State=="Ready").Temp);if(corrupt)File.WriteAllText(part,"synthetic corruption");else File.Move(part,part+".synthetic-moved");}
            var r=new ImportEngine(FakeSource.One(),recovered).Run(false,default);Fixtures.Check(r.Count(Outcome.Missing)==0);Fixtures.Check(r.Count(Outcome.Imported)==1);
            Fixtures.Check(Directory.GetFiles(recovered.Videos,"*.mov").Length==1);
        }));
    }
    public static int CrashChild(string root,string phase)
    {using var d=new Destination(root);new ImportEngine(FakeSource.One(),d){FaultPoint=p=>{if(p==phase)Environment.Exit(77);}}.Run(false,default);return 2;}
    private static void Crash(string root,string phase)
    {
        var info=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true};
        if(Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet",StringComparison.OrdinalIgnoreCase))info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        foreach(string arg in new[]{"--crash",root,phase})info.ArgumentList.Add(arg);
        using var child=Process.Start(info)!;Fixtures.Check(child.WaitForExit(15000),"Crash child timeout");Fixtures.Check(child.ExitCode==77,"Crash child exit "+child.ExitCode);
    }
    private static void CrashRecovery(string phase)
    {string root=Root();Crash(root,phase);using var d=new Destination(root);var r=new ImportEngine(FakeSource.One(id:"new-id"),d).Run(false,default);Fixtures.Check(r.Count(Outcome.Imported)+r.Count(Outcome.AlreadyPresent)==1,r.Status);Fixtures.Check(Directory.GetFiles(d.Videos).Length==1);}
    private sealed class SpaceFillingStream(byte[] data,FakeSpace space):MemoryStream(data)
    {public override int Read(byte[] buffer,int offset,int count){int n=base.Read(buffer,offset,count);space.Bytes=0;return n;}}
}

