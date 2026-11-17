using Porto.Apple;

internal static class AppleCatalogStreamingTests
{
    internal static void Add(List<(string,Action)> tests)
    {
        tests.Add(("apple streaming capture compares every byte with bounded cleared scratch",()=>
        {
            byte[] data=AppleGalleryTests.Database("CREATE TABLE PADDING(DATA BLOB); WITH RECURSIVE n(x) AS(SELECT 1 UNION ALL SELECT x+1 FROM n WHERE x<1500) INSERT INTO PADDING SELECT zeroblob(4096) FROM n;");
            using var files=new Files(data){SecondReadLimit=65521};
            var catalog=AppleCatalogCapture.Read(files,default);
            Fixtures.Check(catalog.VisibleVideoCount==1&&files.SecondBytes==data.Length);
            Fixtures.Check(files.SecondBuffers.Count>1&&files.SecondBuffers.All(buffer=>buffer.Length<=1024*1024),"Second read allocated an entire database");
            Fixtures.Check(files.SecondBuffers.All(buffer=>buffer.AsSpan().IndexOfAnyExcept((byte)0)<0),"Comparison buffer was not cleared");
        }));
        foreach(string fault in new[]{"byte-change","truncate","grow"})
        {
            tests.Add(("apple streaming capture rejects second database "+fault,()=>
            {
                byte[] data=AppleGalleryTests.Database();
                using var files=new Files(data){Fault=fault};
                Fixtures.Throws<IOException>(()=>AppleCatalogCapture.Read(files,default));
                Fixtures.Check(files.SecondBuffers.Count>0&&files.SecondBuffers.All(buffer=>buffer.AsSpan().IndexOfAnyExcept((byte)0)<0));
            }));
        }
        tests.Add(("apple streaming capture rejects WAL appearance disappearance and modification",()=>
        {
            foreach(string mutation in new[]{"appear","disappear","mtime"})
            {
                using var files=new Files(AppleGalleryTests.Database()){Wal=mutation=="appear"?null:[],WalMutation=mutation};
                Fixtures.Throws<IOException>(()=>AppleCatalogCapture.Read(files,default));
                Fixtures.Check(files.SecondBytes==0,"Metadata mismatch must reject before second file read");
            }
        }));
        tests.Add(("apple streaming capture rejects second WAL byte change even when metadata is stable",()=>
        {
            using var files=new Files(AppleGalleryTests.Database()){Wal=[1,2,3,4],Fault="wal-byte-change"};
            IOException? error=null;
            try{AppleCatalogCapture.Read(files,default);}catch(IOException failure){error=failure;}
            Fixtures.Check(error!=null&&error.Message.Contains("cambiato durante la lettura",StringComparison.Ordinal));
        }));
        tests.Add(("apple streaming capture checks metadata after final verification byte",()=>
        {
            using var files=new Files(AppleGalleryTests.Database()){ChangeAtSecondEof=true};
            Fixtures.Throws<IOException>(()=>AppleCatalogCapture.Read(files,default));
            Fixtures.Check(files.SecondBytes==files.Data.Length);
        }));
        tests.Add(("apple streaming capture rejects active journal after database verification",()=>
        {
            using var files=new Files(AppleGalleryTests.Database()){Journal=new byte[28],ActivateJournal=true};
            Fixtures.Throws<IOException>(()=>AppleCatalogCapture.Read(files,default));
            Fixtures.Check(files.SecondBytes==files.Data.Length);
        }));
        tests.Add(("apple streaming capture cancellation closes second stream and clears scratch",()=>
        {
            using var cancel=new CancellationTokenSource();
            using var files=new Files(AppleGalleryTests.Database()){CancelOnSecondRead=cancel};
            Fixtures.Throws<OperationCanceledException>(()=>AppleCatalogCapture.Read(files,cancel.Token));
            Fixtures.Check(files.SecondDisposed&&files.SecondBuffers.Count>0&&files.SecondBuffers.All(buffer=>buffer.AsSpan().IndexOfAnyExcept((byte)0)<0));
        }));
    }

    private const string Database="/PhotoData/Photos.sqlite";
    private sealed class Files(byte[] data):IAppleFileSystem
    {
        internal byte[] Data=>data;
        internal byte[]? Wal,Journal;
        internal string? Fault,WalMutation;
        internal bool ChangeAtSecondEof,ActivateJournal,SecondDisposed;
        internal int SecondReadLimit=int.MaxValue;
        internal long SecondBytes;
        internal CancellationTokenSource? CancelOnSecondRead;
        internal readonly List<byte[]> SecondBuffers=[];
        private int dbOpens,walOpens,walStats;
        private bool changed,eof;
        public AppleFileInfo? Stat(string path,CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            if(path==Database)return new(data.Length,changed?"2":"1",true);
            if(path==Database+"-journal")return Journal==null?null:new(Journal.Length,"1",true);
            if(path==Database+"-wal")
            {
                walStats++;
                if(walStats>=3&&WalMutation!=null)return WalMutation switch
                {"appear"=>new(0,"1",true),"disappear"=>null,_=>new(Wal!.Length,"2",true)};
                return Wal==null?null:new(Wal.Length,"1",true);
            }
            throw new InvalidOperationException("Unexpected path");
        }
        public Stream OpenRead(string path,CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            if(path==Database)
            {
                bool second=++dbOpens==2;
                byte[] current=data;
                if(second&&Fault=="byte-change"){current=(byte[])data.Clone();current[^1]^=1;}
                if(second&&Fault=="truncate")current=data[..^1];
                if(second&&Fault=="grow")current=[..data,1];
                return new ObservedStream(current,this,second);
            }
            if(path==Database+"-wal")
            {
                byte[] current=Wal!;
                if(++walOpens==2&&Fault=="wal-byte-change"){current=(byte[])current.Clone();current[^1]^=1;}
                return new MemoryStream(current,false);
            }
            if(path==Database+"-journal")
            {
                byte[] current=Journal!;
                if(eof&&ActivateJournal){current=(byte[])current.Clone();current[0]=1;}
                return new MemoryStream(current,false);
            }
            throw new InvalidOperationException("Unexpected path");
        }
        public void Dispose(){}
        private sealed class ObservedStream(byte[] bytes,Files files,bool second):MemoryStream(bytes,false)
        {
            public override int Read(byte[] buffer,int offset,int count)
            {
                if(second){files.SecondBuffers.Add(buffer);count=Math.Min(count,files.SecondReadLimit);}
                int read=base.Read(buffer,offset,count);
                if(second){files.SecondBytes+=read;files.CancelOnSecondRead?.Cancel();}
                return read;
            }
            public override int ReadByte()
            {
                int result=base.ReadByte();
                if(second&&result==-1){files.eof=true;files.changed=files.ChangeAtSecondEof;}
                return result;
            }
            protected override void Dispose(bool disposing){if(second)files.SecondDisposed=true;base.Dispose(disposing);}
        }
    }
}
