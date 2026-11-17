using System.Runtime.InteropServices;
using Porto.Apple;
using Porto.Core;

internal static class AppleGalleryTests
{
    public static void Add(List<(string,Action)> tests)
    {
        AppleTransportTests.Add(tests);
        tests.Add(("apple catalog distinguishes true video from Live Photo and preserves missing paths",()=>
        {
            var catalog=AppleCatalogDecoder.Decode(Database(),default);
            Fixtures.Check(catalog.Assets.Count==4&&catalog.VisiblePhotoCount==2&&catalog.VisibleVideoCount==1);
            Fixtures.Check(catalog.Assets.Single(a=>a.Id=="photo-live").Kind==MediaKind.Image);
            Fixtures.Check(catalog.Assets.Single(a=>a.Id=="missing").Path==null);
            Fixtures.Check(catalog.UnresolvedAssetCount==2&&catalog.HiddenAssetCount==1&&catalog.TrashedAssetCount==1);
        }));
        tests.Add(("apple catalog reads native album membership without paths and rejects absent selection",()=>
        {
            var catalog=AppleCatalogDecoder.Decode(Database(),default);
            Fixtures.Check(catalog.Albums.Count==1&&catalog.Albums[0].AssetCount==2);
            Fixtures.Check(catalog.Select("album-one").Count==2);
            Fixtures.Throws<IOException>(()=>catalog.Select("other"));
        }));
        tests.Add(("apple catalog uses native parent folders to distinguish identical album names",()=>
        {
            const string hierarchy="ALTER TABLE ZGENERICALBUM ADD COLUMN ZPARENTFOLDER INTEGER DEFAULT 0; INSERT INTO ZGENERICALBUM VALUES(10,'family','Famiglia',4000,0,0),(11,'work','Lavoro',4000,0,0),(2,'album-two','Vacanze',2,0,11); UPDATE ZGENERICALBUM SET ZPARENTFOLDER=10 WHERE Z_PK=1;";
            var catalog=AppleCatalogDecoder.Decode(Database(hierarchy),default);
            Fixtures.Check(catalog.Albums.Single(a=>a.Id=="album-one").Name=="Famiglia / Vacanze"&&catalog.Albums.Single(a=>a.Id=="album-two").Name=="Lavoro / Vacanze");
            Fixtures.Check(catalog.Select("album-one").Count==2&&catalog.Select("album-two").Count==0);
            Fixtures.Throws<IOException>(()=>AppleCatalogDecoder.Decode(Database(hierarchy+"UPDATE ZGENERICALBUM SET ZPARENTFOLDER=10 WHERE Z_PK=10"),default));
        }));
        tests.Add(("apple catalog rejects unsupported schema and malformed membership",()=>
        {
            Fixtures.Throws<IOException>(()=>AppleCatalogDecoder.Decode(Database("ALTER TABLE ZASSET RENAME COLUMN ZKIND TO UNKNOWN"),default));
            var malformed=AppleCatalogDecoder.Decode(Database("INSERT INTO Z_26ASSETS VALUES(1,999)"),default);
            Fixtures.Throws<IOException>(()=>malformed.Select("album-one"));
            Fixtures.Throws<IOException>(()=>malformed.Select(null));
        }));
        tests.Add(("apple catalog rejects duplicate asset UUID and ambiguous album relationship",()=>
        {
            Fixtures.Throws<IOException>(()=>AppleCatalogDecoder.Decode(Database("UPDATE ZASSET SET ZUUID='photo-live' WHERE Z_PK=2"),default));
            Fixtures.Throws<IOException>(()=>AppleCatalogDecoder.Decode(Database("CREATE TABLE Z_27ASSETS(Z_27ALBUMS INTEGER,Z_3ASSETS INTEGER)"),default));
        }));
        tests.Add(("apple catalog rejects traversal paths without dropping known media",()=>
        {
            var catalog=AppleCatalogDecoder.Decode(Database("UPDATE ZASSET SET ZDIRECTORY='DCIM/../PhotoData' WHERE Z_PK=2"),default);
            var asset=catalog.Assets.Single(a=>a.Id=="video");
            Fixtures.Check(asset.Kind==MediaKind.Video&&asset.Path==null&&catalog.UnresolvedAssetCount==3);
        }));
        tests.Add(("apple catalog memory reader rejects truncated database and cancellation",()=>
        {
            var bytes=Database();
            Fixtures.Throws<IOException>(()=>AppleCatalogDecoder.Decode(bytes[..^1],default));
            Fixtures.Throws<OperationCanceledException>(()=>AppleCatalogDecoder.Decode(bytes,new CancellationToken(true)));
        }));
        tests.Add(("apple catalog source counts unavailable originals and never exposes Live companion video",()=>
        {
            var bytes=Database();var fs=new MemoryFiles(bytes);
            fs.Files["/DCIM/100APPLE/IMG_1.HEIC"]=[1,2];fs.Files["/DCIM/100APPLE/IMG_2.MOV"]=[3,4,5];
            using var source=new AppleGallerySource(new("synthetic-device","Synthetic"),null,_=>fs);
            var inventory=source.Inventory(default);
            Fixtures.Check(inventory.Items.Count==4&&inventory.Items.Count(i=>i.Kind==MediaKind.Video)==1);
            Fixtures.Check(inventory.Items.Single(i=>i.PersistentId=="missing").Offline&&source.Coverage!.Unresolved==2);
            var video=inventory.Items.Single(i=>i.Kind==MediaKind.Video);using var input=source.OpenRead(video,default);using var output=new MemoryStream();input.CopyTo(output);
            Fixtures.Check(output.ToArray().SequenceEqual(new byte[]{3,4,5}));
        }));
        tests.Add(("apple video filter avoids stat for ten thousand photos and missing unselected originals",()=>
        {
            var bytes=Database("DELETE FROM ZASSET WHERE Z_PK=4; WITH RECURSIVE n(x) AS(SELECT 10 UNION ALL SELECT x+1 FROM n WHERE x<10009) INSERT INTO ZASSET SELECT x,'photo-'||x,'DCIM/100APPLE','P_'||x||'.JPG',0,0,0,0 FROM n;");
            var fs=new MemoryFiles(bytes);fs.Files["/DCIM/100APPLE/IMG_2.MOV"]=[1,2,3];
            using var source=new AppleGallerySource(new("synthetic-device","Synthetic"),null,_=>fs,ImportMedia.Videos);
            var inventory=source.Inventory(default);
            Fixtures.Check(inventory.Items.Count==10003&&inventory.EnumerationComplete&&inventory.Errors.Count==0);
            Fixtures.Check(fs.Stats.Count(p=>p.StartsWith("/DCIM/",StringComparison.Ordinal))==1&&source.Coverage!.Unresolved==0);
        }));
        tests.Add(("apple source album does not widen selection and records missing image",()=>
        {
            var fs=new MemoryFiles(Database());fs.Files["/DCIM/100APPLE/IMG_1.HEIC"]=[1];
            using var source=new AppleGallerySource(new("synthetic-device","Synthetic"),"album-one",_=>fs);
            var inventory=source.Inventory(default);
            Fixtures.Check(inventory.Items.Count==2&&inventory.Items.All(i=>i.Kind==MediaKind.Image)&&source.Coverage!.Unresolved==1);
        }));
        tests.Add(("apple unknown catalog type named mov remains unresolved and cannot be read",()=>
        {
            var fs=new MemoryFiles(Database("UPDATE ZASSET SET ZFILENAME='UNKNOWN.MOV' WHERE Z_PK=4"));fs.Files["/DCIM/100APPLE/UNKNOWN.MOV"]=[1,2,3];
            using var source=new AppleGallerySource(new("synthetic-device","Synthetic"),null,_=>fs,ImportMedia.Videos);
            var unknown=source.Inventory(default).Items.Single(i=>i.PersistentId=="unknown");
            Fixtures.Check(unknown.Kind==MediaKind.Unknown&&unknown.Offline);
            Fixtures.Throws<IOException>(()=>source.OpenRead(unknown,default));
        }));
        tests.Add(("apple source recovery reconnects only with matching complete selection",()=>
        {
            var bytes=Database("DELETE FROM ZASSET WHERE Z_PK=4");int connects=0;
            using var source=new AppleGallerySource(new("synthetic-device","Synthetic"),null,_=>
            {connects++;var fs=new MemoryFiles(bytes);fs.Files["/DCIM/100APPLE/IMG_2.MOV"]=[1,2,3];return fs;},ImportMedia.Videos);
            var video=source.Inventory(default).Items.Single(i=>i.Kind==MediaKind.Video);
            source.InvalidateRead();using var input=source.OpenRead(video,default);
            Fixtures.Check(connects==2&&source.SessionRestarts==1&&input.ReadByte()==1);
        }));
        tests.Add(("apple source recovery rejects changed original before reading another file",()=>
        {
            var bytes=Database("DELETE FROM ZASSET WHERE Z_PK=4");int connects=0;
            using var source=new AppleGallerySource(new("synthetic-device","Synthetic"),null,_=>
            {var fs=new MemoryFiles(bytes);fs.Files["/DCIM/100APPLE/IMG_2.MOV"]=++connects==1?[1,2,3]:[1,2,3,4];return fs;},ImportMedia.Videos);
            var video=source.Inventory(default).Items.Single(i=>i.Kind==MediaKind.Video);source.InvalidateRead();
            Fixtures.Throws<PreviousReadCleanupException>(()=>source.OpenRead(video,default));
            Fixtures.Throws<PreviousReadCleanupException>(()=>source.OpenRead(video,default));
            Fixtures.Check(connects==2,"A rejected recovery must not reconnect using a cleared baseline.");
        }));
        tests.Add(("apple final recovery failure leaves unopened siblings pending and outer retry succeeds",()=>
        {
            byte[] database=ThreeVideos(),video=Fixtures.Video(710);int attempts=0,firstAttemptOpens=0;
            IVideoSource Create()
            {
                int attempt=++attempts,connections=0;
                return new AppleGallerySource(new("synthetic-device","Synthetic"),null,_=>
                {
                    if(++connections>1&&attempt==1)throw new IOException("Synthetic recovery connection failure");
                    var fs=VideoFiles(database,video);
                    if(attempt==1)fs.OnOpen=(path,_)=>
                    {if(path.StartsWith("/DCIM/",StringComparison.Ordinal)){firstAttemptOpens++;throw new IOException("Synthetic original read failure");}return null;};
                    return fs;
                },ImportMedia.Videos);
            }
            using var destination=new Destination(Fixtures.Root());
            using(var source=Create())
            {
                var report=new ImportEngine(source,destination){Media=ImportMedia.Videos}.Run(false,default);
                Fixtures.Check(firstAttemptOpens==1&&report.CurrentCount(Outcome.Error)==1&&report.CurrentCount(Outcome.Pending)==2);
                Fixtures.Check(report.ReadRecoveryInterrupted&&report.RetryRecommended&&report.RetryLast.Count==1);
            }
            attempts=0;firstAttemptOpens=0;
            var session=new ImportSession(Create,destination){Media=ImportMedia.Videos,RetryDelay=TimeSpan.Zero};
            var retried=session.Run(false,default);
            Fixtures.Check(attempts==2&&session.Attempts==2&&firstAttemptOpens==1&&retried.GallerySelectionCopiesVerified&&Directory.GetFiles(destination.Videos).Length==3);
        }));
        tests.Add(("apple final recovery cancellation propagates without blaming the unopened file",()=>
        {
            byte[] database=ThreeVideos();int connections=0;using var cancellation=new CancellationTokenSource();
            using var source=new AppleGallerySource(new("synthetic-device","Synthetic"),null,_=>
            {if(++connections>1){cancellation.Cancel();throw new OperationCanceledException(cancellation.Token);}return VideoFiles(database,Fixtures.Video(711));},ImportMedia.Videos);
            var item=source.Inventory(default).Items.First(i=>i.Kind==MediaKind.Video);source.InvalidateRead();
            Fixtures.Throws<OperationCanceledException>(()=>source.OpenRead(item,cancellation.Token));
        }));
        tests.Add(("apple final recoverable stat error preserves uncertain asset and copies healthy siblings",()=>
        {
            var fs=VideoFiles(ThreeVideos(),Fixtures.Video(712));
            fs.OnStat=path=>{if(path.EndsWith("IMG_2.MOV",StringComparison.Ordinal))throw new IOException("Synthetic metadata failure");};
            using var source=new AppleGallerySource(new("synthetic-device","Synthetic"),null,_=>fs,ImportMedia.Videos);
            using var destination=new Destination(Fixtures.Root());
            var report=new ImportEngine(source,destination){Media=ImportMedia.Videos}.Run(false,default);
            Fixtures.Check(report.CandidateCount==3&&report.CurrentCount(Outcome.Imported)==2&&report.CurrentCount(Outcome.Missing)==1);
            Fixtures.Check(!report.GallerySelectionCopiesVerified&&report.GalleryCoverage?.Unresolved==1&&Directory.GetFiles(destination.Videos).Length==2);
            Fixtures.Check(report.InventoryErrors.Any(error=>error.Contains("metadati",StringComparison.Ordinal))&&
                !report.InventoryErrors.Any(error=>error.Contains("Synthetic",StringComparison.Ordinal)||error.Contains("IMG_2",StringComparison.Ordinal)||error.Contains("originale locale accessibile",StringComparison.Ordinal)));
            Fixtures.Check(!fs.Opened.Any(path=>path.EndsWith("IMG_2.MOV",StringComparison.Ordinal)));
        }));
        tests.Add(("apple final poisoned stat failure aborts inventory instead of accepting partial siblings",()=>
        {
            var fs=VideoFiles(ThreeVideos(),Fixtures.Video(713));
            fs.OnStat=path=>{if(path.EndsWith("IMG_2.MOV",StringComparison.Ordinal)){fs.IsUsable=false;throw new IOException("Synthetic poisoned connection");}};
            using var source=new AppleGallerySource(new("synthetic-device","Synthetic"),null,_=>fs,ImportMedia.Videos);
            Fixtures.Throws<IOException>(()=>source.Inventory(default));
            Fixtures.Check(source.Coverage==null&&source.CurrentCatalog==null&&!fs.Stats.Any(path=>path.EndsWith("IMG_7.MOV",StringComparison.Ordinal)));
        }));
        tests.Add(("apple capture rejects changing database and active rollback journal",()=>
        {
            byte[] first=Database(),second=Database("UPDATE ZGENERICALBUM SET ZTITLE='Changed'");
            var fs=new MemoryFiles(first){OnOpen=(path,count)=>path=="/PhotoData/Photos.sqlite"&&count>1?second:null};
            Fixtures.Throws<IOException>(()=>AppleCatalogCapture.Read(fs,default));
            var journal=new MemoryFiles(first);journal.Files["/PhotoData/Photos.sqlite-journal"]=[1];
            Fixtures.Throws<IOException>(()=>AppleCatalogCapture.Read(journal,default));
        }));
        tests.Add(("apple rc13 capture reads committed native SQLite WAL with uncommitted tail",()=>
        {
            var (database,journal)=JournalDatabase(true);
            var fs=new MemoryFiles(database);fs.Files["/PhotoData/Photos.sqlite-wal"]=journal;
            var catalog=AppleCatalogCapture.Read(fs,default);
            Fixtures.Check(catalog.Albums.Single().Name=="Committed native WAL"&&catalog.VisibleVideoCount==1);
            Fixtures.Check(!catalog.AtomicSnapshotVerified);
        }));
        tests.Add(("apple rc13 capture accepts settled native SQLite persist journal",()=>
        {
            var (database,journal)=JournalDatabase(false);
            Fixtures.Check(journal.Length>28&&journal.AsSpan(0,28).IndexOfAnyExcept((byte)0)<0);
            var fs=new MemoryFiles(database);fs.Files["/PhotoData/Photos.sqlite-journal"]=journal;
            var catalog=AppleCatalogCapture.Read(fs,default);
            Fixtures.Check(catalog.Albums.Single().Name=="Committed native journal");
        }));
        tests.Add(("apple rc13 capture rejects active unknown truncated or changing journal headers",()=>
        {
            byte[] active=new byte[28];new byte[]{0xd9,0xd5,0x05,0xf9,0x20,0xa1,0x63,0xd7}.CopyTo(active,0);
            byte[] unknown=new byte[28];unknown[27]=1;
            foreach(byte[] header in new[]{active,unknown,new byte[27]})
            {
                var fs=new MemoryFiles(Database());fs.Files["/PhotoData/Photos.sqlite-journal"]=header;
                Fixtures.Throws<IOException>(()=>AppleCatalogCapture.Read(fs,default));
            }
            var changing=new MemoryFiles(Database()){OnOpen=(path,count)=>path.EndsWith("-journal",StringComparison.Ordinal)&&count>1?active:null};
            changing.Files["/PhotoData/Photos.sqlite-journal"]=new byte[28];
            Fixtures.Throws<IOException>(()=>AppleCatalogCapture.Read(changing,default));
        }));
        tests.Add(("apple rc13 visibility nonzero null and new states preserve membership without opening originals",()=>
        {
            foreach(string state in new[]{"2","999","NULL"})
            {
                var fs=new MemoryFiles(Database("ALTER TABLE ZASSET ADD COLUMN ZVISIBILITYSTATE INTEGER DEFAULT 0; UPDATE ZASSET SET ZVISIBILITYSTATE="+state+" WHERE Z_PK=1;"));
                fs.Files["/DCIM/100APPLE/IMG_1.HEIC"]=[1,2,3];
                using var source=new AppleGallerySource(new("synthetic-device","Synthetic"),"album-one",_=>fs,ImportMedia.Photos);
                var inventory=source.Inventory(default);var uncertain=inventory.Items.Single(i=>i.PersistentId=="photo-live");
                Fixtures.Check(inventory.Items.Count==2&&uncertain.Kind==MediaKind.Image&&uncertain.Offline&&source.Coverage!.Unresolved==2);
                Fixtures.Check(source.CurrentCatalog!.Select("album-one").Count==2&&source.CurrentCatalog.UnresolvedAssetCount==3);
                Fixtures.Check(inventory.Errors.Any(error=>error.Contains("visibilità",StringComparison.Ordinal))&&source.CurrentCatalog.ScopeNote.Contains("1 elementi",StringComparison.Ordinal));
                Fixtures.Check(!fs.Stats.Any(path=>path.StartsWith("/DCIM/",StringComparison.Ordinal)));
                Fixtures.Throws<IOException>(()=>source.OpenRead(uncertain,default));
            }
        }));
        tests.Add(("apple rc13 visibility zero and missing-column profiles keep original selection identity",()=>
        {
            SourceItem ReadVideo(string extra)
            {
                var fs=new MemoryFiles(Database("DELETE FROM ZASSET WHERE Z_PK=4;"+extra));fs.Files["/DCIM/100APPLE/IMG_2.MOV"]=[1,2,3];
                using var source=new AppleGallerySource(new("synthetic-device","Synthetic"),null,_=>fs,ImportMedia.Videos);
                var inventory=source.Inventory(default);Fixtures.Check(inventory.EnumerationComplete&&source.Coverage!.Unresolved==0);
                if(extra.Length==0)Fixtures.Check(source.CurrentCatalog!.ScopeNote.Contains("Indicatore di visibilità non disponibile",StringComparison.Ordinal));
                return inventory.Items.Single(i=>i.Kind==MediaKind.Video);
            }
            Fixtures.Check(ReadVideo("")==ReadVideo("ALTER TABLE ZASSET ADD COLUMN ZVISIBILITYSTATE INTEGER DEFAULT 0;"));
        }));
        tests.Add(("apple rc13 visibility uncertainty of excluded photos does not block video selection",()=>
        {
            var fs=new MemoryFiles(Database("DELETE FROM ZASSET WHERE Z_PK=4; ALTER TABLE ZASSET ADD COLUMN ZVISIBILITYSTATE INTEGER DEFAULT 0; UPDATE ZASSET SET ZVISIBILITYSTATE=2 WHERE ZKIND=0;"));
            fs.Files["/DCIM/100APPLE/IMG_2.MOV"]=[1,2,3];
            using var source=new AppleGallerySource(new("synthetic-device","Synthetic"),null,_=>fs,ImportMedia.Videos);
            var inventory=source.Inventory(default);
            Fixtures.Check(inventory.EnumerationComplete&&inventory.Errors.Count==0&&source.Coverage!.Unresolved==0);
            Fixtures.Check(fs.Stats.Count(path=>path.StartsWith("/DCIM/",StringComparison.Ordinal))==1);
        }));
        tests.Add(("apple rc13 visibility change rejects read recovery before opening original",()=>
        {
            int connections=0;MemoryFiles? latest=null;
            using var source=new AppleGallerySource(new("synthetic-device","Synthetic"),null,_=>
            {
                latest=new MemoryFiles(Database("DELETE FROM ZASSET WHERE Z_PK=4; ALTER TABLE ZASSET ADD COLUMN ZVISIBILITYSTATE INTEGER DEFAULT 0;"+
                    (++connections>1?"UPDATE ZASSET SET ZVISIBILITYSTATE=2 WHERE Z_PK=2;":"")));
                latest.Files["/DCIM/100APPLE/IMG_2.MOV"]=[1,2,3];return latest;
            },ImportMedia.Videos);
            var item=source.Inventory(default).Items.Single(i=>i.Kind==MediaKind.Video);source.InvalidateRead();
            Fixtures.Throws<PreviousReadCleanupException>(()=>source.OpenRead(item,default));
            Fixtures.Check(connections==2&&!latest!.Opened.Any(path=>path.StartsWith("/DCIM/",StringComparison.Ordinal)));
        }));
        tests.Add(("apple rc13 visibility uncertainty is ambiguous while healthy video siblings copy",()=>
        {
            var fs=VideoFiles(ThreeVideosWithVisibility(),Fixtures.Video(714));
            using var source=new AppleGallerySource(new("synthetic-device","Synthetic"),null,_=>fs,ImportMedia.Videos);
            using var destination=new Destination(Fixtures.Root());
            var report=new ImportEngine(source,destination){Media=ImportMedia.Videos}.Run(false,default);
            Fixtures.Check(report.CandidateCount==3&&report.CurrentCount(Outcome.Imported)==2&&report.CurrentCount(Outcome.Ambiguous)==1&&report.CurrentCount(Outcome.Missing)==0);
            Fixtures.Check(report.GalleryCoverage?.Unresolved==1&&!report.GallerySelectionCopiesVerified&&!report.EntireLibraryVerified);
            Fixtures.Check(report.InventoryErrors.Any(error=>error.Contains("visibilità",StringComparison.Ordinal))&&!fs.Opened.Any(path=>path.EndsWith("IMG_2.MOV",StringComparison.Ordinal)));
            Fixtures.Check(report.Items.Any(item=>item.Outcome==Outcome.Ambiguous&&item.Detail.Contains("Visibilità",StringComparison.Ordinal)));
        }));
        tests.Add(("apple rc13 visibility change in final catalog prevents verified selection",()=>
        {
            int connections=0;
            using var source=new AppleGallerySource(new("synthetic-device","Synthetic"),null,_=>
                VideoFiles(ThreeVideosWithVisibility(++connections==1?0:2),Fixtures.Video(715)),ImportMedia.Videos);
            using var destination=new Destination(Fixtures.Root());
            var report=new ImportEngine(source,destination){Media=ImportMedia.Videos}.Run(false,default);
            Fixtures.Check(report.CurrentCount(Outcome.Imported)==3&&!report.FinalInventoryChecked&&!report.GallerySelectionCopiesVerified&&!report.EntireLibraryVerified);
            Fixtures.Check(report.InventoryErrors.Any(error=>error.Contains("visibilità",StringComparison.Ordinal)));
        }));
    }

