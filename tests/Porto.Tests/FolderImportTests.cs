using Porto.Core;

internal static class FolderImportTests
{
    public static void Add(List<(string,Action)> tests)
    {
        tests.Add(("folder import unsupported video container prevents a verified selection",()=>
        {
            string root=Fixtures.Root(),sourcePath=Path.Combine(root,"exports"),archive=Path.Combine(root,"archive");Directory.CreateDirectory(sourcePath);
            File.WriteAllBytes(Path.Combine(sourcePath,"supported.mov"),Fixtures.Video(1));
            File.WriteAllBytes(Path.Combine(sourcePath,"unsupported.avi"),[1,2,3,4]);
            using var source=new FolderVideoSource(sourcePath,archive);using var destination=new Destination(archive);
            var report=new ImportEngine(source,destination).Run(false,default);
            Fixtures.Check(!report.SelectedFolderVerified&&report.CurrentCount(Outcome.Imported)==1&&report.CurrentCount(Outcome.Ambiguous)==1);
            Fixtures.Check(File.ReadAllBytes(Path.Combine(sourcePath,"unsupported.avi")).SequenceEqual(new byte[]{1,2,3,4}));
        }));
        tests.Add(("folder import changed final inventory prevents verified selection",()=>
        {
            using var destination=new Destination(Fixtures.Root());
            using var selected=new Selection(Source(("a","one.mov",Fixtures.Video(1))))
                {FinalInventory=initial=>new(initial.Items.Concat([new SourceItem("new","synthetic","late.mov",MediaKind.Video,50)]).ToList(),[],true)};
            var report=new ImportEngine(selected,destination).Run(false,default);
            Fixtures.Check(!report.SelectedFolderVerified&&!report.FinalInventoryChecked&&report.CurrentCount(Outcome.Pending)==1);
        }));
        tests.Add(("folder import inconsistent deep reread cannot confirm reuse",()=>
        {
            using var destination=new Destination(Fixtures.Root());var data=Fixtures.Video(1);
            new ImportEngine(Source(("old","clip.mov",data)),destination).Run(false,default);var before=MediaFiles(destination);
            var changing=Source(("a","clip.mov",data));changing.Reader=(_,n)=>new MemoryStream(n==1?data:Fixtures.Video(2));
            using var selected=new Selection(changing);
            var report=new ImportEngine(selected,destination).Run(true,default);
            Fixtures.Check(!report.SelectedFolderVerified&&report.CurrentCount(Outcome.AlreadyPresent)==0&&report.CurrentCount(Outcome.Error)==1);
            Fixtures.Check(report.PreviousCopies.All(c=>!c.ReusedForSelection));Preserved(before);
        }));
        tests.Add(("folder import real local source repeats without duplication and preserves exported bytes",()=>
        {
            string root=Fixtures.Root(),sourcePath=Path.Combine(root,"exports"),archive=Path.Combine(root,"archive");Directory.CreateDirectory(sourcePath);
            var data=Fixtures.Video(41);string original=Path.Combine(sourcePath,"IMG_E0001.MOV");File.WriteAllBytes(original,data);
            File.WriteAllText(Path.Combine(sourcePath,"accessory.AAE"),"synthetic metadata");
            using var source=new FolderVideoSource(sourcePath,archive);using var destination=new Destination(archive);
            var first=new ImportEngine(source,destination).Run(true,default);
            var second=new ImportEngine(source,destination).Run(true,default);
            Fixtures.Check(first.SelectedFolderVerified&&first.CurrentCount(Outcome.Imported)==1&&first.CurrentCount(Outcome.Excluded)==1);
            Fixtures.Check(second.SelectedFolderVerified&&second.CurrentCount(Outcome.AlreadyPresent)==1&&second.CurrentCount(Outcome.Imported)==0);
            Fixtures.Check(File.ReadAllBytes(original).SequenceEqual(data)&&Directory.GetFiles(destination.Videos).Length==1);
        }));
        tests.Add(("folder import reuses unregistered files without modifying or registering foreign paths",()=>
        {
            using var destination=new Destination(Fixtures.Root());var data=Fixtures.Video(17);
            string foreign=Path.Combine(destination.Root,"Originale cliente.mov");File.WriteAllBytes(foreign,data);
            string extra=Path.Combine(destination.Videos,"altro.mov");File.WriteAllBytes(extra,Fixtures.Video(18));
            using var selected=new Selection(Source(("a","renamed.mov",data)));
            var report=new ImportEngine(selected,destination).Run(false,default);
            Fixtures.Check(report.SelectedFolderVerified&&report.CurrentCount(Outcome.AlreadyPresent)==1&&report.CurrentCount(Outcome.Imported)==0);
            Fixtures.Check(report.PreviousCopies.Count==2&&report.PreviousCopies.Count(c=>c.ReusedForSelection)==1);
            Fixtures.Check(File.ReadAllBytes(foreign).SequenceEqual(data)&&Directory.GetFiles(destination.Videos).Length==1);
            using var catalog=new Catalog(destination);Fixtures.Check(catalog.Entries().Count==0);
        }));
        tests.Add(("folder import selection subset reuses surviving copy and retains historical multiplicity debt",()=>
        {
            using var destination=new Destination(Fixtures.Root());var data=Fixtures.Video(9);
            new ImportEngine(Source(("old1","a.mov",data),("old2","b.mov",data),("old3","c.mov",data)),destination).Run(false,default);
            var files=Directory.GetFiles(destination.Videos);File.Delete(files[1]);File.Delete(files[2]);
            using var selected=new Selection(Source(("chosen","one.mov",data)));
            var report=new ImportEngine(selected,destination).Run(false,default);
            Fixtures.Check(report.SelectedFolderVerified&&report.CurrentCount(Outcome.AlreadyPresent)==1&&report.CurrentCount(Outcome.Imported)==0);
            Fixtures.Check(report.Items.Any(i=>i.Historical&&i.Outcome==Outcome.Pending)&&!report.RetryRecommended);
            Fixtures.Check(Directory.GetFiles(destination.Videos).Length==1);
        }));
        tests.Add(("folder import matches selected videos including prior additional copies without moving extras",()=>
        {
            using var destination=new Destination(Fixtures.Root());
            var prior=Source(("old-real","clip.mov",Fixtures.Video(1)),("old-extra","IMG_E1234.MOV",Rc8MediaTests.TimedVideo()),("unselected","other.mov",Fixtures.Video(3)));
            new ImportEngine(prior,destination).Run(false,default);
            var before=MediaFiles(destination);
            using var selected=new Selection(Source(("chosen1","renamed.mov",Fixtures.Video(1)),("chosen2","IMG_E2222.MOV",Rc8MediaTests.TimedVideo()),("new","new.mov",Fixtures.Video(4))));
            var report=new ImportEngine(selected,destination).Run(true,default);
            Fixtures.Check(report.SelectedFolder&&report.SelectedFolderVerified);
            Fixtures.Check(report.CurrentCount(Outcome.AlreadyPresent)==2&&report.CurrentCount(Outcome.Imported)==1);
            Fixtures.Check(report.CurrentCount(Outcome.AdditionalAlreadyPresent)==0&&report.CurrentCount(Outcome.Additional)==0);
            Fixtures.Check(report.PreviousCopies.Count==3&&report.PreviousCopies.Count(c=>c.ReusedForSelection)==2);
            Fixtures.Check(report.PreviousCopies.Single(c=>!c.ReusedForSelection).IntegrityVerified);
            Preserved(before);
            Fixtures.Check(MediaFiles(destination).Count==4);
        }));
        tests.Add(("folder import preserves multiplicity and never uses one prior file twice",()=>
        {
            using var destination=new Destination(Fixtures.Root());var data=Fixtures.Video(1);
            new ImportEngine(Source(("old","original.mov",data)),destination).Run(false,default);
            using var selection=new Selection(Source(("a","same.mov",data),("b","same.mov",data)));
            var first=new ImportEngine(selection,destination).Run(false,default);
            Fixtures.Check(first.SelectedFolderVerified&&first.CurrentCount(Outcome.AlreadyPresent)==1&&first.CurrentCount(Outcome.Imported)==1);
            var before=MediaFiles(destination);
            var repeated=new ImportEngine(selection,destination).Run(false,default);
            Fixtures.Check(repeated.SelectedFolderVerified&&repeated.CurrentCount(Outcome.AlreadyPresent)==2&&repeated.CurrentCount(Outcome.Imported)==0);
            Preserved(before);Fixtures.Check(MediaFiles(destination).Count==2);
        }));
        tests.Add(("folder import historical USB errors remain visible without becoming selected videos",()=>
        {
            using var destination=new Destination(Fixtures.Root());var old=Source(("unread","old.mov",Fixtures.Video(1)));
            old.Reader=(_,_)=>throw new IOException("Synthetic unread USB video");
            new ImportEngine(old,destination).Run(false,default);
            using var selected=new Selection(Source(("current","clip.mov",Fixtures.Video(2))));
            var report=new ImportEngine(selected,destination).Run(false,default);
            Fixtures.Check(report.SelectedFolderVerified&&report.HistoricalCount>0&&report.CandidateCount==1);
            Fixtures.Check(!report.Status.StartsWith("Completati",StringComparison.Ordinal));
        }));
        tests.Add(("folder import corrupt old copy is preserved and repaired in a new file",()=>
        {
            using var destination=new Destination(Fixtures.Root());var data=Fixtures.Video(1);
            new ImportEngine(Source(("old","clip.mov",data)),destination).Run(false,default);
            string prior=Directory.GetFiles(destination.Videos).Single();File.WriteAllBytes(prior,[1,2,3]);
            using var selected=new Selection(Source(("current","clip.mov",data)));
            var report=new ImportEngine(selected,destination).Run(false,default);
            Fixtures.Check(report.CurrentCount(Outcome.Imported)==1&&File.ReadAllBytes(prior).SequenceEqual(new byte[]{1,2,3}));
            Fixtures.Check(report.PreviousCopies.Count==1&&!report.PreviousCopies[0].IntegrityVerified&&!report.PreviousCopies[0].ReusedForSelection);
        }));
        tests.Add(("folder import repaired same-selection archive defect stays historical and visible on next run",()=>
        {
            using var destination=new Destination(Fixtures.Root());var data=Fixtures.Video(71);
            using var selection=new Selection(Source(("same","same.mov",data)));
            new ImportEngine(selection,destination).Run(false,default);
            string broken=Directory.GetFiles(destination.Videos).Single();File.WriteAllBytes(broken,[7,8,9]);
            var repaired=new ImportEngine(selection,destination).Run(false,default);
            Fixtures.Check(repaired.SelectedFolderVerified&&repaired.CurrentCount(Outcome.Imported)==1&&repaired.HistoricalCount>0);
            var again=new ImportEngine(selection,destination).Run(false,default);
            Fixtures.Check(again.SelectedFolderVerified&&again.CurrentCount(Outcome.AlreadyPresent)==1&&again.CurrentCount(Outcome.Imported)==0);
            Fixtures.Check(again.PreviousCopies.Count==2&&again.PreviousCopies.Single(c=>c.File==Path.GetRelativePath(destination.Root,broken)).IntegrityVerified==false);
            Fixtures.Check(File.ReadAllBytes(broken).SequenceEqual(new byte[]{7,8,9}));
        }));
        tests.Add(("folder import malformed selected file remains unresolved and other videos continue",()=>
        {
            using var destination=new Destination(Fixtures.Root());
            using var selection=new Selection(Source(("broken","broken.mov",new byte[]{1,2,3}),("good","good.mov",Fixtures.Video(3))));
            var report=new ImportEngine(selection,destination).Run(false,default);
            Fixtures.Check(!report.SelectedFolderVerified&&report.CurrentCount(Outcome.Imported)==1&&report.CurrentCount(Outcome.Ambiguous)==1);
        }));
    }
    private static FakeSource Source(params (string id,string name,byte[] bytes)[] items)=>new(
        items.Select(i=>new SourceItem(i.id,"synthetic",i.name,MediaKind.Video,i.bytes.Length)).ToList(),items.ToDictionary(i=>i.id,i=>i.bytes));
    private static Dictionary<string,CopyResult> MediaFiles(Destination d)=>Directory.GetFiles(d.Videos).Concat(Directory.GetFiles(d.AdditionalVideos)).ToDictionary(p=>p,p=>VerifiedCopy.HashFile(p,default));
    private static void Preserved(Dictionary<string,CopyResult> before){foreach(var pair in before)Fixtures.Check(File.Exists(pair.Key)&&VerifiedCopy.HashFile(pair.Key,default)==pair.Value);}
    private sealed class Selection(IVideoSource inner):ISelectedVideoSource
    {
        private int inventories;
        public Func<Inventory,Inventory>? FinalInventory {get;init;}
        public string SelectionDescription=>"Esportazione selezionata, dati sintetici";
        public Inventory Inventory(CancellationToken cancel){var result=inner.Inventory(cancel);return ++inventories>1&&FinalInventory!=null?FinalInventory(result):result;}
        public Stream OpenRead(SourceItem item,CancellationToken cancel)=>inner.OpenRead(item,cancel);
        public void Dispose()=>inner.Dispose();
    }
}
