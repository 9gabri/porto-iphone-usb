using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Porto.Core;
using Porto.Usb;

internal static class AdversarialUsbTests
{
    private const string SelectedDevice="synthetic-selected-phone";
    public static int Child(string mode)
    {
        if(mode=="--adversarial-usb-no-stdin"){Thread.Sleep(30000);return 0;}
        if(mode=="--adversarial-usb-mixed-files")return MixedFilesChild();
        bool failedSession=mode=="--adversarial-usb-stale-session",closeFails=mode=="--adversarial-usb-close-fails",sent=false,streamOpen=false;
        using var writer=new BinaryWriter(Console.OpenStandardOutput());
        void Frame(byte kind,byte[] data){writer.Write(kind);writer.Write(data.Length);writer.Write(data);writer.Flush();}
        void Json<T>(T value)=>Frame(0,JsonSerializer.SerializeToUtf8Bytes(value));
        void Error(string message)=>Frame(3,Encoding.UTF8.GetBytes(message));
        string? line;
        while((line=Console.ReadLine())!=null)
        {
            var command=JsonSerializer.Deserialize<Command>(line)!;
            switch(command.Op)
            {
                case "connect":
                    if(command.Device==SelectedDevice)Json(true);else Error("Synthetic worker received a different device ID.");
                    break;
                case "inventory":
                    if(closeFails&&streamOpen){streamOpen=false;Error("Synthetic cleanup failure.");break;}
                    Json(new Inventory(failedSession
                        ?[new("old-first","parent","first.mov",MediaKind.Video,1),new("reused","parent","second.mov",MediaKind.Video,1)]
                        :[new("fresh","parent","fresh.mov",MediaKind.Video,1)],[],true));
                    break;
                case "open":sent=false;streamOpen=true;Json(true);break;
                case "read":
                    if(failedSession)return 77; // Real pipe EOF: this connection and its IDs are gone.
                    else{Frame(2,sent?[]:[42]);sent=true;}
                    break;
                case "close":streamOpen=false;if(closeFails)Error("Synthetic cleanup failure.");else Json(true);break;
                default:Error("Unsupported synthetic command.");break;
            }
        }
        return 0;
    }

    private static int MixedFilesChild()
    {
        byte[] first=Fixtures.Video(101),second=Fixtures.Video(102);
        List<SourceItem> items=[new("bad-open-1","parent","bad1.mov",MediaKind.Video,first.Length),
            new("bad-read","parent","bad2.mov",MediaKind.Video,first.Length),
            new("bad-open-2","parent","bad3.mov",MediaKind.Video,first.Length),
            new("good-1","parent","good1.mov",MediaKind.Video,first.Length),
            new("good-2","parent","good2.mov",MediaKind.Video,second.Length)];
        using var writer=new BinaryWriter(Console.OpenStandardOutput());
        void Frame(byte kind,byte[] data){writer.Write(kind);writer.Write(data.Length);writer.Write(data);writer.Flush();}
        void Json<T>(T value)=>Frame(0,JsonSerializer.SerializeToUtf8Bytes(value));
        void Error(string message)=>Frame(3,Encoding.UTF8.GetBytes(message));
        string? current=null,line;int position=0;
        while((line=Console.ReadLine())!=null)
        {
            var command=JsonSerializer.Deserialize<Command>(line)!;
            switch(command.Op)
            {
                case "connect":
                    if(command.Device==SelectedDevice)Json(true);else Error("Synthetic worker received a different device ID.");
                    break;
                case "inventory":current=null;Json(new Inventory(items,[],true));break;
                case "open":
                    current=command.Object;position=0;
                    if(current is "bad-open-1" or "bad-open-2")Error("Synthetic unreadable file "+current);
                    else if(items.Any(item=>item.Id==current))Json(true);
                    else Error("Unknown synthetic object.");
                    break;
                case "read":
                    if(current=="bad-read"){Error("Synthetic unreadable file "+current);break;}
                    byte[] data=current=="good-1"?first:second;
                    int count=Math.Min(command.Count,data.Length-position);
                    Frame(2,data.AsSpan(position,count).ToArray());position+=count;break;
                case "close":current=null;Json(true);break;
                default:Error("Unsupported synthetic command.");break;
            }
        }
        return 0;
    }