    private static byte[] ThreeVideosWithVisibility(int state=2)=>Database("DELETE FROM ZASSET WHERE Z_PK=4; INSERT INTO ZASSET VALUES(7,'second-video','DCIM/100APPLE','IMG_7.MOV',1,0,0,0),(8,'third-video','DCIM/100APPLE','IMG_8.MOV',1,0,0,0); ALTER TABLE ZASSET ADD COLUMN ZVISIBILITYSTATE INTEGER DEFAULT 0; UPDATE ZASSET SET ZVISIBILITYSTATE="+state+" WHERE Z_PK=2;");

    private static (byte[] Database,byte[] Journal) JournalDatabase(bool wal)
    {
        string path=Path.Combine(Path.GetTempPath(),"porto-apple-native-journal-"+Guid.NewGuid().ToString("N")+".sqlite");
        nint db=0;
        try
        {
            File.WriteAllBytes(path,Database());
            Fixtures.Check(Native.sqlite3_open_v2(path,out db,6,0)==0);
            string sql=wal?
                "PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0; UPDATE ZGENERICALBUM SET ZTITLE='Committed native WAL'; PRAGMA cache_size=1; BEGIN IMMEDIATE; UPDATE ZGENERICALBUM SET ZTITLE='Uncommitted'; CREATE TABLE UNCOMMITTED_DATA(B BLOB); INSERT INTO UNCOMMITTED_DATA VALUES(zeroblob(65536));":
                "PRAGMA journal_mode=PERSIST; UPDATE ZGENERICALBUM SET ZTITLE='Committed native journal';";
            Fixtures.Check(Native.sqlite3_exec(db,sql,0,0,out var error)==0);
            if(error!=0)Native.sqlite3_free(error);
            byte[] ReadShared(string file)
            {
                using var input=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
                using var output=new MemoryStream();input.CopyTo(output);return output.ToArray();
            }
            return(ReadShared(path),ReadShared(path+(wal?"-wal":"-journal")));
        }
        finally
        {
            if(db!=0)Native.sqlite3_close(db);
            foreach(string suffix in new[]{"","-wal","-shm","-journal"})File.Delete(path+suffix);
        }
    }

