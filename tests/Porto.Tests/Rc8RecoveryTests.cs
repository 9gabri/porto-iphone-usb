using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Porto.Core;
using Porto.Usb;

// Synthetic subprocesses and content only; these tests never discover or access a phone.
internal static class Rc8RecoveryTests
{
    private const string Prefix="--rc8-recovery-";
    private const string Device="synthetic-rc8-device";
    private const int BadCount=6;
    private static byte[] Video(int index)=>Fixtures.Video(8000+index);

    public static int Child(string mode)
    {
        string[] arguments=mode[Prefix.Length..].Split(':');
        string scenario=arguments[0];
        int generation=int.Parse(arguments[1]);
        bool remap=scenario=="remap";
        var items=Enumerable.Range(0,9).Select(index=>new SourceItem(
            $"{(remap?generation:0)}-{index}","synthetic-parent","same.mov",MediaKind.Video,Video(index).Length)).ToList();
        using var writer=new BinaryWriter(Console.OpenStandardOutput());
        void Frame(byte kind,byte[] data){writer.Write(kind);writer.Write(data.Length);writer.Write(data);writer.Flush();}
        void Json<T>(T value)=>Frame(0,JsonSerializer.SerializeToUtf8Bytes(value));
        void Error(string message)=>Frame(3,Encoding.UTF8.GetBytes(message));
        void Fatal()=>Frame(4,JsonSerializer.SerializeToUtf8Bytes(WorkerSessionError.From(
            new SourceUnavailableException("Synthetic disconnected resource."))));
        int current=-1,position=0;
        string? line;
        while((line=Console.ReadLine())!=null)
        {
            var command=JsonSerializer.Deserialize<Command>(line)!;
            switch(command.Op)
            {
                case "connect":
                    if(command.Device==Device)Json(true);else Error("Another synthetic device was selected.");
                    break;
                case "inventory":current=-1;Json(new Inventory(items,[],true));break;
                case "open":
                    current=items.FindIndex(item=>item.Id==command.Object);position=0;
                    if(current<0){Error("Obsolete synthetic ID was opened.");break;}
                    if(current is >=1 and <=BadCount)
                    {
                        if(scenario=="hang-open")Thread.Sleep(30000);
                        if(scenario=="remap")
                        {if(current%2==0)Fatal();else Error("Synthetic unreadable resource.");break;}
                        if(scenario=="disconnect"){Fatal();break;}
                    }
                    Json(true);break;
                case "read":
                    if(scenario=="hang-read"&&current is >=1 and <=BadCount)Thread.Sleep(30000);
                    byte[] payload=Video(current);
                    int count=Math.Min(command.Count,payload.Length-position);
                    Frame(2,payload.AsSpan(position,count).ToArray());position+=count;break;
                case "close":current=-1;Json(true);break;
                default:Error("Unknown synthetic operation.");break;
            }
        }
        return 0;
    }

