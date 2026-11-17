using Porto.Apple;
using Porto.Core;

internal static class AppleAlbumAssociationTests
{
    internal static void Add(List<(string,Action)> tests)
    {
        tests.Add(("apple album association isolates a broken album without broadening a healthy selection",()=>
        {
            var catalog=AppleCatalogDecoder.Decode(AppleGalleryTests.Database(HealthyVideoAlbum+"INSERT INTO Z_26ASSETS VALUES(1,999);"),default);
            var broken=catalog.Albums.Single(a=>a.Id=="album-one");
            var healthy=catalog.Albums.Single(a=>a.Id=="healthy-video");
            Fixtures.Check(catalog.UnresolvedAssociationCount==1&&!broken.IsSelectable&&broken.UnresolvedAssociations==1);
            Fixtures.Check(healthy.IsSelectable&&healthy.UnresolvedAssociations==0&&healthy.AssetCount==1&&healthy.VideoCount==1);
            Fixtures.Check(catalog.Select("healthy-video").Select(a=>a.Id).SequenceEqual(new[]{"video"}));
            Fixtures.Throws<IOException>(()=>catalog.Select("album-one"));
            Fixtures.Throws<IOException>(()=>catalog.Select(null));
        }));
        tests.Add(("apple album association reports unknown album links without treating them as deleted",()=>
        {
            foreach(long key in new long[]{1,999})
            {
                var catalog=AppleCatalogDecoder.Decode(AppleGalleryTests.Database(HealthyVideoAlbum+$"INSERT INTO Z_26ASSETS VALUES(999,{key});"),default);
                Fixtures.Check(catalog.UnresolvedAssociationCount==1&&catalog.Albums.All(a=>a.IsSelectable));
                Fixtures.Check(catalog.Select("healthy-video").Single().Id=="video");
                Fixtures.Check(catalog.Select("album-one").Count==2);
                Fixtures.Throws<IOException>(()=>catalog.Select(null));
                Fixtures.Check(catalog.ScopeNote.Contains("1",StringComparison.Ordinal),"Unresolved relation count must remain visible");
            }
        }));
        tests.Add(("apple album association missing assets remain explicit and cannot masquerade as an empty album",()=>
        {
            var catalog=AppleCatalogDecoder.Decode(AppleGalleryTests.Database("DELETE FROM Z_26ASSETS; INSERT INTO Z_26ASSETS VALUES(1,991),(1,992);"),default);
            var album=catalog.Albums.Single();
            Fixtures.Check(!album.IsSelectable&&album.UnresolvedAssociations==2&&catalog.UnresolvedAssociationCount==2);
            Fixtures.Check(album.AssetCount==2,"Unresolved selected elements must not vanish from album counts");
            Fixtures.Throws<IOException>(()=>catalog.Select(album.Id));
            Fixtures.Throws<IOException>(()=>catalog.Select(null));
        }));
        tests.Add(("apple album association healthy source reads only its selected video despite another broken album",()=>
        {
            byte[] bytes=Fixtures.Video(181);
            using var files=new Files(AppleGalleryTests.Database(HealthyVideoAlbum+"INSERT INTO Z_26ASSETS VALUES(1,999);"));
            files.Data[Video]=bytes;
            using var source=new AppleGallerySource(new("synthetic-device","Synthetic"),"healthy-video",_=>files,ImportMedia.Videos);
            var inventory=source.Inventory(default);
            Fixtures.Check(inventory.EnumerationComplete&&inventory.Errors.Count==0&&inventory.Items.Count==1);
            Fixtures.Check(inventory.Items[0].PersistentId=="video"&&source.Coverage!.Unresolved==0);
            Fixtures.Check(files.Stats.Where(p=>p.StartsWith("/DCIM/",StringComparison.Ordinal)).SequenceEqual(new[]{Video}));
            using var input=source.OpenRead(inventory.Items[0],default);using var output=new MemoryStream();input.CopyTo(output);
            Fixtures.Check(output.ToArray().SequenceEqual(bytes));
            Fixtures.Check(files.Opens.Where(p=>p.StartsWith("/DCIM/",StringComparison.Ordinal)).SequenceEqual(new[]{Video}));
        }));
        tests.Add(("apple album association rejected album and gallery never stat or open any original",()=>
        {
            foreach(string? selection in new string?[]{"album-one",null})
            {
                using var files=new Files(AppleGalleryTests.Database("INSERT INTO Z_26ASSETS VALUES(1,999);"));
                using var source=new AppleGallerySource(new("synthetic-device","Synthetic"),selection,_=>files,ImportMedia.PhotosAndVideos);
                Fixtures.Throws<IOException>(()=>source.Inventory(default));
                Fixtures.Check(!files.Stats.Concat(files.Opens).Any(p=>p.StartsWith("/DCIM/",StringComparison.Ordinal)),"Uncertain selection must not open original media");
                Fixtures.Check(source.CurrentCatalog==null&&source.Coverage==null);
            }
        }));
        tests.Add(("apple album association known hidden and deleted members are exclusions not missing relations",()=>
        {
            var catalog=AppleCatalogDecoder.Decode(AppleGalleryTests.Database("INSERT INTO Z_26ASSETS VALUES(1,5),(1,6);"),default);
            var album=catalog.Albums.Single();
            Fixtures.Check(catalog.UnresolvedAssociationCount==0&&album.IsSelectable&&album.UnresolvedAssociations==0);
            Fixtures.Check(catalog.Select(album.Id).Select(a=>a.Id).SequenceEqual(new[]{"photo-live","missing"}));
            Fixtures.Check(catalog.HiddenAssetCount==1&&catalog.TrashedAssetCount==1&&catalog.Select(null).Count==4);
        }));
        tests.Add(("apple album association duplicate membership remains rejected",()=>
            Fixtures.Throws<IOException>(()=>AppleCatalogDecoder.Decode(AppleGalleryTests.Database("INSERT INTO Z_26ASSETS VALUES(1,1);"),default))));
        tests.Add(("apple album association unresolved system and deleted albums remain globally visible",()=>
        {
            var catalog=AppleCatalogDecoder.Decode(AppleGalleryTests.Database(HealthyVideoAlbum+"INSERT INTO ZGENERICALBUM VALUES(3,'system','System',1000,0),(4,'deleted-album','Deleted',2,1); INSERT INTO Z_26ASSETS VALUES(3,991),(4,992);"),default);
            Fixtures.Check(catalog.UnresolvedAssociationCount==2&&catalog.Albums.Count==2&&catalog.Albums.All(a=>a.IsSelectable));
            Fixtures.Check(catalog.Select("healthy-video").Single().Id=="video");
            Fixtures.Throws<IOException>(()=>catalog.Select("system"));
            Fixtures.Throws<IOException>(()=>catalog.Select("deleted-album"));
            Fixtures.Throws<IOException>(()=>catalog.Select(null));
        }));
        tests.Add(("apple album association final membership uncertainty preserves copy but blocks successful report",()=>
        {
            byte[] video=Fixtures.Video(182);
            byte[] changed=AppleGalleryTests.Database(HealthyVideoAlbum+"INSERT INTO Z_26ASSETS VALUES(2,999);");
            using var files=new Files(AppleGalleryTests.Database(HealthyVideoAlbum));
            files.Data[Video]=video;
            files.OnOpen=path=>{if(path==Video)files.Data["/PhotoData/Photos.sqlite"]=changed;};
            using var source=new AppleGallerySource(new("synthetic-device","Synthetic"),"healthy-video",_=>files,ImportMedia.Videos);
            using var destination=new Destination(Fixtures.Root());
            var report=new ImportEngine(source,destination){Media=ImportMedia.Videos}.Run(false,default);
            Fixtures.Check(!report.FinalInventoryChecked&&!report.GallerySelectionCopiesVerified);
            Fixtures.Check(report.InventoryErrors.Any(error=>error.Contains("APPLE-ALBUM-ASSOCIATIONS",StringComparison.Ordinal)));
            string[] copies=Directory.GetFiles(destination.Videos);
            Fixtures.Check(copies.Length==1&&File.ReadAllBytes(copies[0]).SequenceEqual(video),"Completed bytes must remain intact after final inventory failure");
            Fixtures.Check(report.CurrentCount(Outcome.Imported)==1);
        }));
    }

    private const string HealthyVideoAlbum="INSERT INTO ZGENERICALBUM VALUES(2,'healthy-video','Video prova',2,0); INSERT INTO Z_26ASSETS VALUES(2,2);";
    private const string Video="/DCIM/100APPLE/IMG_2.MOV";
    private sealed class Files(byte[] database):IAppleFileSystem
    {
        internal readonly Dictionary<string,byte[]> Data=new(StringComparer.Ordinal){{"/PhotoData/Photos.sqlite",database}};
        internal readonly List<string> Stats=[],Opens=[];
        internal Action<string>? OnOpen;
        public AppleFileInfo? Stat(string path,CancellationToken cancel)
        {cancel.ThrowIfCancellationRequested();Stats.Add(path);return Data.TryGetValue(path,out var bytes)?new(bytes.LongLength,"1",true):null;}
        public Stream OpenRead(string path,CancellationToken cancel)
        {cancel.ThrowIfCancellationRequested();Opens.Add(path);OnOpen?.Invoke(path);return new MemoryStream(Data[path],false);}
        public void Dispose(){}
    }
}
