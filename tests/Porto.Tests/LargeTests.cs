using System.Diagnostics;
using Porto.Core;
using static Fixtures;

internal static class LargeTests
{
    public static void Add(List<(string,Action)> tests)
    {
        tests.Add(("stream beyond 4 GiB: 4 GiB + 17 bytes actually read and hashed without disk allocation",()=>{
            long length=(1L<<32)+17;var timer=Stopwatch.StartNew();using var stream=new VirtualStream(length);
            var result=VerifiedCopy.Copy(stream,Stream.Null,length,null,null,default);Fixtures.Check(result.Length==length);Console.WriteLine($"  LARGE stream: {result.Length} bytes, {timer.Elapsed.TotalSeconds:F2}s, working set {Environment.WorkingSet/(1024*1024)} MiB");
        }));
        tests.Add(("50000-item inventory and 250 GB logical capacity without writing payload",()=>{
            var items=Enumerable.Range(0,50000).Select(i=>new SourceItem(i.ToString(),"p","video"+i+".mov",MediaKind.Video,5_000_000)).ToList();
            Fixtures.Check(items.Sum(i=>i.Size!.Value)==250_000_000_000L);using var d=new Destination(Root());var s=new FakeSource(items,[]);
            s.Reader=(_,_)=>throw new Exception("No data should be read in capacity simulation");var timer=Stopwatch.StartNew();
            var r=new ImportEngine(s,d,new FakeSpace(240_000_000_000)).Run(false,default);Fixtures.Check(r.Status.Contains("pausa"));
            Console.WriteLine($"  LARGE inventory: {items.Count} items, 250 GB logical, {timer.Elapsed.TotalSeconds:F2}s, working set {Environment.WorkingSet/(1024*1024)} MiB");
        }));
    }
}