    private sealed class MemoryFiles(byte[] database) : IAppleFileSystem
    {
        public bool IsUsable {get;set;}=true;
        internal Dictionary<string,byte[]> Files {get;}=new(StringComparer.Ordinal){{"/PhotoData/Photos.sqlite",database}};
        internal List<string> Stats {get;}=[];
        internal Func<string,int,byte[]?>? OnOpen {get;set;}
        internal Action<string>? OnStat {get;set;}
        internal List<string> Opened {get;}=[];
        private readonly Dictionary<string,int> opens=new(StringComparer.Ordinal);
        public AppleFileInfo? Stat(string path,CancellationToken cancel){cancel.ThrowIfCancellationRequested();Stats.Add(path);OnStat?.Invoke(path);return Files.TryGetValue(path,out var data)?new(data.Length,"1",true):null;}
        public Stream OpenRead(string path,CancellationToken cancel)
        {cancel.ThrowIfCancellationRequested();Opened.Add(path);int number=opens.GetValueOrDefault(path)+1;opens[path]=number;return new MemoryStream(OnOpen?.Invoke(path,number)??Files[path],false);}
        public void Dispose(){}
    }
    private static byte[] ThreeVideos()=>Database("DELETE FROM ZASSET WHERE Z_PK=4; INSERT INTO ZASSET VALUES(7,'second-video','DCIM/100APPLE','IMG_7.MOV',1,0,0,0),(8,'third-video','DCIM/100APPLE','IMG_8.MOV',1,0,0,0);");
    private static MemoryFiles VideoFiles(byte[] database,byte[] video)
    {
        var files=new MemoryFiles(database);
        foreach(string name in new[]{"IMG_2.MOV","IMG_7.MOV","IMG_8.MOV"})files.Files["/DCIM/100APPLE/"+name]=video;
        return files;
    }