    public static void Add(List<(string,Action)> tests)
    {
        foreach(string scenario in new[]{"hang-open","hang-read","disconnect"})
            tests.Add(($"rc8 six {scenario} failures recover before later healthy videos in one pass",()=>
            {
                using var destination=new Destination(Fixtures.Root());
                using var watchdog=new CancellationTokenSource(TimeSpan.FromSeconds(20));
                int connections=0;
                WorkerClient Create()=>new(Environment.ProcessPath!,Prefix+scenario+":"+ ++connections)
                    {Timeout=TimeSpan.FromMilliseconds(700)};
                using var source=new ReconnectingUsbSource(Create,Device);
                var timer=Stopwatch.StartNew();
                var report=new ImportEngine(source,destination).Run(false,watchdog.Token);
                CheckSaved(destination,[0,7,8]);
                Fixtures.Check(!watchdog.IsCancellationRequested&&timer.Elapsed<TimeSpan.FromSeconds(20)
                    &&connections==BadCount+1&&report.UsbSessionRestarts==BadCount,
                    "Each rejected resource must consume exactly one isolated recovery before advancing.");
                Fixtures.Check(report.Count(Outcome.Error)==BadCount&&report.Count(Outcome.Imported)==3
                    &&report.Count(Outcome.Pending)==0&&report.FinalInventoryChecked,
                    "A failed session starved or blamed a later healthy resource.");
                Fixtures.Check(!report.Status.StartsWith("Completati",StringComparison.Ordinal),
                    "The six unreadable resources were silently reported complete.");
            }));

        tests.Add(("rc8 six failures with remapped IDs and tied metadata reach every healthy video",()=>
        {
            using var destination=new Destination(Fixtures.Root());
            using var watchdog=new CancellationTokenSource(TimeSpan.FromSeconds(20));
            int connections=0;
            WorkerClient Create()=>new(Environment.ProcessPath!,Prefix+"remap:"+ ++connections)
                {Timeout=TimeSpan.FromSeconds(3)};
            var session=new ImportSession(()=>new ReconnectingUsbSource(Create,Device),destination){RetryDelay=TimeSpan.Zero};
            var report=session.Run(false,watchdog.Token);
            CheckSaved(destination,[0,7,8]);
            Fixtures.Check(!watchdog.IsCancellationRequested&&session.Attempts==BadCount+1&&connections==2*(BadCount+1),
                "Fresh failures must advance the bounded continuation; each remap rejects the old inventory once.");
            Fixtures.Check(report.Items.Where(item=>item.Outcome==Outcome.Error).All(item=>
                int.TryParse(item.SourceId[(item.SourceId.LastIndexOf('-')+1)..],out int index)&&index is >=1 and <=BadCount),
                "An unopened healthy resource was blamed for old-session cleanup.");
            Fixtures.Check(report.Items.Any(item=>item.Outcome is Outcome.Error or Outcome.Pending)
                &&!report.Status.StartsWith("Completati",StringComparison.Ordinal),
                "Same metadata was mistaken for content identity or unresolved resources disappeared.");
        }));

        foreach(bool tied in new[]{false,true})
            tests.Add(($"rc8 continuation defers earlier healthy reads without skipping them; tied={tied}",()=>
            {
                const int leading=12,tail=2;
                using var destination=new Destination(Fixtures.Root());
                int attempts=0;
                Dictionary<int,int> opens=[];
                IVideoSource Create()
                {
                    int generation=++attempts;
                    var items=Enumerable.Range(0,leading+BadCount+tail).Select(index=>new SourceItem(
                        generation+"-"+index,"parent",tied?"same.mov":index+".mov",MediaKind.Video,Video(index).Length)).ToList();
                    return new FakeSource(items,[]){Reader=(item,_)=>
                    {
                        int index=int.Parse(item.Id[(item.Id.LastIndexOf('-')+1)..]);
                        opens[index]=opens.GetValueOrDefault(index)+1;
                        if(index>=leading&&index<leading+BadCount)throw new SourceUnavailableException("Synthetic blocked file.");
                        return new MemoryStream(Video(index),false);
                    }};
                }
                var session=new ImportSession(Create,destination){RetryDelay=TimeSpan.Zero};
                var report=session.Run(false,default);
                CheckSaved(destination,Enumerable.Range(0,leading).Concat(Enumerable.Range(leading+BadCount,tail)).ToArray());
                Fixtures.Check(attempts==BadCount+1&&Enumerable.Range(0,leading).All(index=>opens[index]==2),
                    "Earlier healthy files were reread in every continuation, or their final fresh reads were skipped.");
                Fixtures.Check(!report.Status.StartsWith("Completati",StringComparison.Ordinal));
            }));

        foreach(bool complete in new[]{false,true})
            tests.Add(($"rc8 metadata churn cannot expand the first inventory recovery budget; complete={complete}",()=>
            {
                using var destination=new Destination(Fixtures.Root());
                int attempts=0;
                var session=new ImportSession(()=>
                {
                    int generation=++attempts;
                    var items=Enumerable.Range(0,generation==1?2:7).Select(index=>new SourceItem(
                        generation+"-"+index,"p",generation+"-"+index+".mov",MediaKind.Video,Video(index).Length)).ToList();
                    return new FakeSource(items,[]){Errors=complete?[]:["Synthetic incomplete inventory."],
                        Reader=(_,_)=>throw new SourceUnavailableException("Synthetic perpetual new obstacle.")};
                },destination){RetryDelay=TimeSpan.Zero};
                var report=session.Run(false,default);
                Fixtures.Check(attempts==ImportSession.MaximumAttempts+(complete?2:0),
                    "Changing metadata grew the fixed recovery cap, or an incomplete inventory enabled extra continuations.");
                Fixtures.Check(Directory.GetFiles(destination.Videos).Length==0&&report.Count(Outcome.Pending)>0
                    &&!report.Status.StartsWith("Completati",StringComparison.Ordinal));
            }));

        tests.Add(("rc8 cancellation stops before the next recovery continuation",()=>
        {
            using var destination=new Destination(Fixtures.Root());
            using var cancel=new CancellationTokenSource();
            int attempts=0;
            var session=new ImportSession(()=>
            {
                int generation=++attempts;
                var items=Enumerable.Range(0,8).Select(index=>new SourceItem(generation+"-"+index,
                    "p",index+".mov",MediaKind.Video,Video(index).Length)).ToList();
                return new FakeSource(items,[]){Reader=(_,_)=>throw new SourceUnavailableException("Synthetic blocked resource.")};
            },destination){RetryDelay=TimeSpan.FromMilliseconds(1),Progress=progress=>
                {if(progress.Phase=="Recupero automatico"&&attempts==ImportSession.MaximumAttempts)cancel.Cancel();}};
            var report=session.Run(false,cancel.Token);
            Fixtures.Check(cancel.IsCancellationRequested&&attempts==ImportSession.MaximumAttempts
                &&Directory.GetFiles(destination.Videos).Length==0&&report.Count(Outcome.Pending)>0,
                "Pause opened another source or discarded the not-yet-read items.");
        }));

        tests.Add(("rc8 disconnected replacement inventories stop within ordinary retry budget",()=>
        {
            using var destination=new Destination(Fixtures.Root());
            int attempts=0;
            var session=new ImportSession(()=>++attempts==1
                ?new FakeSource([new("bad","p","bad.mov",MediaKind.Video,Video(0).Length),
                    new("good","p","good.mov",MediaKind.Video,Video(1).Length)],[])
                    {Reader=(_,_)=>throw new SourceUnavailableException("Synthetic disconnect.")}
                :new DisconnectedSource(),destination){RetryDelay=TimeSpan.Zero};
            var report=session.Run(false,default);
            Fixtures.Check(attempts==ImportSession.MaximumAttempts&&report.Count(Outcome.Error)==1
                &&report.Count(Outcome.Pending)==1&&Directory.GetFiles(destination.Videos).Length==0
                &&!report.Status.StartsWith("Completati",StringComparison.Ordinal),
                "Unsuccessful inventories gained progress credit or erased the prior unread observations.");
        }));

        tests.Add(("rc8 changed final inventories alone cannot grant recovery continuation",()=>
        {
            using var destination=new Destination(Fixtures.Root());
            int attempts=0;
            var session=new ImportSession(()=>new ChangingFinalInventory(++attempts),destination){RetryDelay=TimeSpan.Zero};
            var report=session.Run(false,default);
            Fixtures.Check(attempts==ImportSession.MaximumAttempts&&!report.ReadRecoveryInterrupted
                &&!report.FinalInventoryChecked&&report.Count(Outcome.Pending)>0,
                "A completed pass with an unstable final inventory gained interrupted-read progress credit.");
        }));
    }

