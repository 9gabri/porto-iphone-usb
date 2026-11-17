using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Porto.Apple;
using Porto.Core;

internal static class Rc21LargeCatalogTests
{
    internal static void AddLarge(List<(string,Action)> tests)
    {
        tests.Add(("rc21 large catalog full capture above 1.42 GB with reported WAL length",()=>
        {
            // New synthetic SQLite file, retained for inspection. The fake phone streams from
            // disk rather than holding another 1.42 GB source array alongside the product.
            string path=Path.Combine(Fixtures.Root(),"synthetic-catalog.sqlite");
            File.WriteAllBytes(path,AppleGalleryTests.Database());
            nint db=0;
            try
            {
                Fixtures.Check(Native.sqlite3_open_v2(path,out db,2,0)==0);
                const string sql="""
                    CREATE TABLE SYNTHETIC_PADDING(ID INTEGER PRIMARY KEY, DATA BLOB);
                    BEGIN;
                    WITH RECURSIVE n(x) AS (SELECT 1 UNION ALL SELECT x+1 FROM n WHERE x<1370)
                    INSERT INTO SYNTHETIC_PADDING SELECT x,zeroblob(1040000) FROM n;
                    COMMIT;
                    """;
                int code=Native.sqlite3_exec(db,sql,0,0,out var error);
                if(error!=0)Native.sqlite3_free(error);
                Fixtures.Check(code==0,"Synthetic fixture creation failed");
            }
            finally{if(db!=0)Native.sqlite3_close(db);}
            byte[] first=new byte[4096];long length;
            using(var fixture=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
            {
                length=fixture.Length;fixture.ReadExactly(first);first[18]=2;first[19]=2;
                fixture.Position=0;fixture.Write(first);fixture.Flush(true);
            }
            Fixtures.Check(length>=Rc21CatalogTests.ReportedDatabase&&length<1536L*1024*1024);
            byte[] wal=Wal(first,length);Fixtures.Check(wal.Length==Rc21CatalogTests.ReportedWal);
            Console.WriteLine($"MEASURE synthetic fixture ready DB={length}; WAL={wal.Length}; no device accessed");
            var original=VerifiedCopy.HashFile(path,default);byte[] walHash=SHA256.HashData(wal);
            using var files=new Files(path,length,wal);
            using var process=Process.GetCurrentProcess();process.Refresh();long privateBefore=process.PrivateMemorySize64;
            long allocatedBefore=GC.GetTotalAllocatedBytes(true);var timer=Stopwatch.StartNew();
            var result=AppleCatalogCapture.Read(files,default);timer.Stop();
            process.Refresh();
            Console.WriteLine($"MEASURE rc21 full catalog DB={length}; WAL={wal.Length}; sourceReads={files.Opens}; bytes={files.Bytes}; elapsed_ms={timer.ElapsedMilliseconds}; managed_allocated={GC.GetTotalAllocatedBytes(true)-allocatedBefore}; peak_working_set={process.PeakWorkingSet64}");
            Fixtures.Check(result.VisiblePhotoCount==2&&result.VisibleVideoCount==1&&result.HiddenAssetCount==1&&result.TrashedAssetCount==1);
            Fixtures.Check(result.Albums.Count==1&&result.Select("album-one").Count==2);
            Fixtures.Check(files.Opens==4&&files.Bytes==2*(length+wal.Length));
            Fixtures.Check(VerifiedCopy.HashFile(path,default)==original&&SHA256.HashData(wal).AsSpan().SequenceEqual(walHash));
            // Album selection followed by import rereads the same catalog in the same process.
            // The first capture must not strand a cleared, unreachable large managed buffer.
            using var again=new Files(path,length,wal);
            var repeated=AppleCatalogCapture.Read(again,default);
            Fixtures.Check(repeated.VisibleVideoCount==result.VisibleVideoCount&&again.Opens==4&&again.Bytes==2*(length+wal.Length));
            Console.WriteLine("MEASURE sequential second full capture verified");
            process.Refresh();long privateAfter=process.PrivateMemorySize64;
            Console.WriteLine($"MEASURE rc22 two captures private_before={privateBefore}; private_after={privateAfter}; peak_working_set={process.PeakWorkingSet64}");
            Fixtures.Check(privateAfter-privateBefore<256L*1024*1024,"Completed captures retained catalog-sized memory");
            CryptographicOperations.ZeroMemory(wal);
        }));
    }
    private static byte[] Wal(byte[] firstPage,long length)
    {
        const int page=4096,frames=1781,stride=page+24;
        Fixtures.Check(BinaryPrimitives.ReadUInt16BigEndian(firstPage.AsSpan(16))==page&&length%page==0);
        byte[] wal=new byte[32+frames*stride];uint a=0,b=0;
        void Big(int offset,uint value)=>BinaryPrimitives.WriteUInt32BigEndian(wal.AsSpan(offset),value);
        void Checksum(int offset,int count)
        {unchecked{for(int i=offset;i<offset+count;i+=8){a+=BinaryPrimitives.ReadUInt32LittleEndian(wal.AsSpan(i))+b;b+=BinaryPrimitives.ReadUInt32LittleEndian(wal.AsSpan(i+4))+a;}}}
        Big(0,0x377f0682);Big(4,3007000);Big(8,page);Big(16,21);Big(20,37);
        Checksum(0,24);Big(24,a);Big(28,b);
        for(int n=0;n<frames;n++)
        {
            int offset=32+n*stride;Big(offset,1);Big(offset+4,n==frames-1?checked((uint)(length/page)):0);Big(offset+8,21);Big(offset+12,37);
            firstPage.CopyTo(wal,offset+24);Checksum(offset,8);Checksum(offset+24,page);Big(offset+16,a);Big(offset+20,b);
        }
        return wal;
    }
    private sealed class Files(string path,long length,byte[] wal):IAppleFileSystem
    {
        internal long Bytes;internal int Opens;
        public AppleFileInfo? Stat(string name,CancellationToken cancel)
        {cancel.ThrowIfCancellationRequested();return name switch{"/PhotoData/Photos.sqlite"=>new(length,"synthetic",true),"/PhotoData/Photos.sqlite-wal"=>new(wal.Length,"synthetic",true),_=>null};}
        public Stream OpenRead(string name,CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();Opens++;
            Stream stream=name switch{"/PhotoData/Photos.sqlite"=>new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read),"/PhotoData/Photos.sqlite-wal"=>new MemoryStream(wal,false),_=>throw new IOException("Unexpected fixture read")};
            return new Counter(stream,n=>Bytes+=n);
        }
        public void Dispose(){}
    }
    private sealed class Counter(Stream inner,Action<int> counted):Stream
    {
        public override int Read(byte[] b,int offset,int count){int n=inner.Read(b,offset,count);counted(n);return n;}
        public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;
        public override long Length=>inner.Length;public override long Position{get=>inner.Position;set=>throw new NotSupportedException();}
        public override void Flush(){}public override long Seek(long n,SeekOrigin o)=>throw new NotSupportedException();public override void SetLength(long n)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int n)=>throw new NotSupportedException();
        protected override void Dispose(bool disposing){if(disposing)inner.Dispose();base.Dispose(disposing);}
    }
    private static class Native
    {
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)]string name,out nint db,int flags,nint vfs);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_exec(nint db,[MarshalAs(UnmanagedType.LPUTF8Str)]string sql,nint cb,nint arg,out nint error);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_close(nint db);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern void sqlite3_free(nint data);
    }
}
