using System.Globalization;
using System.Buffers.Binary;
using System.Text;
using Porto.Apple;

internal static class AppleCatalogDiagnosticsTests
{
    internal static void Add(List<(string,Action)> tests)
    {
        long limit=AppleSqliteImage.MaximumBytes;
        foreach(var item in new (string Name,AppleFileInfo Database,AppleFileInfo? Wal,string Code)[]
        {
            ("nonregular database",Info(4096,false),null,"DB-TYPE"),
            ("empty database",Info(0),null,"DB-EMPTY"),
            ("oversize database",Info(limit+1),null,"DB-LIMIT"),
            ("nonregular WAL",Info(4096),Info(32,false),"WAL-TYPE"),
            ("negative WAL length",Info(4096),Info(-1),"WAL-LENGTH"),
            ("oversize WAL",Info(4096),Info(limit+1),"WAL-LIMIT"),
            ("combined size overflow",Info(limit),Info(32),"TOTAL-LIMIT"),
            ("hostile long size without arithmetic overflow",Info(long.MaxValue),Info(long.MaxValue),"DB-LIMIT")
        })
        {
            tests.Add(("apple catalog diagnostic reports "+item.Name+" before opening any file",()=>
            {
                using var files=new MetadataFiles(item.Database,item.Wal);
                IOException? failure=null;
                try{AppleCatalogCapture.Read(files,default);}
                catch(IOException exception){failure=exception;}
                Fixtures.Check(failure!=null,"Invalid metadata was accepted");
                Fixtures.Check(failure!.Message.Contains("[APPLE-CATALOG-"+item.Code+"]",StringComparison.Ordinal),failure.Message);
                Fixtures.Check(failure.Message.Contains(item.Database.Length.ToString(CultureInfo.InvariantCulture),StringComparison.Ordinal),"Missing numeric database length");
                if(item.Wal!=null)Fixtures.Check(failure.Message.Contains(item.Wal.Length.ToString(CultureInfo.InvariantCulture),StringComparison.Ordinal),"Missing numeric WAL length");
                Fixtures.Check(!failure.Message.Contains("private-modification",StringComparison.Ordinal)&&!failure.Message.Contains("/PhotoData",StringComparison.Ordinal),"Diagnostic exposed private metadata");
                Fixtures.Check(files.OpenCount==0,"Rejected metadata caused a file read");
            }));
        }
        tests.Add(("apple catalog diagnostic boundary permits exact database limit with absent WAL",()=>
            AppleCatalogCapture.ValidateMetadata(Info(limit),null)));
        tests.Add(("apple catalog diagnostic boundary permits exact combined limit and empty WAL",()=>
        {
            AppleCatalogCapture.ValidateMetadata(Info(limit-32),Info(32));
            AppleCatalogCapture.ValidateMetadata(Info(limit),Info(0));
        }));
        tests.Add(("apple catalog diagnostic AFC distinguishes file directory link and unknown without exposing raw types",()=>
        {
            foreach(var shape in new[]{("S_IFREG",true,AppleNonFileKind.Unknown,""),
                ("S_IFDIR",false,AppleNonFileKind.Directory,"cartella"),
                ("S_IFLNK",false,AppleNonFileKind.SymbolicLink,"collegamento"),
                ("SECRET_PHONE_TYPE",false,AppleNonFileKind.Unknown,"non riconosciuto")})
            {
                using var wire=new StatWire(shape.Item1);
                using var afc=new AppleAfcFileSystem(wire);
                var info=afc.Stat("/PhotoData/Photos.sqlite",default)!;
                Fixtures.Check(info.IsFile==shape.Item2&&info.NonFileKind==shape.Item3);
                if(shape.Item2)
                    Fixtures.Check(info==new AppleFileInfo(4096,"1",true),"Regular metadata compatibility changed");
                else
                {
                    using var files=new MetadataFiles(info,null);
                    IOException? error=null;
                    try{AppleCatalogCapture.Read(files,default);}catch(IOException failure){error=failure;}
                    Fixtures.Check(error!=null&&error.Message.Contains(shape.Item4,StringComparison.Ordinal));
                    Fixtures.Check(!error!.Message.Contains("SECRET_PHONE_TYPE",StringComparison.Ordinal)&&files.OpenCount==0);
                }
            }
        }));
    }

    private static AppleFileInfo Info(long size,bool regular=true)=>new(size,"private-modification",regular);
    private sealed class StatWire:Stream
    {
        private readonly MemoryStream reply;
        internal StatWire(string kind)
        {
            byte[] payload=Encoding.UTF8.GetBytes("st_size\0"+"4096\0st_mtime\0"+"1\0st_ifmt\0"+kind+'\0');
            byte[] bytes=new byte[40+payload.Length];"CFA6LPAA"u8.CopyTo(bytes);
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(8),(ulong)bytes.Length);
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(16),(ulong)bytes.Length);
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(24),1);
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(32),2);
            payload.CopyTo(bytes,40);reply=new(bytes,false);
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancel=default)=>reply.ReadAsync(buffer,cancel);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken cancel=default)=>ValueTask.CompletedTask;
        public override int Read(byte[] buffer,int offset,int count)=>reply.Read(buffer,offset,count);
        public override void Write(byte[] buffer,int offset,int count){}
        protected override void Dispose(bool disposing){if(disposing)reply.Dispose();base.Dispose(disposing);}
        public override bool CanRead=>true;public override bool CanWrite=>true;public override bool CanSeek=>false;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush(){}public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();
    }
    private sealed class MetadataFiles(AppleFileInfo database,AppleFileInfo? wal):IAppleFileSystem
    {
        internal int OpenCount;
        public AppleFileInfo? Stat(string path,CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            return path switch
            {
                "/PhotoData/Photos.sqlite"=>database,
                "/PhotoData/Photos.sqlite-wal"=>wal,
                "/PhotoData/Photos.sqlite-journal"=>null,
                _=>throw new InvalidOperationException("Unexpected synthetic path")
            };
        }
        public Stream OpenRead(string path,CancellationToken cancel)
        {
            OpenCount++;
            throw new InvalidOperationException("Metadata rejection must happen before opening files");
        }
        public void Dispose(){}
    }
}