    public static void Add(List<(string,Action)> tests)
    {
        foreach(bool cancelled in new[]{false,true})tests.Add(($"blocked stdin write respects {(cancelled?"pause":"timeout")} and disposes promptly",()=>
        {
            using var cancel=new CancellationTokenSource();
            using var worker=new WorkerClient(Environment.ProcessPath!,"--adversarial-usb-no-stdin")
                {Timeout=TimeSpan.FromMilliseconds(cancelled?10000:300)};
            if(cancelled)cancel.CancelAfter(300);
            var timer=Stopwatch.StartNew();
            // Fits the worker's command limit but cannot fit an undrained anonymous pipe.
            string id=new('x',48*1024);
            if(cancelled)Fixtures.Throws<OperationCanceledException>(()=>worker.Connect(id,cancel.Token));
            else Fixtures.Throws<IOException>(()=>worker.Connect(id,cancel.Token));
            worker.Dispose();
            Fixtures.Check(worker.IsDisposed&&timer.Elapsed<TimeSpan.FromSeconds(5),"Command write or cleanup exceeded the deadline.");
        }));
        tests.Add(("failed WPD session requires a new inventory before any further object read",()=>
        {
            int connections=0;
            WorkerClient Create()=>new(Environment.ProcessPath!,++connections==1
                ?"--adversarial-usb-stale-session":"--adversarial-usb-remapped-session");
            using var source=new ReconnectingUsbSource(Create,SelectedDevice);
            Fixtures.Throws<SourceUnavailableException>(()=>source.OpenRead(new("reused","parent","stale.mov",MediaKind.Video,1),default));
            Fixtures.Check(connections==0,"An object ID was accepted before this session produced an inventory.");
            var inventory=source.Inventory(default);
            using(var stream=source.OpenRead(inventory.Items[0],default))
                Fixtures.Throws<IOException>(()=>stream.ReadByte());
            Fixtures.Throws<SourceUnavailableException>(()=>source.OpenRead(inventory.Items[1],default));
            Fixtures.Throws<SourceUnavailableException>(()=>source.Inventory(default));
            Fixtures.Check(connections==1,"A failed session reconnected and accepted an obsolete object ID.");
            using var nextPass=new ReconnectingUsbSource(Create,SelectedDevice);
            var fresh=nextPass.Inventory(default);
            Fixtures.Check(fresh.Items.Single().Id=="fresh");
            using var recovered=nextPass.OpenRead(fresh.Items.Single(),default);
            Fixtures.Check(recovered.ReadByte()==42&&recovered.ReadByte()==-1&&connections==2);
        }));
        tests.Add(("remote per-file errors do not starve healthy videos in a full import session",()=>
        {
            using var destination=new Destination(Fixtures.Root());int connections=0;
            WorkerClient Create(){connections++;return new(Environment.ProcessPath!,"--adversarial-usb-mixed-files");}
            var session=new ImportSession(()=>new ReconnectingUsbSource(Create,SelectedDevice),destination){RetryDelay=TimeSpan.Zero};
            var report=session.Run(false,default);
            Fixtures.Check(session.Attempts==ImportSession.MaximumAttempts&&connections==ImportSession.MaximumAttempts,
                "A valid operation error must not replace the worker within an import pass.");
            Fixtures.Check(report.Count(Outcome.Error)==3&&report.Count(Outcome.AlreadyPresent)==2&&report.Count(Outcome.Pending)==0,report.Status);
            Fixtures.Check(report.Items.Where(item=>item.Outcome==Outcome.Error).All(item=>item.Detail.Contains("Synthetic unreadable file",StringComparison.Ordinal)));
            var files=Directory.GetFiles(destination.Videos);Fixtures.Check(files.Length==2,"Healthy files after the failures were not imported exactly once.");
            var hashes=files.Select(path=>VerifiedCopy.HashFile(path,default).Hash).ToHashSet(StringComparer.Ordinal);
            Fixtures.Check(hashes.SetEquals(new[]{101,102}.Select(seed=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Fixtures.Video(seed))))));
        }));
        foreach(bool cancelled in new[]{false,true})tests.Add(($"remote stream disposal preserves the primary {(cancelled?"cancellation":"copy error")}",()=>
        {
            using var worker=new WorkerClient(Environment.ProcessPath!,"--adversarial-usb-close-fails");
            worker.Connect(SelectedDevice,default);
            Exception primary=cancelled?new OperationCanceledException("Primary cancellation."):new IOException("Primary copy failure.");
            Exception? caught=null;
            try{using var stream=worker.OpenRead(new("fresh","parent","fresh.mov",MediaKind.Video,1),default);throw primary;}
            catch(Exception ex){caught=ex;}
            Fixtures.Check(ReferenceEquals(caught,primary),"Stream cleanup replaced the primary failure.");
            // Deferred cleanup failures still reach the next operation as a valid remote response.
            Fixtures.Throws<RemoteOperationException>(()=>worker.Inventory(default));
            Fixtures.Check(!worker.IsDisposed,"An operation error incorrectly invalidated a working pipe.");
        }));
        tests.Add(("repeated zero inventory progress is throttled and each inventory starts immediately",()=>
        {
            long now=0;List<int> updates=[];
            var progress=new InventoryProgressThrottle(updates.Add,()=>now);
            for(int n=0;n<10000;n++)progress.Report(0);
            Fixtures.Check(updates.SequenceEqual([0]));
            now=249;progress.Report(0);Fixtures.Check(updates.Count==1);
            now=250;progress.Report(0);Fixtures.Check(updates.Count==2);
            progress.Reset();progress.Report(0);Fixtures.Check(updates.Count==3);
            now=499;progress.Report(1);Fixtures.Check(updates.Count==3);
            now=500;progress.Report(1);Fixtures.Check(updates.SequenceEqual([0,0,0,1]));
        }));
    }
}
