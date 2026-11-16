using Porto.Core;

internal static class GalleryImportTests
{
    public static void Add(List<(string,Action)> tests)
    {
        tests.Add(("gallery import unknown asset roles never become videos from filename extensions",()=>
        {
            using var destination=new Destination(Fixtures.Root());
            var inner=new FakeSource([new("unknown","gallery","maybe-live.mov",MediaKind.Unknown),new("unknown-aae","gallery","metadata.aae",MediaKind.Unknown)],[]);
            using var source=new Selection(inner);var report=new ImportEngine(source,destination).Run(false,default);
            Fixtures.Check(inner.Reads==0&&!report.GallerySelectionCopiesVerified&&report.CurrentCount(Outcome.Ambiguous)==2);
            Fixtures.Check(report.CurrentCount(Outcome.Imported)==0&&report.CurrentCount(Outcome.Excluded)==0);
        }));
        tests.Add(("gallery import selects true video and preserves old Live Photo copies",()=>
        {
            using var destination=new Destination(Fixtures.Root());var video=Fixtures.Video(501);var extra=Rc8MediaTests.TimedVideo();
            new ImportEngine(new FakeSource([new("old","raw","clip.mov",MediaKind.Video,video.Length),new("live","raw","IMG_E9999.mov",MediaKind.Video,extra.Length)],new(){{"old",video},{"live",extra}}),destination).Run(false,default);
            var before=Directory.GetFiles(destination.Root,"*.mov",SearchOption.AllDirectories).ToDictionary(path=>path,File.ReadAllBytes);
            using var source=new Selection(new FakeSource([new("new","gallery","renamed.mov",MediaKind.Video,video.Length),new("still","gallery","IMG_9999.heic",MediaKind.Image)],new(){{"new",video}}));
            var report=new ImportEngine(source,destination){Media=ImportMedia.Videos}.Run(false,default);
            Fixtures.Check(report.SelectedGallery&&!report.SelectedFolder&&report.GallerySelectionCopiesVerified);
            Fixtures.Check(report.CurrentCount(Outcome.AlreadyPresent)==1&&report.CurrentCount(Outcome.Imported)==0&&report.ExcludedByMediaFilter==1);
            Fixtures.Check(report.PreviousCopies.Count==2&&report.PreviousCopies.Count(c=>c.ReusedForSelection)==1);
            Fixtures.Check(report.SourceDescription.Contains("Album sintetico",StringComparison.Ordinal)&&!report.EntireLibraryVerified);
            foreach(var (path,bytes) in before)Fixtures.Check(File.ReadAllBytes(path).SequenceEqual(bytes));
            foreach(string inventory in Directory.GetFiles(Path.Combine(destination.Root,".porto"),"inventory-*.json"))
                Fixtures.Check(!File.ReadAllText(inventory).Contains("IMG_9999.heic",StringComparison.Ordinal));
        }));
        tests.Add(("gallery import empty chosen type reports no matches instead of failure",()=>
        {
            using var destination=new Destination(Fixtures.Root());var inner=new FakeSource([new("photo","gallery","PRIVATE-PHOTO-NAME.heic",MediaKind.Image)],[]);
            using var source=new Selection(inner);var report=new ImportEngine(source,destination){Media=ImportMedia.Videos}.Run(false,default);
            Fixtures.Check(report.Status.StartsWith("Nessun elemento",StringComparison.Ordinal)&&report.FinalInventoryChecked&&report.ExcludedByMediaFilter==1);
            Fixtures.Check(inner.Reads==0&&report.Items.Count==0&&!report.GallerySelectionCopiesVerified);
            foreach(string path in Directory.GetFiles(destination.Root,"*.json",SearchOption.AllDirectories))
                Fixtures.Check(!File.ReadAllText(path).Contains("PRIVATE-PHOTO-NAME",StringComparison.Ordinal));
        }));
        tests.Add(("gallery import unresolved video remains counted and blocks verified selection",()=>
        {
            using var destination=new Destination(Fixtures.Root());var bytes=Fixtures.Video(502);
            using var source=new Selection(new FakeSource([new("good","gallery","good.mov",MediaKind.Video,bytes.Length),new("cloud","gallery","cloud.mov",MediaKind.Video,Offline:true)],new(){{"good",bytes}}));
            var report=new ImportEngine(source,destination){Media=ImportMedia.Videos}.Run(false,default);
            Fixtures.Check(!report.GallerySelectionCopiesVerified&&report.CandidateCount==2&&report.CurrentCount(Outcome.Imported)==1);
            Fixtures.Check(report.Items.Any(i=>i.SourceId=="cloud"&&i.Outcome is Outcome.Missing or Outcome.Error or Outcome.Pending or Outcome.Ambiguous));
        }));
        tests.Add(("gallery import changed album membership prevents verified selection",()=>
        {
            using var destination=new Destination(Fixtures.Root());
            using var source=new Selection(FakeSource.One(Fixtures.Video(503))){ChangeAtEnd=true};
            var report=new ImportEngine(source,destination).Run(false,default);
            Fixtures.Check(!report.FinalInventoryChecked&&!report.GallerySelectionCopiesVerified&&report.RetryRecommended);
        }));
        tests.Add(("gallery import retry continues past failed file without duplicating successful copy",()=>
        {
            using var destination=new Destination(Fixtures.Root());var bytes=Fixtures.Video(504);int attempts=0;
            IVideoSource Create()
            {
                int attempt=++attempts;var inner=new FakeSource([new("first","gallery","first.mov",MediaKind.Video,bytes.Length),new("second","gallery","second.mov",MediaKind.Video,bytes.Length)],new(){{"first",bytes},{"second",bytes}});
                inner.Reader=(item,_)=>item.Id=="first"&&attempt==1?throw new IOException("Synthetic temporary read failure"):new MemoryStream(bytes);
                return new Selection(inner);
            }
            var report=new ImportSession(Create,destination){RetryDelay=TimeSpan.Zero}.Run(false,default);
            Fixtures.Check(attempts==2&&report.GallerySelectionCopiesVerified&&Directory.GetFiles(destination.Videos).Length==2);
        }));
    }
    private sealed class Selection(FakeSource inner):IGallerySource
    {
        private int inventories;
        public bool ChangeAtEnd {get;init;}
        public bool AppleResourceHints=>false;
        public string SourceDescription {get;private set;}="Galleria sintetica";
        public GalleryCoverage? Coverage=>new("Catalogo sintetico; nessuno snapshot atomico certificato.",1,1,0,2,3);
        public Inventory Inventory(CancellationToken cancel)
        {
            SourceDescription="Album sintetico scelto";var result=inner.Inventory(cancel);
            return ++inventories>1&&ChangeAtEnd?new(result.Items.Concat([new SourceItem("late","gallery","late.mov",MediaKind.Video,5)]).ToList(),[],true):result;
        }
        public Stream OpenRead(SourceItem item,CancellationToken cancel)=>inner.OpenRead(item,cancel);
        public void Dispose()=>inner.Dispose();
    }
}
