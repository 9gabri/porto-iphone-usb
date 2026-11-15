using System.Text;
using System.Text.Json;
using Porto.Core;
using Porto.Usb;

internal static class Rc5ProtocolTests
{
    public static int Child(string mode)
    {
        string scenario=mode["--rc5-protocol-".Length..];
        string response=scenario switch
        {
            "zero"=>"{\"ResourceLength\":0}",
            "large"=>"{\"ResourceLength\":4294967313}",
            "unknown"=>"{\"ResourceLength\":null}",
            "legacy"=>"true",
            "negative"=>"{\"ResourceLength\":-1}",
            "string"=>"{\"ResourceLength\":\"123\"}",
            "missing"=>"{}",
            "false"=>"false",
            "overflow"=>"{\"ResourceLength\":9223372036854775808}",
            _=>throw new InvalidOperationException("Unknown synthetic protocol scenario.")
        };
        byte[] payload=scenario=="zero"?[]:[1,2,3,4];
        int position=0;
        using var writer=new BinaryWriter(Console.OpenStandardOutput());
        void Frame(byte kind,ReadOnlySpan<byte> bytes)
        {writer.Write(kind);writer.Write(bytes.Length);writer.Write(bytes);writer.Flush();}
        string? line;
        while((line=Console.ReadLine())!=null)
        {
            var command=JsonSerializer.Deserialize<Command>(line)!;
            switch(command.Op)
            {
                case "open":Frame(0,Encoding.UTF8.GetBytes(response));break;
                case "read":
                    int count=Math.Min(command.Count,payload.Length-position);
                    Frame(2,payload.AsSpan(position,count));position+=count;break;
                default:return 91;
            }
        }
        return 0;
    }

    public static void Add(List<(string,Action)> tests)
    {
        foreach(var sample in new (string Mode,long? Expected)[]{("zero",0),("large",4294967313),("unknown",null),("legacy",null)})
        {
            tests.Add(($"rc5 protocol resource length crosses process boundary and preserves payload: {sample.Mode}",()=>
            {
                using var worker=new WorkerClient(Environment.ProcessPath!,"--rc5-protocol-"+sample.Mode){Timeout=TimeSpan.FromSeconds(3)};
                using var stream=worker.OpenRead(new("synthetic","p","video.mov",MediaKind.Video),default);
                Fixtures.Check(stream is ISourceStreamInfo info&&info.ResourceLength==sample.Expected,"Resource length was lost or truncated across IPC.");
                byte[] buffer=[99,99,99,99,99,99];
                Fixtures.Check(stream.Read(buffer,1,0)==0);
                int count=stream.Read(buffer,1,4);
                Fixtures.Check(count==(sample.Mode=="zero"?0:4));
                Fixtures.Check(buffer[0]==99&&buffer[5]==99,"IPC payload overwrote bytes outside the requested range.");
                if(sample.Mode!="zero")Fixtures.Check(buffer.AsSpan(1,4).SequenceEqual(new byte[]{1,2,3,4}));
                Fixtures.Check(stream.Read(buffer,0,4)==0&&!worker.IsDisposed);
            }));
        }
        foreach(string mode in new[]{"negative","string","missing","false","overflow"})
        {
            tests.Add(($"rc5 protocol malformed resource length invalidates worker: {mode}",()=>
            {
                using var worker=new WorkerClient(Environment.ProcessPath!,"--rc5-protocol-"+mode){Timeout=TimeSpan.FromSeconds(3)};
                Fixtures.Throws<SourceUnavailableException>(()=>worker.OpenRead(new("synthetic","p","video.mov",MediaKind.Video),default));
                Fixtures.Check(worker.IsDisposed,"A malformed open response left the connection usable.");
            }));
        }
    }
}
