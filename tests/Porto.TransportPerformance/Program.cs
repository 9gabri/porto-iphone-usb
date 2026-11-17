using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Porto.Apple;
using Porto.Core;

// All payloads and AFC replies are synthetic. No network, TLS, credentials or device.
var observations=new List<object>();
string resultPath=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../results.json"));
if(File.Exists(resultPath))throw new IOException("Preserve existing results; use a new output directory.");
byte[] payload=new byte[64*1024*1024];new Random(20261117).NextBytes(payload);
using var process=Process.GetCurrentProcess();
Console.WriteLine(JsonSerializer.Serialize(new {kind="environment",framework=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,os=System.Runtime.InteropServices.RuntimeInformation.OSDescription,
    cpuCount=Environment.ProcessorCount,core=typeof(VerifiedCopy).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
    appleSha=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(AppleAfcFileSystem).Assembly.Location))),note="Concurrent isolated analysis may contend for CPU; peak working set is cumulative. Fake wait is not real USB latency."}));

// Warmup every variant; not included in collected observations.
foreach(int chunk in new[]{64,256,512})Run(1024*1024,chunk,0,false,0,false);
Run(1024*1024,64,0,true,0,false);
foreach(int size in new[]{16,64})
for(int trial=1;trial<=3;trial++)
foreach(int chunk in new[]{64,256,512})Run(size*1024*1024,chunk,0,false,trial,true);
for(int trial=1;trial<=3;trial++)
foreach(int chunk in new[]{64,256,512})Run(16*1024*1024,chunk,1,false,trial,true);

// One variable: coalesce the two command writes, keep 64 KiB and zero fake latency.
for(int trial=1;trial<=3;trial++)
foreach(bool combine in new[]{false,true})Run(64*1024*1024,64,0,combine,trial,true,"coalescing");

foreach(int chunk in new[]{256,512})
foreach(string fault in new[]{"truncated-payload","disconnected","bad-sequence","oversized-reply","early-eof","cancel"})CheckFailure(chunk,fault);

