using Porto.Core;
using static Fixtures;

internal static class Rc5ResourceTests
{
    public static void Add(List<(string,Action)> tests)
    {
        foreach(int delta in new[]{-31,31})tests.Add(($"rc5 resource size overrides stale object {delta} only with two matching reads",()=>{
            byte[] video=Video();using var destination=new Destination(Root());
            var source=FakeSource.One(video,size:video.Length+delta);
            source.Reader=(_,_)=>new ResourceStream(video,video.Length);
            var report=new ImportEngine(source,destination).Run(false,default);
            Fixtures.Check(report.Count(Outcome.Imported)==1&&source.Reads==2,report.Status);
            Fixtures.Check(report.Items.Single(i=>i.Outcome==Outcome.Imported).Detail.Contains("inventario",StringComparison.OrdinalIgnoreCase));
            Fixtures.Check(File.ReadAllBytes(Directory.GetFiles(destination.Videos).Single()).SequenceEqual(video));
        }));
        foreach(string failure in new[]{"truncated","extra","second-size","second-content","missing-size"})tests.Add(($"rc5 resource rejects {failure} without publication",()=>{
            byte[] video=Video();using var destination=new Destination(Root());
            var source=FakeSource.One(video,size:video.Length+31);
            source.Reader=(_,read)=>new ResourceStream(failure=="second-content"&&read==2?Video(2):video,
                failure=="missing-size"?null:video.Length+(failure=="truncated"||failure=="second-size"&&read==2?1:failure=="extra"?-1:0));
            var report=new ImportEngine(source,destination).Run(false,default);
            Fixtures.Check(report.Count(Outcome.Error)>0&&Directory.GetFiles(destination.Videos).Length==0);
            Fixtures.Check(!report.Status.StartsWith("Completati"));
        }));
        foreach(int delta in new[]{-31,31})tests.Add(($"rc5 resource repeated stale inventory {delta} reuses one verified copy",()=>{
            byte[] video=Video();using var destination=new Destination(Root());
            for(int pass=0;pass<2;pass++)
            {
                var source=FakeSource.One(video,size:video.Length+delta);
                source.Reader=(_,_)=>new ResourceStream(video,video.Length);
                var report=new ImportEngine(source,destination).Run(false,default);
                Fixtures.Check(report.Count(pass==0?Outcome.Imported:Outcome.AlreadyPresent)==1,report.Status);
                Fixtures.Check(report.Count(Outcome.Error)==0&&source.Reads==2,"Each pass needs two current complete source reads.");
                Fixtures.Check(Directory.GetFiles(destination.Videos).Length==1,"Stale object size must not bypass the existing-copy comparison.");
                Fixtures.Check(File.ReadAllBytes(Directory.GetFiles(destination.Videos).Single()).SequenceEqual(video));
            }
        }));
        tests.Add(("rc5 resource reuses verified copy with two current reads when object size changes",()=>{
            byte[] video=Video();using var destination=new Destination(Root());
            new ImportEngine(FakeSource.One(video),destination).Run(false,default);
            // Object size equals an existing length to exercise the pre-copy fingerprint path.
            var source=FakeSource.One(video);
            source.Reader=(_,read)=>new ResourceStream(video,video.Length+(read==2?1:0));
            var report=new ImportEngine(source,destination).Run(true,default);
            Fixtures.Check(report.Count(Outcome.AlreadyPresent)==0&&report.Count(Outcome.Error)>0);
            Fixtures.Check(Directory.GetFiles(destination.Videos).Length==1);
        }));
        tests.Add(("rc5 resource resumes a synthetic partial without overwriting its prefix",()=>{
            byte[] video=Video();using var destination=new Destination(Root());
            var source=FakeSource.One(video,size:video.Length+31);
            var item=source.Inventory(default).Items.Single();string id=Guid.NewGuid().ToString("N");
            string relative=Path.Combine(".porto",id+".part");
            using(var catalog=new Catalog(destination))catalog.Save(new(id,"Copying",relative,"",null,0,item));
            File.WriteAllBytes(destination.Resolve(relative),video[..200]);
            source.Reader=(_,_)=>new ResourceStream(video,video.Length);
            var report=new ImportEngine(source,destination).Run(false,default);
            Fixtures.Check(report.Count(Outcome.Imported)==1&&source.Reads==2);
            Fixtures.Check(File.ReadAllBytes(Directory.GetFiles(destination.Videos).Single()).SequenceEqual(video));
        }));
    }
    private sealed class ResourceStream(byte[] data,long? length):MemoryStream(data,false),ISourceStreamInfo
    {public long? ResourceLength=>length;}
}
