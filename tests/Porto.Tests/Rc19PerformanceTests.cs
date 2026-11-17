using System.Diagnostics;
using Porto.Core;

internal static class Rc19PerformanceTests
{
    public static void Add(List<(string,Action)> tests)
    {
        tests.Add(("rc19 comparison replays unmatched content without second source read",()=>
        {
            using var d=Seed();var prior=Preserved(d);using var source=Gallery.One(1902);
            var report=new ImportEngine(source,d).Run(false,default);
            Fixtures.Check(source.Inner.Reads==1&&report.GallerySelectionCopiesVerified&&report.CurrentCount(Outcome.Imported)==1);
            AssertPreserved(prior);NoReplay(d);
        }));
        tests.Add(("rc19 comparison matching old copy preserves multiplicity and same names",()=>
        {
            using var d=Seed();byte[] bytes=Fixtures.Video(1901);var prior=Preserved(d);
            using var source=new Gallery(new FakeSource([new("a","album","same.mov",MediaKind.Video,bytes.Length),new("b","album","same.mov",MediaKind.Video,bytes.Length)],new(){{"a",bytes},{"b",bytes}}));
            var report=new ImportEngine(source,d).Run(false,default);
            Fixtures.Check(report.GallerySelectionCopiesVerified&&report.CurrentCount(Outcome.Imported)==1&&report.CurrentCount(Outcome.AlreadyPresent)==1);
            Fixtures.Check(Directory.GetFiles(d.Videos).Length==2&&source.Inner.Reads==2);AssertPreserved(prior);NoReplay(d);
        }));
        tests.Add(("rc19 comparison low space retains hash-only reuse without temporary writes",()=>
        {
            using var d=Seed();var prior=Preserved(d);using var source=Gallery.One(1901);
            var report=new ImportEngine(source,d,new FakeSpace(VerifiedCopy.ReserveBytes)).Run(false,default);
            Fixtures.Check(report.GallerySelectionCopiesVerified&&report.CurrentCount(Outcome.AlreadyPresent)==1&&source.Inner.Reads==1);
            AssertPreserved(prior);NoReplay(d);
        }));
        tests.Add(("rc19 comparison deep mode still detects discordant independent source reads",()=>
        {
            using var d=Seed();using var source=Gallery.One(1902);
            source.Inner.Reader=(_,n)=>new MemoryStream(Fixtures.Video(n==1?1902:1903));
            var report=new ImportEngine(source,d).Run(true,default);
            Fixtures.Check(source.Inner.Reads==2&&!report.GallerySelectionCopiesVerified&&report.CurrentCount(Outcome.Error)>0);
            Fixtures.Check(Directory.GetFiles(d.Videos).Length==1);NoReplay(d);
        }));
        tests.Add(("rc19 comparison cancellation discards only newly owned replay and resumes",()=>
        {
            using var d=Seed();var prior=Preserved(d);using var cancel=new CancellationTokenSource();
            using(var source=Gallery.One(1902))
            {
                var report=new ImportEngine(source,d){FaultPoint=p=>{if(p=="after-comparison")cancel.Cancel();}}.Run(false,cancel.Token);
                Fixtures.Check(!report.GallerySelectionCopiesVerified&&report.CurrentCount(Outcome.Pending)>0);
            }
            NoReplay(d);AssertPreserved(prior);
            using var next=Gallery.One(1902);var resumed=new ImportEngine(next,d).Run(false,default);
            Fixtures.Check(resumed.GallerySelectionCopiesVerified&&resumed.CurrentCount(Outcome.Imported)==1);NoReplay(d);
        }));
        tests.Add(("rc19 comparison rejects truncated replay source before publishing",()=>
        {
            using var d=Seed();using var source=Gallery.One(1902);var prior=Preserved(d);
            source.Inner.Reader=(_,_)=>new MemoryStream(Fixtures.Video(1902)[..^1]);
            var report=new ImportEngine(source,d).Run(false,default);
            Fixtures.Check(!report.GallerySelectionCopiesVerified&&report.CurrentCount(Outcome.Error)>0);
            AssertPreserved(prior);Fixtures.Check(Directory.GetFiles(d.Videos).Length==1);NoReplay(d);
        }));
        foreach(string phase in new[]{"after-comparison","after-intent","after-flush","after-ready","after-rename"})
        tests.Add(($"rc19 comparison process death at {phase} recovers with old files unchanged",()=>
        {
            string root;Dictionary<string,byte[]> prior;
            using(var d=Seed()){root=d.Root;prior=Preserved(d);}
            var info=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true};
            foreach(var arg in new[]{"--rc19-crash",root,phase})info.ArgumentList.Add(arg);
            using(var child=Process.Start(info)!){Fixtures.Check(child.WaitForExit(20000),"child timeout");Fixtures.Check(child.ExitCode==77);}
            using var recovered=new Destination(root);NoReplay(recovered);AssertPreserved(prior);
            using var source=Gallery.One(1902);var report=new ImportEngine(source,recovered).Run(false,default);
            Fixtures.Check(report.GallerySelectionCopiesVerified&&Directory.GetFiles(recovered.Videos).Length==2);
            AssertPreserved(prior);NoReplay(recovered);
        }));
    }
    public static int CrashChild(string root,string phase)
    {
        using var d=new Destination(root);using var source=Gallery.One(1902);
        new ImportEngine(source,d){FaultPoint=p=>{if(p==phase)Environment.Exit(77);}}.Run(false,default);return 2;
    }
    private static Destination Seed()
    {
        var d=new Destination(Fixtures.Root());using var source=Gallery.One(1901);
        Fixtures.Check(new ImportEngine(source,d).Run(false,default).GallerySelectionCopiesVerified);return d;
    }
    private static Dictionary<string,byte[]> Preserved(Destination d)=>Directory.GetFiles(d.Videos).ToDictionary(p=>p,File.ReadAllBytes);
    private static void AssertPreserved(Dictionary<string,byte[]> prior)
    {foreach(var (path,bytes) in prior)Fixtures.Check(File.ReadAllBytes(path).SequenceEqual(bytes));}
    private static void NoReplay(Destination d)=>Fixtures.Check(Directory.GetFiles(d.State,"comparison-*.tmp").Length==0);
    private sealed class Gallery(FakeSource inner):IGallerySource
    {
        public FakeSource Inner=>inner;
        public string SourceDescription=>"Synthetic rc19 gallery";public bool AppleResourceHints=>false;
        public GalleryCoverage? Coverage=>new("Synthetic",0,1,0,0,0);
        public Inventory Inventory(CancellationToken token)=>inner.Inventory(token);
        public Stream OpenRead(SourceItem item,CancellationToken token)=>inner.OpenRead(item,token);
        public void Dispose()=>inner.Dispose();
        public static Gallery One(int seed)=>new(FakeSource.One(Fixtures.Video(seed),id:seed.ToString(),name:"same.mov"));
    }
}