File.WriteAllText(resultPath,JsonSerializer.Serialize(observations,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("ALL SYNTHETIC INTEGRITY AND FAILURE CHECKS PASSED");

void Run(int size,int chunk,int latency,bool combine,int trial,bool record,string experiment="chunk-size")
{
    var hashTimer=Stopwatch.StartNew();var expected=new CopyResult(size,Convert.ToHexString(SHA256.HashData(payload.AsSpan(0,size))));hashTimer.Stop();
    using var wire=new SyntheticAfc(payload,size,latency);
    using Stream transport=combine?new CoalescedCommands(wire):wire;
    using var files=new AppleAfcFileSystem(transport){ReadLimit=chunk*1024};
    process.Refresh();var cpuBefore=process.TotalProcessorTime;long allocated=GC.GetTotalAllocatedBytes(true);
    var timer=Stopwatch.StartNew();CopyResult result;
    using(var input=files.OpenRead("/DCIM/100APPLE/SYNTHETIC.MOV",default))
        result=VerifiedCopy.Copy(input,Stream.Null,size,null,null,default);
    timer.Stop();process.Refresh();
    if(result!=expected||wire.PayloadReadBytes!=size)throw new Exception("Payload or hash differs");
    if(wire.ReadRequests!=1+(size+chunk*1024-1)/(chunk*1024))throw new Exception("Request count differs");
    if(!record)return;
    var row=new{kind="benchmark",experiment,payloadBytes=size,chunkKiB=chunk,requestedWaitPerReadMs=latency,combineWrites=combine,trial,
        elapsedMs=timer.Elapsed.TotalMilliseconds,actualWaitMs=wire.WaitMs,cpuMs=(process.TotalProcessorTime-cpuBefore).TotalMilliseconds,
        allocatedBytes=GC.GetTotalAllocatedBytes(false)-allocated,peakWorkingSetBytes=process.PeakWorkingSet64,hashReferenceMs=hashTimer.Elapsed.TotalMilliseconds,
        readRequests=wire.ReadRequests,openRequests=wire.OpenRequests,closeRequests=wire.CloseRequests,transportWriteCalls=wire.WriteCalls,
        transportWriteBytes=wire.WriteBytes,transportReadCalls=wire.ReadCalls,transportReadBytes=wire.ReadBytes,payloadReadBytes=wire.PayloadReadBytes,
        MiBps=size/1048576.0/timer.Elapsed.TotalSeconds,sha256Matches=true};
    observations.Add(row);Console.WriteLine(JsonSerializer.Serialize(row));
}
void CheckFailure(int chunk,string fault)
{
    using var wire=new SyntheticAfc(payload,4*1024*1024,0){Fault=fault};using var token=new CancellationTokenSource();
    using var files=new AppleAfcFileSystem(wire){ReadLimit=chunk*1024};
    using var input=files.OpenRead("/DCIM/100APPLE/SYNTHETIC.MOV",token.Token);
    if(fault=="cancel")token.CancelAfter(50);
    var timer=Stopwatch.StartNew();string exception="";bool cancelled=false;
    try{_ = VerifiedCopy.Copy(input,Stream.Null,4*1024*1024,null,null,token.Token);}
    catch(Exception ex) when(ex is IOException or InvalidDataException or OperationCanceledException){exception=ex.GetType().Name;cancelled=ex is OperationCanceledException;}
    timer.Stop();
    if(exception.Length==0)throw new Exception("Fault accepted: "+fault);
    if(fault=="cancel"&&!cancelled)throw new Exception("Cancellation not preserved");
    if(fault!="early-eof"&&files.IsUsable)throw new Exception("Broken wire stayed usable");
    if(fault=="cancel"&&timer.Elapsed.TotalSeconds>5)throw new Exception("Cancellation too late");
    var row=new{kind="fault-check",chunkKiB=chunk,fault,exception,elapsedMs=timer.Elapsed.TotalMilliseconds,sessionUsable=files.IsUsable,verifiedCopyPublished=false,readRequests=wire.ReadRequests};
    observations.Add(row);Console.WriteLine(JsonSerializer.Serialize(row));
}

sealed class CoalescedCommands(Stream inner):Stream
{
    private byte[]? header;
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> part,CancellationToken cancel=default)
    {
        cancel.ThrowIfCancellationRequested();
        if(header==null){if(part.Length!=40)throw new IOException("Expected AFC header");header=part.ToArray();return;}
        byte[] command=new byte[header.Length+part.Length];header.CopyTo(command,0);part.CopyTo(command.AsMemory(header.Length));header=null;
        await inner.WriteAsync(command,cancel);
    }
    public override ValueTask<int> ReadAsync(Memory<byte> b,CancellationToken c=default)=>inner.ReadAsync(b,c);
    public override int Read(byte[] b,int o,int n)=>inner.Read(b,o,n);
    public override void Write(byte[] b,int o,int n)=>WriteAsync(b.AsMemory(o,n)).AsTask().GetAwaiter().GetResult();
    protected override void Dispose(bool disposing){if(disposing)inner.Dispose();base.Dispose(disposing);}
    public override bool CanRead=>true;public override bool CanWrite=>true;public override bool CanSeek=>false;
    public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
    public override void Flush(){}public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long n)=>throw new NotSupportedException();
}
sealed class SyntheticAfc(byte[] data,int size,int delayMs):Stream
{
    private readonly MemoryStream pending=new();private byte[] response=[];private int responseOffset,position;private bool mediaResponse;
    public string? Fault{get;init;}
    public int ReadRequests{get;private set;}public int OpenRequests{get;private set;}public int CloseRequests{get;private set;}
    public int WriteCalls{get;private set;}public long WriteBytes{get;private set;}public int ReadCalls{get;private set;}public long ReadBytes{get;private set;}
    public long PayloadReadBytes{get;private set;}public double WaitMs{get;private set;}
    public override void Write(byte[] b,int o,int n)=>Send(b.AsSpan(o,n));
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> b,CancellationToken token=default){token.ThrowIfCancellationRequested();Send(b.Span);return ValueTask.CompletedTask;}
    private void Send(ReadOnlySpan<byte> part)
    {
        WriteCalls++;WriteBytes+=part.Length;pending.Write(part);
        byte[] accumulated=pending.GetBuffer();if(pending.Length<40)return;
        int length=checked((int)BinaryPrimitives.ReadUInt64LittleEndian(accumulated.AsSpan(8)));
        if(pending.Length<length)return;if(pending.Length!=length)throw new Exception("Combined multiple commands");
        ulong sequence=BinaryPrimitives.ReadUInt64LittleEndian(accumulated.AsSpan(24)),op=BinaryPrimitives.ReadUInt64LittleEndian(accumulated.AsSpan(32));
        var request=accumulated.AsSpan(40,length-40);byte[] body;ulong kind;mediaResponse=false;
        if(op==13){OpenRequests++;position=0;kind=14;body=new byte[8];BinaryPrimitives.WriteUInt64LittleEndian(body,42);}
        else if(op==15)
        {
            ReadRequests++;mediaResponse=true;
            if(delayMs>0){var wait=Stopwatch.StartNew();Thread.Sleep(delayMs);WaitMs+=wait.Elapsed.TotalMilliseconds;}
            int requested=checked((int)BinaryPrimitives.ReadUInt64LittleEndian(request[8..]));
            int end=Fault=="early-eof"?size/2:size;
            int count=Fault=="oversized-reply"?requested+1:Math.Min(requested,end-position);
            body=data.AsSpan(position,count).ToArray();position+=count;kind=2;
            if(Fault=="bad-sequence")sequence++;
        }
        else if(op==20){CloseRequests++;kind=1;body=new byte[8];}
        else throw new Exception("Unexpected operation");
        response=new byte[40+body.Length];"CFA6LPAA"u8.CopyTo(response);
        BinaryPrimitives.WriteUInt64LittleEndian(response.AsSpan(8),(ulong)response.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(response.AsSpan(16),40);
        BinaryPrimitives.WriteUInt64LittleEndian(response.AsSpan(24),sequence);BinaryPrimitives.WriteUInt64LittleEndian(response.AsSpan(32),kind);
        body.CopyTo(response,40);responseOffset=0;pending.SetLength(0);pending.Position=0;
    }
    public override int Read(byte[] b,int o,int n)=>Receive(b.AsSpan(o,n));
    public override async ValueTask<int> ReadAsync(Memory<byte> b,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(mediaResponse&&Fault=="cancel")await Task.Delay(Timeout.Infinite,token);
        return Receive(b.Span);
    }
    private int Receive(Span<byte> output)
    {
        ReadCalls++;
        if(mediaResponse&&Fault=="disconnected")throw new IOException("Synthetic disconnect");
        int available=response.Length;
        if(mediaResponse&&Fault=="truncated-payload")available=40+(available-40)/2;
        int count=Math.Min(output.Length,available-responseOffset);if(count<0)count=0;
        response.AsSpan(responseOffset,count).CopyTo(output);int next=responseOffset+count;
        if(mediaResponse)PayloadReadBytes+=Math.Max(0,next-Math.Max(40,responseOffset));
        responseOffset=next;ReadBytes+=count;return count;
    }
    public override bool CanRead=>true;public override bool CanWrite=>true;public override bool CanSeek=>false;
    public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
    public override void Flush(){}public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long n)=>throw new NotSupportedException();
}
