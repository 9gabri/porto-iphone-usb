using System.Security.Cryptography;
using Porto.Core;

internal static class EncodedMediaTests
{
    public static void Add(List<(string,Action)> tests)
    {
        foreach(string name in new[]{"synthetic-h264.mov","synthetic-h264.m4v","synthetic-hevc.mp4"})
            tests.Add(("encoded synthetic media is copied byte for byte: "+name,()=>
            {
                string fixture=Path.Combine(AppContext.BaseDirectory,"Fixtures",name);
                byte[] bytes=File.ReadAllBytes(fixture);
                Fixtures.Check(MediaInspector.Inspect(fixture,default) is {IsVideo:true,Ambiguous:false});
                using var destination=new Destination(Fixtures.Root());
                var report=new ImportEngine(FakeSource.One(bytes,name:name),destination).Run(true,default);
                Fixtures.Check(report.Count(Outcome.Imported)==1&&report.Status.StartsWith("Completati"),report.Status);
                string saved=Directory.GetFiles(destination.Videos).Single();
                Fixtures.Check(SHA256.HashData(File.ReadAllBytes(saved)).SequenceEqual(SHA256.HashData(bytes)));
            }));
    }
}