    private static void CheckSaved(Destination destination,int[] indices)
    {
        var files=Directory.GetFiles(destination.Videos);
        var expected=indices.Select(index=>Convert.ToHexString(SHA256.HashData(Video(index)))).ToHashSet(StringComparer.Ordinal);
        var actual=files.Select(path=>VerifiedCopy.HashFile(path,default).Hash).ToHashSet(StringComparer.Ordinal);
        Fixtures.Check(files.Length==indices.Length&&actual.SetEquals(expected),
            "Healthy resources were missing, duplicated, or copied from obsolete IDs.");
    }

    private sealed class DisconnectedSource:IVideoSource
    {
        public Inventory Inventory(CancellationToken cancel)=>throw new SourceUnavailableException("Synthetic device unavailable.");
        public Stream OpenRead(SourceItem item,CancellationToken cancel)=>throw new Exception("No item may open without inventory.");
        public void Dispose(){}
    }

    private sealed class ChangingFinalInventory(int generation):IVideoSource
    {
        private int inventories;
        public Inventory Inventory(CancellationToken cancel)=>new(Enumerable.Range(0,++inventories==1?1:2)
            .Select(index=>new SourceItem(generation+"-"+index,"p",generation+"-"+index+".mov",MediaKind.Video,Video(index).Length)).ToList(),[],true);
        public Stream OpenRead(SourceItem item,CancellationToken cancel)=>throw new IOException("Synthetic per-file rejection.");
        public void Dispose(){}
    }
}
