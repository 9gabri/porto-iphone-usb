using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Porto.Core;

internal static class MultiMediaImportTests
{
    private static byte[] Photo()=>PhotoInspectionTests.Png();
    public static void Add(List<(string,Action)> tests)
    {
        tests.Add(("media selection copies only the chosen types and preserves all inventory hints",()=>
        {
            foreach(var mode in Enum.GetValues<ImportMedia>())
            {
                using var destination=new Destination(Fixtures.Root());byte[] photo=Photo(),video=Fixtures.Video();
                var source=new FakeSource([new("p","DEVICE","image.png",MediaKind.Image,photo.Length),new("v","DEVICE","video.mov",MediaKind.Video,video.Length)],new(){{"p",photo},{"v",video}});
                var read=new List<string>();source.Reader=(item,_)=>{read.Add(item.Id);return new MemoryStream(item.Id=="p"?photo:video);};
                var report=new ImportEngine(source,destination){Media=mode}.Run(false,default);
                int expected=mode==ImportMedia.PhotosAndVideos?2:1;
                Fixtures.Check(report.Media==mode&&report.CurrentCount(Outcome.Imported)==expected,$"Wrong import count for {mode}: {report.Status}");
                Fixtures.Check(report.SelectedPhotoCount==(mode==ImportMedia.Videos?0:1)&&report.SelectedVideoCount==(mode==ImportMedia.Photos?0:1));
                Fixtures.Check(read.Count==expected&&read.All(id=>mode==ImportMedia.PhotosAndVideos||(mode==ImportMedia.Photos?id=="p":id=="v")));
                var snapshot=JsonSerializer.Deserialize<Inventory>(File.ReadAllText(Directory.GetFiles(destination.State,"inventory-*.json").Single()))!;
                Fixtures.Check(snapshot.Items.Count==2&&snapshot.Items.Any(item=>item.Kind==MediaKind.Image));
                Fixtures.Check(Directory.GetFiles(destination.Photos).Length==(mode==ImportMedia.Videos?0:1));
            }
        }));
        tests.Add(("media legacy observations retain exact JSON bytes and key",()=>
        {
            var item=new SourceItem("a","DEVICE","clip.mov",MediaKind.Video,12);
            const string legacy="{\"Id\":\"a\",\"ParentId\":\"DEVICE\",\"Name\":\"clip.mov\",\"Kind\":0,\"Size\":12,\"PersistentId\":null,\"Format\":null,\"Offline\":false}";
            Fixtures.Check(JsonSerializer.Serialize(item)==legacy);
            Fixtures.Check(WorkLedger.Key(item)==Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(legacy))));
            Fixtures.Check(WorkLedger.Key(item)!=WorkLedger.Key(item with{SourceNamespace="usb:second"}));
        }));
        tests.Add(("media photo pass preserves prior video catalog and subsequent mixed pass reuses both",()=>
        {
            using var destination=new Destination(Fixtures.Root());var video=Fixtures.Video();var photo=Photo();
            new ImportEngine(FakeSource.One(video),destination).Run(false,default);
            string previous=Directory.GetFiles(destination.Videos).Single();var hash=VerifiedCopy.HashFile(previous,default);
            var source=new FakeSource([new("p","DEVICE","one.png",MediaKind.Image,photo.Length),new("v","DEVICE","clip.mov",MediaKind.Video,video.Length)],new(){{"p",photo},{"v",video}});
            var photos=new ImportEngine(source,destination){Media=ImportMedia.Photos}.Run(true,default);
            Fixtures.Check(photos.CurrentCount(Outcome.Imported)==1&&File.Exists(previous)&&VerifiedCopy.HashFile(previous,default)==hash);
            var both=new ImportEngine(source,destination){Media=ImportMedia.PhotosAndVideos}.Run(true,default);
            Fixtures.Check(both.CurrentCount(Outcome.AlreadyPresent)==2&&both.CurrentCount(Outcome.Imported)==0);
            using var catalog=new Catalog(destination);Fixtures.Check(catalog.Entries().Count==2);
        }));
        tests.Add(("media physical phone folder excludes siblings and keeps recovery forwarding",()=>
        {
            var inventory=Tree();var inner=new MutableUsb(inventory);
            var choice=UsbFolders.List(inventory).Single(folder=>folder.DisplayPath=="Storage / DCIM / Album");
            using var source=new UsbImportSource(inner,"phone-A","Android",false,choice);
            var scoped=source.Inventory(default);Fixtures.Check(scoped.Items.Select(item=>item.Id).Order().SequenceEqual(new[]{"album","inside","nested","nested-photo"}));
            Fixtures.Check(scoped.Items.All(item=>item.SourceNamespace!=null)&&!source.AppleResourceHints&&(IVideoSource)source is not ISelectedVideoSource);
            foreach(var item in scoped.Items.Where(item=>item.Kind!=MediaKind.Container)){using var read=source.OpenRead(item,default);}
            Fixtures.Check(inner.Reads.SequenceEqual(new[]{"inside","nested-photo"})&&inner.RawReads.All(item=>item.SourceNamespace==null));
            Fixtures.Throws<IOException>(()=>source.OpenRead(inventory.Items.Single(item=>item.Id=="outside"),default));
            source.InvalidateRead();Fixtures.Check(inner.Invalidations==1&&source.SessionRestarts==7);
        }));
        tests.Add(("media missing ambiguous moved or corrupt folder never expands to the phone",()=>
        {
            var initial=Tree();var choice=UsbFolders.List(initial).Single(folder=>folder.DisplayPath=="Storage / DCIM / Album");
            foreach(string mutation in new[]{"missing","duplicate","cycle","broken-parent","changed-puid","moved"})
            {
                var changed=Tree();
                switch(mutation)
                {
                    case "missing":changed.Items.RemoveAll(item=>item.Id=="album");break;
                    case "duplicate":changed.Items.Add(new("second-album","dcim","Album",MediaKind.Container,PersistentId:"second"));break;
                    case "cycle":changed.Items[0]=changed.Items[0] with{ParentId="nested"};break;
                    case "broken-parent":changed.Items.Add(new("broken","unknown","file.mov",MediaKind.Video));break;
                    case "changed-puid":changed.Items[2]=changed.Items[2] with{PersistentId="replacement"};break;
                    case "moved":changed.Items[2]=changed.Items[2] with{ParentId="storage"};break;
                }
                var inner=new MutableUsb(initial);using var source=new UsbImportSource(inner,"phone-A","Android",false,choice);
                var prior=source.Inventory(default).Items.Single(item=>item.Id=="inside");inner.Current=changed;
                Fixtures.Throws<IOException>(()=>source.Inventory(default));
                Fixtures.Throws<IOException>(()=>source.OpenRead(prior,default));Fixtures.Check(inner.Reads.Count==0,mutation);
            }
        }));
        tests.Add(("media phone folder tolerates changed transient IDs but not selection drift",()=>
        {
            var original=Tree();var choice=UsbFolders.List(original).Single(folder=>folder.DisplayPath=="Storage / DCIM / Album");
            var changed=new Inventory(original.Items.Select(item=>item with{Id="next-"+item.Id,ParentId=item.ParentId=="DEVICE"?"DEVICE":"next-"+item.ParentId}).ToList(),["synthetic full inventory error"],false);
            using var source=new UsbImportSource(new MutableUsb(changed),"phone-A","Android",false,choice);
            var actual=source.Inventory(default);Fixtures.Check(actual.Items.Count==4&&!actual.EnumerationComplete&&actual.Errors.Count==1);
        }));
        tests.Add(("media identical raw phone IDs do not merge different content histories",()=>
        {
            using var destination=new Destination(Fixtures.Root());
            using(var first=new UsbImportSource(FakeSource.One(Fixtures.Video(51)),"phone-A","A",false))
                Fixtures.Check(new ImportEngine(first,destination).Run(false,default).CurrentCount(Outcome.Imported)==1);
            using(var second=new UsbImportSource(FakeSource.One(Fixtures.Video(52)),"phone-B","B",false))
            {
                var report=new ImportEngine(second,destination).Run(false,default);
                Fixtures.Check(report.CurrentCount(Outcome.Imported)==1&&report.Items.All(item=>item.Outcome!=Outcome.Ambiguous));
            }
            using var ledger=new WorkLedger(destination);Fixtures.Check(ledger.Conflicts().Count==0&&Directory.GetFiles(destination.Videos).Length==2);
        }));
        tests.Add(("media repeated photo observations preserve two physical copies",()=>
        {
            using var destination=new Destination(Fixtures.Root());var bytes=Photo();
            var source=new FakeSource([new("a","DEVICE","a.png",MediaKind.Image,bytes.Length),new("b","DEVICE","b.png",MediaKind.Image,bytes.Length)],new(){{"a",bytes},{"b",bytes}});
            var first=new ImportEngine(source,destination){Media=ImportMedia.Photos}.Run(false,default);
            var second=new ImportEngine(source,destination){Media=ImportMedia.Photos}.Run(false,default);
            Fixtures.Check(first.CurrentCount(Outcome.Imported)==2&&second.CurrentCount(Outcome.AlreadyPresent)==2&&Directory.GetFiles(destination.Photos).Length==2);
        }));
        tests.Add(("media photo folder imports local bytes and preserves 193 earlier archive files",()=>
        {
            string root=Fixtures.Root(),exports=Path.Combine(root,"exports"),archive=Path.Combine(root,"archive");Directory.CreateDirectory(exports);
            File.WriteAllBytes(Path.Combine(exports,"photo.png"),Photo());
            File.WriteAllBytes(Path.Combine(exports,"unselected.mov"),Fixtures.Video(79));
            File.WriteAllBytes(Path.Combine(exports,"unselected.avi"),[1,2,3]);
            using var destination=new Destination(archive);
            var originals=new Dictionary<string,CopyResult>();
            for(int index=0;index<193;index++)
            {
                string path=Path.Combine(destination.Videos,$"previous-{index}.mov");File.WriteAllBytes(path,Fixtures.Video(index));
                originals.Add(path,VerifiedCopy.HashFile(path,default));
            }
            using var source=new FolderVideoSource(exports,archive);
            var first=new ImportEngine(source,destination){Media=ImportMedia.Photos}.Run(true,default);
            var second=new ImportEngine(source,destination){Media=ImportMedia.Photos}.Run(true,default);
            Fixtures.Check(first.SelectedFolderVerified&&first.CurrentCount(Outcome.Imported)==1&&first.CurrentCount(Outcome.Excluded)==2);
            Fixtures.Check(second.SelectedFolderVerified&&second.CurrentCount(Outcome.AlreadyPresent)==1&&Directory.GetFiles(destination.Photos).Length==1);
            Fixtures.Check(Directory.GetFiles(destination.Videos).Length==193);
            foreach(var original in originals)Fixtures.Check(VerifiedCopy.HashFile(original.Key,default)==original.Value);
        }));
        tests.Add(("media photo catalog permits only generated photo names and bounded namespaces",()=>
        {
            using var destination=new Destination(Fixtures.Root());using var catalog=new Catalog(destination);
            string id=Guid.NewGuid().ToString("N");var item=new SourceItem("p","DEVICE","one.png",MediaKind.Image,SourceNamespace:"usb:synthetic");
            string name=Destination.SafeStem(item.Name)+"_"+id+".png";
            var valid=new CatalogEntry(id,"Complete",Path.Combine(".porto",id+".part"),Path.Combine(Destination.PhotoDirectory,name),new string('A',64),16,item);
            catalog.Save(valid);Fixtures.Check(catalog.Entries().Single()==valid);
            foreach(string path in new[]{Path.Combine("Video Porto",name),Path.Combine(Destination.PhotoDirectory,"unmanaged.png"),Path.Combine(Destination.PhotoDirectory,name+".exe")})
                Fixtures.Throws<InvalidDataException>(()=>catalog.Save(valid with{Final=path}));
            foreach(string scope in new[]{"",new string('a',257),"usb:\0other"})
                Fixtures.Throws<InvalidDataException>(()=>catalog.Save(valid with{Source=item with{SourceNamespace=scope}}));
        }));
        tests.Add(("media conflicting photo video hints cannot publish or certify a video in photo mode",()=>
        {
            foreach(bool reuse in new[]{false,true})
            {
                using var destination=new Destination(Fixtures.Root());var bytes=Fixtures.Video(7);
                if(reuse)new ImportEngine(FakeSource.One(bytes),destination).Run(false,default);
                var item=new SourceItem("conflicting","DEVICE","one.jpg",MediaKind.Unknown,bytes.Length,Format:"b9820000");
                using var source=new FakeSource([item],new(){{item.Id,bytes}});
                var report=new ImportEngine(source,destination){Media=ImportMedia.Photos}.Run(false,default);
                Fixtures.Check(report.CurrentCount(Outcome.Ambiguous)==1&&report.CurrentCount(Outcome.Imported)==0&&report.CurrentCount(Outcome.AlreadyPresent)==0);
                Fixtures.Check(report.Items.Single(result=>result.SourceId==item.Id).Detail.Contains("contraddice",StringComparison.Ordinal));
                Fixtures.Check(Directory.GetFiles(destination.Videos).Length==(reuse?1:0)&&Directory.GetFiles(destination.Photos).Length==0);
            }
        }));
        tests.Add(("media non Apple phones do not inherit Apple paired-name routing hints",()=>
        {
            using var destination=new Destination(Fixtures.Root());var bytes=Fixtures.Video(8);var photo=Photo();
            using var source=new UsbImportSource(new FakeSource([new("v","DEVICE","IMG_E0001.mov",MediaKind.Video,bytes.Length),new("p","DEVICE","IMG_E0001.png",MediaKind.Image,photo.Length)],new(){{"v",bytes},{"p",photo}}),"android","Android",false);
            var report=new ImportEngine(source,destination){Media=ImportMedia.PhotosAndVideos}.Run(false,default);
            Fixtures.Check(report.CurrentCount(Outcome.Imported)==2&&report.SuspectedCompanionCount==0&&report.CurrentCount(Outcome.Additional)==0);
            Fixtures.Check(report.SourceDescription.StartsWith("Android",StringComparison.Ordinal)&&!report.SelectedFolder);
            Fixtures.Check(Directory.GetFiles(destination.Videos).Length==1&&Directory.GetFiles(destination.AdditionalVideos).Length==0);
        }));
        tests.Add(("media physical folder displays cannot confuse names with path separators",()=>
        {
            var inventory=new Inventory([
                new("storage","DEVICE","Storage",MediaKind.Container),
                new("flat","storage","A / B",MediaKind.Container),
                new("a","storage","A",MediaKind.Container),new("b","a","B",MediaKind.Container),
                new("controls","storage","control\n\\%2F",MediaKind.Container)],[],true);
            var choices=UsbFolders.List(inventory);
            Fixtures.Check(choices.Select(choice=>choice.DisplayPath).Distinct(StringComparer.Ordinal).Count()==choices.Count);
            var flat=choices.Single(choice=>choice.Segments[^1].Name=="A / B");
            Fixtures.Check(flat.DisplayPath=="Storage / A %2F B"&&choices.Any(choice=>choice.DisplayPath=="Storage / A / B"));
            Fixtures.Check(choices.Single(choice=>choice.Segments[^1].Name.StartsWith("control",StringComparison.Ordinal)).DisplayPath=="Storage / control%0A%5C%252F");
            using var source=new UsbImportSource(new MutableUsb(inventory),"phone-A","Phone",false,flat);
            Fixtures.Check(source.Inventory(default).Items.Single().Id=="flat");
        }));
        tests.Add(("media generic unsupported videos remain ambiguous in video modes",()=>
        {
            foreach(var mode in Enum.GetValues<ImportMedia>())
            {
                using var destination=new Destination(Fixtures.Root());
                using var source=new FakeSource([new("generic","DEVICE","clip.mkv",MediaKind.Other,3)],new(){{"generic",new byte[]{1,2,3}}});
                var report=new ImportEngine(source,destination){Media=mode}.Run(false,default);
                Fixtures.Check(report.CurrentCount(mode==ImportMedia.Photos?Outcome.Excluded:Outcome.Ambiguous)==1&&source.Reads==0);
                Fixtures.Check(report.CurrentCount(Outcome.Imported)==0&&report.CurrentCount(Outcome.AlreadyPresent)==0);
            }
        }));
    }
    private static Inventory Tree()=>new([
        new("storage","DEVICE","Storage",MediaKind.Container,PersistentId:"storage-puid"),
        new("dcim","storage","DCIM",MediaKind.Container,PersistentId:"dcim-puid"),
        new("album","dcim","Album",MediaKind.Container,PersistentId:"album-puid"),
        new("inside","album","in.mov",MediaKind.Video),new("outside","dcim","out.mov",MediaKind.Video),
        new("nested","album","Nested",MediaKind.Container),new("nested-photo","nested","nested.png",MediaKind.Image)],[],true);
    private sealed class MutableUsb(Inventory initial):IVideoSource,IReadFailureRecovery
    {
        public Inventory Current=initial;
        public List<string> Reads {get;}=[];
        public List<SourceItem> RawReads {get;}=[];
        public int Invalidations {get;private set;}
        public int SessionRestarts=>7;
        public Inventory Inventory(CancellationToken cancel)=>Current;
        public Stream OpenRead(SourceItem item,CancellationToken cancel){Reads.Add(item.Id);RawReads.Add(item);return new MemoryStream([1,2,3]);}
        public void InvalidateRead()=>Invalidations++;
        public void Dispose(){}
    }
}
