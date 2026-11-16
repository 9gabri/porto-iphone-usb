using System.Security.Cryptography;
using Porto.Core;

internal static class Rc12PhotoRecoveryTests
{
    public static void Add(List<(string,Action)> tests)
    {
        tests.Add(("rc12 mixed album recovers 29 photos preserving 48 earlier copies and every old part",()=>
        {
            using var destination=new Destination(Fixtures.Root());
            var items=new List<SourceItem>();var data=new Dictionary<string,byte[]>();
            for(int i=0;i<77;i++)
            {
                bool video=i>=65;byte[] bytes=video?Fixtures.Video(i):Photo(i,i>=36?190:0);
                string id="asset-"+i;
                items.Add(new(id,"album",id+(video?".mp4":".jpg"),video?MediaKind.Video:MediaKind.Image,bytes.Length));data[id]=bytes;
            }
            var earlier=items.Where((_,i)=>i<36||i>=65).ToList();
            var first=new ImportEngine(new FakeSource(earlier,data),destination){Media=ImportMedia.PhotosAndVideos}.Run(false,default);
            Fixtures.Check(first.CurrentCount(Outcome.Imported)==48);
            var preserved=Directory.GetFiles(destination.Photos).Concat(Directory.GetFiles(destination.Videos))
                .ToDictionary(path=>path,path=>VerifiedCopy.HashFile(path,default));
            var pending=items.Skip(36).Take(29).ToList();
            using(var ledger=new WorkLedger(destination))
            using(var catalog=new Catalog(destination))
            {
                ledger.Observe(items,"synthetic-previous-album");
                foreach(var item in pending)
                {
                    byte[] bytes=data[item.Id];string id=Guid.NewGuid().ToString("N");
                    var entry=new CatalogEntry(id,"Quarantined",Path.Combine(".porto",id+".part"),"",Convert.ToHexString(SHA256.HashData(bytes)),bytes.Length,item);
                    string path=destination.Resolve(entry.Temp);File.WriteAllBytes(path,bytes);
                    var hash=VerifiedCopy.HashFile(path,default);preserved[path]=hash;catalog.Save(entry);
                    ledger.ReadVerified(item,hash);ledger.Record(item,new(item.Id,item.Name,Outcome.Ambiguous,"Synthetic rc11 JPEG tail rejection",entry.Temp,hash.Length,hash.Hash));
                }
            }
            string foreign=Path.Combine(destination.Root,"keep.txt");File.WriteAllText(foreign,"synthetic foreign file");preserved[foreign]=VerifiedCopy.HashFile(foreign,default);
            var recovered=new ImportEngine(new FakeSource(items,data),destination){Media=ImportMedia.PhotosAndVideos}.Run(false,default);
            Fixtures.Check(recovered.CurrentCount(Outcome.Imported)==29&&recovered.CurrentCount(Outcome.AlreadyPresent)==48,recovered.Status);
            Fixtures.Check(recovered.CurrentCount(Outcome.Ambiguous)==0&&recovered.CurrentCount(Outcome.Error)==0&&recovered.CurrentCount(Outcome.Pending)==0);
            Fixtures.Check(recovered.SelectedPhotoCount==65&&recovered.SelectedVideoCount==12&&recovered.Status.StartsWith("Completati",StringComparison.Ordinal));
            foreach(var entry in recovered.Items.Where(i=>i.Outcome==Outcome.Imported))
            {
                Fixtures.Check(entry.Detail.Contains("190 byte aggiuntivi",StringComparison.Ordinal)&&entry.Detail.Contains("non interpretati",StringComparison.Ordinal));
                Fixtures.Check(File.ReadAllBytes(destination.Resolve(entry.File!)).SequenceEqual(data[entry.SourceId]));
            }
            var again=new ImportEngine(new FakeSource(items,data),destination){Media=ImportMedia.PhotosAndVideos}.Run(false,default);
            Fixtures.Check(again.CurrentCount(Outcome.AlreadyPresent)==77&&again.CurrentCount(Outcome.Imported)==0);
            Fixtures.Check(again.Items.Count(i=>i.Detail.Contains("190 byte aggiuntivi",StringComparison.Ordinal))==29);
            Fixtures.Check(Directory.GetFiles(destination.Photos).Length==65&&Directory.GetFiles(destination.Videos).Length==12);
            foreach(var old in preserved)Fixtures.Check(VerifiedCopy.HashFile(old.Key,default)==old.Value,"Existing test file changed.");
        }));
        tests.Add(("rc12 changed JPEG trailer does not reuse an old different complete file",()=>
        {
            using var destination=new Destination(Fixtures.Root());var a=Photo(90,190);var b=(byte[])a.Clone();b[^1]^=1;
            var item=new SourceItem("p","album","photo.jpg",MediaKind.Image,a.Length);
            var first=new ImportEngine(new FakeSource([item],new(){{item.Id,a}}),destination){Media=ImportMedia.Photos}.Run(false,default);
            Fixtures.Check(first.CurrentCount(Outcome.Imported)==1);
            string saved=destination.Resolve(first.Items.Single(i=>i.Outcome==Outcome.Imported).File!);
            var later=new ImportEngine(new FakeSource([item with{Id="new",PersistentId="new"}],new(){{"new",b}}),destination){Media=ImportMedia.Photos}.Run(false,default);
            Fixtures.Check(later.CurrentCount(Outcome.AlreadyPresent)==0&&later.CurrentCount(Outcome.Imported)==1);
            Fixtures.Check(File.ReadAllBytes(saved).SequenceEqual(a)&&Directory.GetFiles(destination.Photos).Length==2);
        }));
        tests.Add(("rc12 JPEG with appended video stays one photo and is excluded from Solo video",()=>
        {
            byte[] bytes=Photo(91,0).Concat(Fixtures.Video(91)).ToArray();
            var item=new SourceItem("p","album","photo.jpg",MediaKind.Image,bytes.Length);
            using var destination=new Destination(Fixtures.Root());var source=new FakeSource([item],new(){{"p",bytes}});
            var onlyVideo=new ImportEngine(source,destination){Media=ImportMedia.Videos}.Run(false,default);
            Fixtures.Check(source.Reads==0&&onlyVideo.CandidateCount==0);
            var all=new ImportEngine(source,destination){Media=ImportMedia.PhotosAndVideos}.Run(false,default);
            Fixtures.Check(all.CurrentCount(Outcome.Imported)==1&&Directory.GetFiles(destination.Photos).Length==1&&Directory.GetFiles(destination.Videos).Length==0);
            Fixtures.Check(File.ReadAllBytes(destination.Resolve(all.Items.Single(i=>i.Outcome==Outcome.Imported).File!)).SequenceEqual(bytes));
        }));
    }
    private static byte[] Photo(int seed,int tail)
    {
        // Synthetic COM identifier makes every test picture distinct without real EXIF or pixels.
        byte[] jpeg=PhotoInspectionTests.Jpeg(),comment=[0xff,0xfe,0,6,(byte)seed,0,0,0];
        return jpeg[..2].Concat(comment).Concat(jpeg[2..]).Concat(Enumerable.Repeat((byte)(seed+1),tail)).ToArray();
    }
}
