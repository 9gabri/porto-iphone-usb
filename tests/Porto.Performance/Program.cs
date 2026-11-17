using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Porto.Core;

string root=Path.GetFullPath(args[0]);string label=args[1];
if(!root.EndsWith("perf-rc19",StringComparison.Ordinal)||label is not("before" or "after"))throw new Exception("Isolated benchmark only");
string resultPath=Path.Combine(root,label+".json");if(File.Exists(resultPath))throw new Exception("Preserve prior results");
var data=Enumerable.Range(0,3).Select(n=>Video(n+19,8*1024*1024)).ToArray();var old=Video(11,32768);
var records=new List<object>();
for(int trial=1;trial<=3;trial++)
{
    string directory=Path.Combine(root,$"{label}-{trial}");if(Directory.Exists(directory))throw new Exception("Preserve prior archive");
    using var destination=new Destination(directory);using var seed=new Gallery([old],0);
    if(!new ImportEngine(seed,destination).Run(false,default).GallerySelectionCopiesVerified)throw new Exception("Seed failed");
    string prior=Directory.GetFiles(destination.Videos).Single();byte[] hash=SHA256.HashData(File.ReadAllBytes(prior));
    using var source=new Gallery(data,5);
    var watch=Stopwatch.StartNew();var report=new ImportEngine(source,destination).Run(false,default);double elapsed=watch.Elapsed.TotalMilliseconds;
    if(!report.GallerySelectionCopiesVerified||report.CurrentCount(Outcome.Imported)!=3)throw new Exception(report.Status);
    if(!SHA256.HashData(File.ReadAllBytes(prior)).SequenceEqual(hash))throw new Exception("Prior changed");
    var hashes=Directory.GetFiles(destination.Videos).Select(path=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))).ToHashSet();
    if(data.Any(bytes=>!hashes.Contains(Convert.ToHexString(SHA256.HashData(bytes)))))throw new Exception("Byte mismatch");
    records.Add(new{trial,elapsedMs=elapsed,source.Bytes,source.Opens,source.Calls,source.WaitMs,payloadBytes=data.Sum(b=>(long)b.Length)});
}
string result=JsonSerializer.Serialize(new{label,syntheticOnly=true,realUsb=false,delay="5 ms spin wait per successful payload Read; actual wait measured; NOT phone latency",
    coreSha=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(ImportEngine).Assembly.Location))),records},new JsonSerializerOptions{WriteIndented=true});
File.WriteAllText(resultPath,result);Console.WriteLine(result);

static byte[] Video(int seed,int size)
{
    byte[] handler=new byte[24];Encoding.ASCII.GetBytes("vide").CopyTo(handler,8);
    byte[] prefix=Atom("ftyp",Encoding.ASCII.GetBytes("qt  \0\0\0\0qt  ")).Concat(Atom("moov",Atom("trak",Atom("mdia",Atom("hdlr",handler))))).ToArray();
    var payload=new byte[size-prefix.Length-8];new Random(seed).NextBytes(payload);return prefix.Concat(Atom("mdat",payload)).ToArray();
}
static byte[] Atom(string name,byte[] payload)
{var b=new byte[payload.Length+8];BinaryPrimitives.WriteUInt32BigEndian(b,(uint)b.Length);Encoding.ASCII.GetBytes(name).CopyTo(b,4);payload.CopyTo(b,8);return b;}
sealed class Gallery(byte[][] contents,int delay):IGallerySource
{
    public long Bytes;public int Opens,Calls;public double WaitMs;
    public bool AppleResourceHints=>false;public string SourceDescription=>"Synthetic performance comparison";
    public GalleryCoverage? Coverage=>new("Synthetic",0,contents.Length,0,0,0);
    public Inventory Inventory(CancellationToken token)=>new(contents.Select((b,i)=>new SourceItem(i.ToString(),"album","same.mov",MediaKind.Video,b.Length,SourceNamespace:"perf19")).ToList(),[],true);
    public Stream OpenRead(SourceItem item,CancellationToken token){Opens++;return new Reader(contents[int.Parse(item.Id)],this,delay);}
    public void Dispose(){}
}
sealed class Reader(byte[] bytes,Gallery owner,int delay):MemoryStream(bytes,false)
{
    public override int Read(byte[] b,int offset,int count)
    {
        int n=base.Read(b,offset,Math.Min(65536,count));owner.Calls++;owner.Bytes+=n;
        if(n>0&&delay>0){long start=Stopwatch.GetTimestamp();while(Stopwatch.GetElapsedTime(start).TotalMilliseconds<delay)Thread.SpinWait(64);owner.WaitMs+=Stopwatch.GetElapsedTime(start).TotalMilliseconds;}
        return n;
    }
}
