using Porto.Core;

internal static class Rc6ClassificationTests
{
    public static void Add(List<(string,Action)> tests)
    {
        foreach(var sample in new[]{(Name:"IMG_0010.MOV",Photo:true,Edited:false),
            (Name:"IMG_E0010.MOV",Photo:false,Edited:true),(Name:"IMG_E0010.MOV",Photo:true,Edited:true)})
            tests.Add(($"rc8 copied additional resource report explains photo pairing and edited-name evidence: {sample.Photo}/{sample.Edited}",()=>
            {
                byte[] video=Fixtures.Video();
                List<SourceItem> items=[new("video","folder",sample.Name,MediaKind.Video,video.Length)];
                if(sample.Photo)items.Add(new("photo","folder",Path.ChangeExtension(sample.Name,".JPG"),MediaKind.Image,50));
                var source=new FakeSource(items,new(){{"video",video}});
                using var destination=new Destination(Fixtures.Root());
                var report=new ImportEngine(source,destination).Run(false,default);
                var result=report.Items.Single(i=>i.SourceId=="video");
                Fixtures.Check(result.Outcome==Outcome.Additional&&source.Reads==1&&Directory.GetFiles(destination.AdditionalVideos).Length==1&&Directory.GetFiles(destination.Videos).Length==0);
                Fixtures.Check(result.Detail.Contains("foto omonima",StringComparison.OrdinalIgnoreCase)==sample.Photo,"The report must distinguish a photo-pair heuristic from an edited name.");
                Fixtures.Check(result.Detail.Contains("IMG_E",StringComparison.Ordinal)==sample.Edited,"Edited-name evidence must be explicit, without claiming it proves a Live Photo.");
                Fixtures.Check(!report.Status.StartsWith("Completati",StringComparison.Ordinal));
            }));
    }
}
