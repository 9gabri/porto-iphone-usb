using Porto.Core;

internal static class Rc6ClassificationTests
{
    public static void Add(List<(string,Action)> tests)
    {
        foreach(var sample in new[]{(Name:"IMG_0010.MOV",Photo:true,Edited:false),
            (Name:"IMG_E0010.MOV",Photo:false,Edited:true),(Name:"IMG_E0010.MOV",Photo:true,Edited:true)})
            tests.Add(($"rc6 ambiguous report explains photo pairing and edited-name evidence: {sample.Photo}/{sample.Edited}",()=>
            {
                List<SourceItem> items=[new("video","folder",sample.Name,MediaKind.Video,100)];
                if(sample.Photo)items.Add(new("photo","folder",Path.ChangeExtension(sample.Name,".JPG"),MediaKind.Image,50));
                var source=new FakeSource(items,[]){Reader=(_,_)=>throw new Exception("Do not read personal-like ambiguous media in a classification test.")};
                using var destination=new Destination(Fixtures.Root());
                var report=new ImportEngine(source,destination).Run(false,default);
                var result=report.Items.Single(i=>i.SourceId=="video");
                Fixtures.Check(result.Outcome==Outcome.Ambiguous&&source.Reads==0&&Directory.GetFiles(destination.Videos).Length==0);
                Fixtures.Check(result.Detail.Contains("foto omonima",StringComparison.OrdinalIgnoreCase)==sample.Photo,"The report must distinguish a photo-pair heuristic from an edited name.");
                Fixtures.Check(result.Detail.Contains("IMG_E",StringComparison.Ordinal)==sample.Edited,"Edited-name evidence must be explicit, without claiming it proves a Live Photo.");
                Fixtures.Check(!report.Status.StartsWith("Completati",StringComparison.Ordinal));
            }));
    }
}
