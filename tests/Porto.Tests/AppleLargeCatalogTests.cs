using System.Diagnostics;
using System.Buffers.Binary;
using System.Security.Cryptography;
using Porto.Apple;
using Porto.Core;

internal static class AppleLargeCatalogTests
{
    internal static void Add(List<(string,Action)> tests)
    {
        tests.Add(("apple large catalog accepts reported database and WAL sizes without allocating media",()=>
            AppleCatalogCapture.ValidateMetadata(new(563425280,"synthetic",true),new(4227152,"synthetic",true))));
        tests.Add(("apple large catalog accepts reported database with no WAL",()=>
            AppleCatalogCapture.ValidateMetadata(new(563425280,"synthetic",true),null)));
    }

    // Opt-in because this exercises real allocations and a valid SQLite database above 512 MiB.
    // Only synthetic bytes are generated; no phone or existing personal file is accessed.
    internal static void AddLarge(List<(string,Action)> tests)
    {
        tests.Add(("apple large catalog full capture and integrity check above reported size",()=>
        {
            byte[] database=AppleGalleryTests.Database("""
                CREATE TABLE SYNTHETIC_PADDING(ID INTEGER PRIMARY KEY, DATA BLOB);
                WITH RECURSIVE n(x) AS (SELECT 1 UNION ALL SELECT x+1 FROM n WHERE x<544)
                INSERT INTO SYNTHETIC_PADDING SELECT x,zeroblob(1040000) FROM n;
                """);
            byte[]? wal=null;
            try
            {
                Fixtures.Check(database.LongLength>563425280&&database.LongLength<600000000,"Synthetic catalog must cover the reported database size");
                database[18]=2;database[19]=2;
                wal=RepeatedFirstPageWal(database);
                Fixtures.Check(wal.Length==4227152,"WAL matches the reported byte length");
                byte[] hash=SHA256.HashData(database),walHash=SHA256.HashData(wal);
                using var files=new SyntheticFiles(database,wal);
                long allocationStart=GC.GetTotalAllocatedBytes(true);
                var timer=Stopwatch.StartNew();
                var catalog=AppleCatalogCapture.Read(files,default);
                timer.Stop();
                long allocated=GC.GetTotalAllocatedBytes(true)-allocationStart;
                using var process=Process.GetCurrentProcess();process.Refresh();
                Console.WriteLine($"MEASURE synthetic catalog bytes={database.LongLength}; WAL={wal.LongLength}; managed allocated during capture={allocated}; process peak working set={process.PeakWorkingSet64}; elapsed_ms={timer.ElapsedMilliseconds}");
                Fixtures.Check(catalog.VisiblePhotoCount==2&&catalog.VisibleVideoCount==1&&catalog.HiddenAssetCount==1&&catalog.TrashedAssetCount==1);
                Fixtures.Check(catalog.Albums.Count==1&&catalog.Select("album-one").Count==2);
                Fixtures.Check(files.Opens==4&&files.ReadBytes==2*(database.LongLength+wal.LongLength),"Catalog and WAL must be completely read and compared twice");
                Fixtures.Check(hash.AsSpan().SequenceEqual(SHA256.HashData(database)),"Source bytes were modified");
                Fixtures.Check(walHash.AsSpan().SequenceEqual(SHA256.HashData(wal)),"Source WAL bytes were modified");
                Fixtures.Check(allocated<database.LongLength+128L*1024*1024,"Reread must not allocate another catalog-sized managed buffer");
            }
            finally{CryptographicOperations.ZeroMemory(database);if(wal!=null)CryptographicOperations.ZeroMemory(wal);}
        }));
    }

    private static byte[] RepeatedFirstPageWal(byte[] database)
    {
        const int page=4096,frames=1026,stride=page+24;
        Fixtures.Check(BinaryPrimitives.ReadUInt16BigEndian(database.AsSpan(16))==page);
        byte[] wal=new byte[32+frames*stride];
        void Big(int offset,uint value)=>BinaryPrimitives.WriteUInt32BigEndian(wal.AsSpan(offset),value);
        Big(0,0x377f0682);Big(4,3007000);Big(8,page);Big(16,17);Big(20,29);
        uint first=0,second=0;
        void Checksum(int offset,int count)
        {
            unchecked{for(int i=offset;i<offset+count;i+=8){first+=BinaryPrimitives.ReadUInt32LittleEndian(wal.AsSpan(i))+second;second+=BinaryPrimitives.ReadUInt32LittleEndian(wal.AsSpan(i+4))+first;}}
        }
        Checksum(0,24);Big(24,first);Big(28,second);
        for(int n=0;n<frames;n++)
        {
            int offset=32+n*stride;Big(offset,1);Big(offset+4,n==frames-1?(uint)(database.Length/page):0);Big(offset+8,17);Big(offset+12,29);
            database.AsSpan(0,page).CopyTo(wal.AsSpan(offset+24,page));
            Checksum(offset,8);Checksum(offset+24,page);Big(offset+16,first);Big(offset+20,second);
        }
        return wal;
    }

    private sealed class SyntheticFiles(byte[] database,byte[] wal):IAppleFileSystem
    {
        internal int Opens;
        internal long ReadBytes;
        public AppleFileInfo? Stat(string path,CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            return path switch {"/PhotoData/Photos.sqlite"=>new(database.LongLength,"synthetic",true),"/PhotoData/Photos.sqlite-wal"=>new(wal.LongLength,"synthetic",true),_=>null};
        }
        public Stream OpenRead(string path,CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            byte[] bytes=path switch {"/PhotoData/Photos.sqlite"=>database,"/PhotoData/Photos.sqlite-wal"=>wal,_=>throw new IOException("Unexpected synthetic file read")};Opens++;
            return new CountingStream(bytes,count=>ReadBytes+=count);
        }
        public void Dispose(){}
    }
    private sealed class CountingStream(byte[] bytes,Action<int> read):Stream
    {
        private readonly MemoryStream input=new(bytes,false);
        public override int Read(byte[] buffer,int offset,int count)
        {int result=input.Read(buffer,offset,count);read(result);return result;}
        public override int Read(Span<byte> buffer)
        {int result=input.Read(buffer);read(result);return result;}
        public override int ReadByte()
        {int result=input.ReadByte();if(result>=0)read(1);return result;}
        public override bool CanRead=>true;public override bool CanWrite=>false;public override bool CanSeek=>false;
        public override long Length=>input.Length;public override long Position{get=>input.Position;set=>throw new NotSupportedException();}
        public override void Flush(){}public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
        protected override void Dispose(bool disposing){if(disposing)input.Dispose();base.Dispose(disposing);}
    }
}
