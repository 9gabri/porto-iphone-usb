using Porto.Apple;
using Porto.Core;
using System.Diagnostics;
using System.Buffers.Binary;

internal static class Rc23CatalogReuseTests
{
    internal static void Add(List<(string,Action)> tests)
    {
        tests.Add(("rc23 repeated validated catalog needs one complete fresh read",()=>
        {
            using var files=new Files(AppleGalleryTests.Database());
            using var source=new AppleGallerySource(new("synthetic-device","Synthetic"),null,_=>files);
            source.Inventory(default);int before=files.Opens;
            source.Inventory(default);
            Fixtures.Check(files.Opens-before==1,"Unchanged catalog still reread twice");
        }));
        tests.Add(("rc23 catalog changed with identical size timestamp and header is revalidated",()=>
        {
            var cache=new AppleCatalogCache();using var files=new Files(AppleGalleryTests.Database());
            var before=AppleCatalogCapture.Read(files,default,cache,"one");
            byte[] changed=AppleGalleryTests.Database("UPDATE ZASSET SET ZKIND=0 WHERE ZKIND=1");
            Fixtures.Check(changed.Length==files.Data.Length);
            files.Data.AsSpan(0,100).CopyTo(changed);files.Data=changed;int opens=files.Opens;
            var after=AppleCatalogCapture.Read(files,default,cache,"one");
            Fixtures.Check(after.VisibleVideoCount==0&&before.VisibleVideoCount==1&&files.Opens-opens==2);
        }));
        tests.Add(("rc23 cache is scoped to device and cleared explicitly",()=>
        {
            var cache=new AppleCatalogCache();using var files=new Files(AppleGalleryTests.Database());
            AppleCatalogCapture.Read(files,default,cache,"one");int start=files.Opens;
            AppleCatalogCapture.Read(files,default,cache,"two");Fixtures.Check(files.Opens-start==2);
            cache.Clear();start=files.Opens;AppleCatalogCapture.Read(files,default,cache,"two");Fixtures.Check(files.Opens-start==2);
        }));
        foreach(string fault in new[]{"truncate","grow","corrupt","cancel","metadata","journal","wal"})
        {
            tests.Add(("rc23 cached catalog rejects "+fault+" and cannot retain successful validation",()=>
            {
                var cache=new AppleCatalogCache();using var files=new Files(AppleGalleryTests.Database());
                AppleCatalogCapture.Read(files,default,cache,"one");byte[] original=(byte[])files.Data.Clone();
                files.Ended=false;files.Fault=fault;using var cancel=new CancellationTokenSource();files.Cancel=cancel;
                if(fault=="corrupt")files.Data[100]=0;
                bool failed=false;
                try{AppleCatalogCapture.Read(files,cancel.Token,cache,"one");}
                catch(Exception error)when(error is IOException or OperationCanceledException){failed=true;}
                Fixtures.Check(failed,"Invalid fresh data reused stale catalog");
                files.Data=original;files.Fault=null;files.Cancel=null;files.Ended=false;int start=files.Opens;
                AppleCatalogCapture.Read(files,default,cache,"one");Fixtures.Check(files.Opens-start==2,"Failure left a reusable validation");
            }));
        }
        tests.Add(("rc23 WAL presence and metadata changes trigger full validation",()=>
        {
            var cache=new AppleCatalogCache();using var files=new Files(AppleGalleryTests.Database());
            AppleCatalogCapture.Read(files,default,cache,"one");files.EmptyWal=true;int before=files.Opens;
            AppleCatalogCapture.Read(files,default,cache,"one");Fixtures.Check(files.Opens-before==4);
            files.Modification="2";before=files.Opens;
            AppleCatalogCapture.Read(files,default,cache,"one");Fixtures.Check(files.Opens-before==4);
            before=files.Opens;var metrics=new SourceTransportMetrics();
            AppleCatalogCapture.Read(files,default,cache,"one",metrics);
            Fixtures.Check(files.Opens-before==2&&metrics.CatalogValidatedReuses==1&&metrics.CatalogFullValidations==0);
        }));
        tests.Add(("rc23 album preview initial inventory and final inventory read four catalogs instead of six",()=>
        {
            var cache=new AppleCatalogCache();using var files=new Files(AppleGalleryTests.Database());
            AppleCatalogCapture.Read(files,default,cache,"one");
            using var source=new AppleGallerySource(new("one","Synthetic"),"album-one",_=>files,catalogCache:cache);
            var first=source.Inventory(default);var last=source.Inventory(default);
            Fixtures.Check(first.Items.SequenceEqual(last.Items)&&files.Opens==4&&files.Bytes==4L*files.Data.Length);
            Fixtures.Check(source.TransportMetrics.CatalogValidatedReuses==2);
        }));
        tests.Add(("rc23 WAL-only album change with identical metadata cannot reuse old members",()=>
        {
            using var files=new Files(AppleGalleryTests.Database());var cache=new AppleCatalogCache();
            files.Wal=FullWal(files.Data);AppleCatalogCapture.Read(files,default,cache,"one");
            byte[] changed=AppleGalleryTests.Database("DELETE FROM Z_26ASSETS WHERE Z_3ASSETS=3");
            files.Data.AsSpan(0,100).CopyTo(changed);
            byte[] next=FullWal(changed);Fixtures.Check(next.Length==files.Wal.Length);files.Wal=next;
            int before=files.Opens;var result=AppleCatalogCapture.Read(files,default,cache,"one");
            Fixtures.Check(result.Select("album-one").Count==1&&files.Opens-before==4,"WAL change reused old members");
        }));
        tests.Add(("rc23 unchanged catalog still refreshes media availability and size",()=>
        {
            using var files=new Files(AppleGalleryTests.Database("INSERT INTO Z_26ASSETS VALUES(1,2)")){Video=new(123,"1",true)};
            using var source=new AppleGallerySource(new("one","Synthetic"),"album-one",_=>files,ImportMedia.Videos);
            var first=source.Inventory(default).Items.Single(i=>i.Kind==MediaKind.Video);Fixtures.Check(!first.Offline&&first.Size==123);
            files.Video=null;var missing=source.Inventory(default).Items.Single(i=>i.Kind==MediaKind.Video);
            Fixtures.Check(missing.Offline&&source.TransportMetrics.CatalogValidatedReuses==1);
            files.Video=new(456,"2",true);var changed=source.Inventory(default).Items.Single(i=>i.Kind==MediaKind.Video);
            Fixtures.Check(!changed.Offline&&changed.Size==456&&changed!=first&&source.TransportMetrics.CatalogValidatedReuses==2);
        }));
    }
    private static byte[] FullWal(byte[] database)
    {
        int page=AppleSqliteImage.ValidateDatabase(database),count=database.Length/page;
        byte[] wal=new byte[32+count*(page+24)];uint a=0,b=0;
        void Put(int offset,uint value)=>BinaryPrimitives.WriteUInt32BigEndian(wal.AsSpan(offset),value);
        void Sum(int offset,int length)
        {unchecked{for(int i=offset;i<offset+length;i+=8){a+=BinaryPrimitives.ReadUInt32LittleEndian(wal.AsSpan(i))+b;b+=BinaryPrimitives.ReadUInt32LittleEndian(wal.AsSpan(i+4))+a;}}}
        Put(0,0x377f0682);Put(4,3007000);Put(8,(uint)page);Put(16,123);Put(20,456);Sum(0,24);Put(24,a);Put(28,b);
        for(int n=0;n<count;n++)
        {
            int offset=32+n*(page+24);Put(offset,(uint)n+1);Put(offset+4,n==count-1?(uint)count:0);Put(offset+8,123);Put(offset+12,456);
            database.AsSpan(n*page,page).CopyTo(wal.AsSpan(offset+24));Sum(offset,8);Sum(offset+24,page);Put(offset+16,a);Put(offset+20,b);
        }
        return wal;
    }
    internal static void AddBench(List<(string,Action)> tests)
    {
        tests.Add(("rc23 benchmark three complete catalog observations",()=>
        {
            byte[] data=AppleGalleryTests.Database("CREATE TABLE PADDING(DATA BLOB); WITH RECURSIVE n(x) AS (SELECT 1 UNION ALL SELECT x+1 FROM n WHERE x<256) INSERT INTO PADDING SELECT zeroblob(65536) FROM n;");
            // Same fixture/read size/delay; only verified reuse changes. Alternating order.
            for(int run=0;run<3;run++)foreach(bool enabled in run%2==0?new[]{false,true}:new[]{true,false})
            {
                using var files=new Files(data){Delay=2,Chunk=256*1024};
                var cache=enabled?new AppleCatalogCache():null;var timer=Stopwatch.StartNew();
                for(int n=0;n<3;n++)Fixtures.Check(AppleCatalogCapture.Read(files,default,cache,enabled?"one":null).VisibleVideoCount==1);
                timer.Stop();Fixtures.Check(files.Bytes==(enabled?4L:6L)*data.Length);
                Console.WriteLine($"MEASURE rc23 run={run+1}; reuse={enabled}; DB={data.Length}; bytes={files.Bytes}; opens={files.Opens}; elapsed_ms={timer.Elapsed.TotalMilliseconds:F2}; artificial_delay_ms=2; chunk=262144");
            }
        }));
    }
    internal sealed class Files(byte[] data):IAppleFileSystem
    {
        internal byte[] Data=data;
        internal int Opens;
        internal long Bytes;
        internal string? Fault;
        internal string Modification="1";
        internal CancellationTokenSource? Cancel;
        internal bool Ended,EmptyWal;
        internal byte[]? Wal;
        internal AppleFileInfo? Video;
        internal int Delay,Chunk=int.MaxValue;
        public AppleFileInfo? Stat(string path,CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            if(path=="/DCIM/100APPLE/IMG_2.MOV")return Video;
            if(path=="/PhotoData/Photos.sqlite")return new(Data.Length,Fault=="metadata"&&Ended?"changed":Modification,true);
            if(path.EndsWith("-journal")&&Fault=="journal")return new(28,"1",true);
            if(path.EndsWith("-wal")&&(Wal!=null||EmptyWal||Fault=="wal"))return new(Wal?.Length??(Fault=="wal"?1:0),"1",true);
            return null;
        }
        public Stream OpenRead(string path,CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();Opens++;
            if(path.EndsWith("-journal"))return new MemoryStream(Enumerable.Repeat((byte)1,28).ToArray(),false);
            if(path.EndsWith("-wal"))return new MemoryStream(Wal??(Fault=="wal"?[1]:[]),false);
            if(path!="/PhotoData/Photos.sqlite")throw new IOException("Unexpected fixture read");
            return new Counter(Fault=="truncate"?Data[..^1]:Fault=="grow"?[..Data,1]:Data,this);
        }
        public void Dispose(){}
        private sealed class Counter(byte[] bytes,Files files):MemoryStream(bytes,false)
        {
            public override int Read(byte[] b,int offset,int count)
            {if(files.Delay>0)Thread.Sleep(files.Delay);int n=base.Read(b,offset,Math.Min(count,files.Chunk));files.Bytes+=n;if(files.Fault=="cancel")files.Cancel!.Cancel();return n;}
            public override int ReadByte(){int n=base.ReadByte();if(n==-1)files.Ended=true;return n;}
        }
    }
}