    internal static byte[] Database(string? extra=null)
    {
        string path=Path.Combine(Path.GetTempPath(),"porto-apple-synthetic-"+Guid.NewGuid().ToString("N")+".sqlite");
        nint db=0;
        try
        {
            Fixtures.Check(Native.sqlite3_open_v2(path,out db,6,0)==0);
            string sql="""
                CREATE TABLE ZASSET(Z_PK INTEGER PRIMARY KEY,ZUUID TEXT,ZDIRECTORY TEXT,ZFILENAME TEXT,ZKIND INTEGER,ZKINDSUBTYPE INTEGER,ZHIDDEN INTEGER,ZTRASHEDSTATE INTEGER);
                CREATE TABLE ZGENERICALBUM(Z_PK INTEGER PRIMARY KEY,ZUUID TEXT,ZTITLE TEXT,ZKIND INTEGER,ZTRASHEDSTATE INTEGER);
                CREATE TABLE Z_26ASSETS(Z_26ALBUMS INTEGER,Z_3ASSETS INTEGER);
                INSERT INTO ZASSET VALUES(1,'photo-live','DCIM/100APPLE','IMG_1.HEIC',0,2,0,0);
                INSERT INTO ZASSET VALUES(2,'video','DCIM/100APPLE','IMG_2.MOV',1,0,0,0);
                INSERT INTO ZASSET VALUES(3,'missing',NULL,NULL,0,0,0,0);
                INSERT INTO ZASSET VALUES(4,'unknown','DCIM/100APPLE','IMG_4.DAT',9,0,0,0);
                INSERT INTO ZASSET VALUES(5,'hidden','DCIM/100APPLE','IMG_5.JPG',0,0,1,0);
                INSERT INTO ZASSET VALUES(6,'deleted','DCIM/100APPLE','IMG_6.MOV',1,0,0,1);
                INSERT INTO ZGENERICALBUM VALUES(1,'album-one','Vacanze',2,0);
                INSERT INTO Z_26ASSETS VALUES(1,1);
                INSERT INTO Z_26ASSETS VALUES(1,3);
                """;
            Fixtures.Check(Native.sqlite3_exec(db,sql+(extra??""),0,0,out var error)==0);
            if(error!=0)Native.sqlite3_free(error);
            Native.sqlite3_close(db);db=0;
            return File.ReadAllBytes(path);
        }
        finally {if(db!=0)Native.sqlite3_close(db);File.Delete(path);}
    }
    private static class Native
    {
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)]string path,out nint db,int flags,nint vfs);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_close(nint db);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_exec(nint db,[MarshalAs(UnmanagedType.LPUTF8Str)]string sql,nint callback,nint arg,out nint error);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern void sqlite3_free(nint value);
    }
}
