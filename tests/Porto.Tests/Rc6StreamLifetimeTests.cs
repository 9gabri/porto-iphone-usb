using System.Text;
using System.Text.Json;
using Porto.Core;
using Porto.Usb;

internal static class Rc6StreamLifetimeTests
{
    public static int Child(bool blockCleanup=false,bool failCleanup=false)
    {
        using var writer=new BinaryWriter(Console.OpenStandardOutput(),Encoding.UTF8,true);
        int opened=0;
        void Frame(byte kind,byte[] data){writer.Write(kind);writer.Write(data.Length);writer.Write(data);writer.Flush();}
        string? line;
        while((line=Console.ReadLine())!=null)
        {
            var command=JsonSerializer.Deserialize<Command>(line)!;
            if(command.Op=="open")
            {
                opened++;
                if(blockCleanup&&opened==2)Thread.Sleep(30000);
                if(failCleanup&&opened==2)
                {Frame(4,JsonSerializer.SerializeToUtf8Bytes(new{Message="Synthetic prior cleanup failed",NativeHResult=(int?)null,PreviousOperation=true}));continue;}
                if(command.Object=="bad")Frame(3,Encoding.UTF8.GetBytes("Synthetic open failure"));
                else Frame(0,JsonSerializer.SerializeToUtf8Bytes(new{ResourceLength=4}));
            }
            else if(command.Op=="read")Frame(2,Enumerable.Repeat((byte)opened,Math.Min(command.Count,4)).ToArray());
            else if(command.Op=="inventory")Frame(0,JsonSerializer.SerializeToUtf8Bytes(new Inventory([],[],true)));
            else Frame(0,JsonSerializer.SerializeToUtf8Bytes(true));
        }
        return 0;
    }
    public static void Add(List<(string,Action)> tests)
    {
        tests.Add(("rc6 protocol preserves that cleanup failure belongs to the previous read",()=>
        {
            using var worker=new WorkerClient(Environment.ProcessPath!,"--rc6-cleanup-failure"){Timeout=TimeSpan.FromSeconds(3)};
            using var previous=worker.OpenRead(new("a","p","a.mov",MediaKind.Video),default);
            previous.Dispose();
            Fixtures.Throws<PreviousReadCleanupException>(()=>worker.OpenRead(new("b","p","b.mov",MediaKind.Video),default));
            Fixtures.Check(worker.IsDisposed,"Cleanup failure must invalidate the worker and its old object IDs.");
        }));
        foreach(bool cancel in new[]{false,true})
            tests.Add(($"rc6 cleanup blocked inside next open remains bounded by {(cancel?"cancellation":"timeout")}",()=>
            {
                using var worker=new WorkerClient(Environment.ProcessPath!,"--rc6-cleanup-hang"){Timeout=TimeSpan.FromSeconds(5)};
                using var unfinished=worker.OpenRead(new("a","p","a.mov",MediaKind.Video),default);
                unfinished.Dispose();
                using var stop=new CancellationTokenSource();
                if(cancel)stop.CancelAfter(150);else worker.Timeout=TimeSpan.FromMilliseconds(400);
                var watch=System.Diagnostics.Stopwatch.StartNew();
                if(cancel)Fixtures.Throws<OperationCanceledException>(()=>worker.OpenRead(new("b","p","b.mov",MediaKind.Video),stop.Token));
                else Fixtures.Throws<SourceUnavailableException>(()=>worker.OpenRead(new("b","p","b.mov",MediaKind.Video),default));
                Fixtures.Check(worker.IsDisposed&&watch.Elapsed<TimeSpan.FromSeconds(3),"A blocked cleanup left the worker alive or stalled the caller.");
            }));
        foreach(string operation in new[]{"open","failed-open","inventory","connect"})
            tests.Add(($"rc6 stale stream cannot consume a later resource after {operation}",()=>
            {
                using var worker=new WorkerClient(Environment.ProcessPath!,"--rc6-lifetime"){Timeout=TimeSpan.FromSeconds(3)};
                using var old=worker.OpenRead(new("a","p","a.mov",MediaKind.Video),default);
                Stream? current=null;
                try
                {
                    switch(operation)
                    {
                        case "open":current=worker.OpenRead(new("b","p","b.mov",MediaKind.Video),default);break;
                        case "failed-open":Fixtures.Throws<IOException>(()=>worker.OpenRead(new("bad","p","bad.mov",MediaKind.Video),default));break;
                        case "inventory":worker.Inventory(default);break;
                        case "connect":worker.Connect("synthetic-device",default);break;
                    }
                    Fixtures.Throws<IOException>(()=>old.ReadByte());
                    Fixtures.Check(!worker.IsDisposed,"A stale wrapper must not destroy the current connection.");
                    if(current!=null)
                    {
                        old.Dispose();byte[] bytes=new byte[4];
                        Fixtures.Check(current.Read(bytes,0,4)==4&&bytes.All(b=>b==2),"The current stream lost its payload or was closed by disposing an old wrapper.");
                    }
                }
                finally{current?.Dispose();}
            }));
    }
}
