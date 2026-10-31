using System.Diagnostics;
using System.Buffers.Binary;
using System.Text;
using Porto.Core;
using static Fixtures;

internal static class LargeTests
{
    public static void Add(List<(string,Action)> tests)
    {
        tests.Add(("stream beyond 4 GiB: 4 GiB + 17 bytes actually read and hashed without disk allocation",()=>{
            long length=(1L<<32)+17;var timer=Stopwatch.StartNew();using var stream=new VirtualStream(length);
            var result=VerifiedCopy.Copy(stream,Stream.Null,length,null,null,default);Fixtures.Check(result.Length==length);Console.WriteLine($"  LARGE stream: {result.Length} bytes, {timer.Elapsed.TotalSeconds:F2}s, working set {Environment.WorkingSet/(1024*1024)} MiB");
        }));
        tests.Add(("50000-item inventory and 250 GB logical capacity without writing payload",()=>{
            var items=Enumerable.Range(0,50000).Select(i=>new SourceItem(i.ToString(),"p","video"+i+".mov",MediaKind.Video,5_000_000)).ToList();
            Fixtures.Check(items.Sum(i=>i.Size!.Value)==250_000_000_000L);using var d=new Destination(Root());var s=new FakeSource(items,[]);
            s.Reader=(_,_)=>throw new Exception("No data should be read in capacity simulation");var timer=Stopwatch.StartNew();
            var r=new ImportEngine(s,d,new FakeSpace(240_000_000_000)).Run(false,default);Fixtures.Check(r.Status.Contains("pausa"));
            Console.WriteLine($"  LARGE inventory: {items.Count} items, 250 GB logical, {timer.Elapsed.TotalSeconds:F2}s, working set {Environment.WorkingSet/(1024*1024)} MiB");
        }));
        tests.Add(("50000 inventory entries with 2000 actual copies and verified second pass",()=>{
            using var d=new Destination(Root());byte[] video=Video();
            var items=Enumerable.Range(0,50000).Select(i=>new SourceItem(i.ToString(),"p",i+(i<2000?".mov":".heic"),i<2000?MediaKind.Video:MediaKind.Image,i<2000?video.Length:100)).ToList();
            var data=items.Take(2000).ToDictionary(i=>i.Id,_=>video);
            var source=new FakeSource(items,data);var watch=Stopwatch.StartNew();
            var first=new ImportEngine(source,d).Run(false,default);
            Fixtures.Check(first.Count(Outcome.Imported)==2000&&first.Count(Outcome.Excluded)==48000&&first.Status.StartsWith("Completati"),first.Status);
            var second=new ImportEngine(new FakeSource(items,data),d).Run(false,default);
            Fixtures.Check(second.Count(Outcome.AlreadyPresent)==2000&&second.Count(Outcome.Pending)==0&&Directory.GetFiles(d.Videos).Length==2000,second.Status);
            Console.WriteLine($"  STRESS full loop: 2000 physical files twice verified, 50000 entries, {watch.Elapsed.TotalSeconds:F2}s, working set {Environment.WorkingSet/(1024*1024)} MiB");
        }));
        tests.Add(("video over 4 GiB actually written verified and published on NTFS",()=>{
            using var d=new Destination(Root());long length=(1L<<32)+4096;
            Fixtures.Check(new LocalDiskSpace().Available(d.Root)>length+10L*1024*1024*1024,"Insufficient free space for bounded 4 GiB test");
            var source=new LargeVideoSource(length);var watch=Stopwatch.StartNew();
            try
            {
                var report=new ImportEngine(source,d).Run(false,default);Fixtures.Check(report.Count(Outcome.Imported)==1,report.Status);
                string saved=Directory.GetFiles(d.Videos).Single();Fixtures.Check(new FileInfo(saved).Length==length&&MediaInspector.Inspect(saved,default).IsVideo);
                Console.WriteLine($"  LARGE NTFS: {length} bytes written and reread, {watch.Elapsed.TotalSeconds:F2}s, working set {Environment.WorkingSet/(1024*1024)} MiB");
            }
            finally
            {
                // Only synthetic payloads created in this fresh test destination; never user/source data.
                foreach(string file in Directory.GetFiles(d.Videos).Concat(Directory.GetFiles(d.State,"*.part")))File.Delete(file);
            }
        }));
    }
    private sealed class LargeVideoSource(long length):IVideoSource
    {
        public Inventory Inventory(CancellationToken cancel)=>new([new("generated","p","large.mov",MediaKind.Video,length)],[],true);
        public Stream OpenRead(SourceItem item,CancellationToken cancel)=>new LargeVideoStream(length);
        public void Dispose(){}
    }
    private sealed class LargeVideoStream:Stream
    {
        private readonly long length;private readonly byte[] prefix;private long position;
        public LargeVideoStream(long length)
        {
            this.length=length;byte[] handler=new byte[24];Encoding.ASCII.GetBytes("vide").CopyTo(handler,8);
            byte[] metadata=Atom("ftyp",Encoding.ASCII.GetBytes("qt  \0\0\0\0qt  ")).Concat(Atom("moov",Atom("trak",Atom("mdia",Atom("hdlr",handler))))).ToArray();
            byte[] header=new byte[16];BinaryPrimitives.WriteUInt32BigEndian(header,1);Encoding.ASCII.GetBytes("mdat").CopyTo(header,4);BinaryPrimitives.WriteUInt64BigEndian(header.AsSpan(8),(ulong)(length-metadata.Length));
            prefix=metadata.Concat(header).ToArray();
        }
        public override int Read(byte[] buffer,int offset,int count)
        {
            int n=(int)Math.Min(count,length-position);Array.Clear(buffer,offset,n);
            if(position<prefix.Length)prefix.AsSpan((int)position,Math.Min(n,prefix.Length-(int)position)).CopyTo(buffer.AsSpan(offset));
            position+=n;return n;
        }
        public override bool CanRead=>true;public override bool CanWrite=>false;public override bool CanSeek=>false;
        public override long Length=>length;public override long Position{get=>position;set=>throw new NotSupportedException();}
        public override void Flush(){}public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long l)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
    }
}

