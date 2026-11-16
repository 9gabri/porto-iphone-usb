using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Porto.Core;
using Porto.Usb;

// Process/pipe integration fixtures only: no COM activation, discovery or personal media.
internal static class Rc7RecoveryTests
{
    private const string SelectedDevice = "synthetic-rc7-selected-phone";
    private const string Prefix = "--rc7-recovery-";

    public static int Child(string mode)
    {
        string[] arguments = mode[Prefix.Length..].Split(':');
        string scenario = arguments[0];
        int generation = int.Parse(arguments[1]);
        int badCount = scenario == "clean" ? 0 : scenario == "many" ? 5 : 1;
        int healthyCount = scenario == "finalchange" ? 0 : scenario == "clean" ? 2 : 3;
        byte[] badPayload = Fixtures.Video(500);
        List<SourceItem> items = [];
        for (int index = 0; index < badCount; index++)
            items.Add(new("bad-" + index, "synthetic-parent", "blocked-" + index + ".mov", MediaKind.Video,
                badPayload.Length, "synthetic-persistent-bad-" + index));
        for (int index = 0; index < healthyCount; index++)
            items.Add(new("good-" + index, "synthetic-parent", "healthy-" + index + ".mov", MediaKind.Video,
                Fixtures.Video(1000 + index).Length, "synthetic-persistent-good-" + index));

        if (generation > 1)
        {
            if (scenario == "remap")
                items = items.Select(item => item with { Id = "fresh-" + item.Id, ParentId = "fresh-parent" }).ToList();
            if (scenario == "metadata")
                items[1] = items[1] with { PersistentId = "replacement-persistent-id", Format = "changed-format" };
            if (scenario == "duplicate") items.Add(items[^1]);
            if (scenario == "reorder") items.Reverse();
            if (scenario == "finalchange")
                items.Add(new("new-video", "synthetic-parent", "new-video.mov", MediaKind.Video,
                    Fixtures.Video(1000).Length, "new-persistent-id"));
        }

        using var writer = new BinaryWriter(Console.OpenStandardOutput());
        void Frame(byte kind, byte[] data)
        { writer.Write(kind); writer.Write(data.Length); writer.Write(data); writer.Flush(); }
        void Json<T>(T value) => Frame(0, JsonSerializer.SerializeToUtf8Bytes(value));
        void Error(string message) => Frame(3, Encoding.UTF8.GetBytes(message));
        bool poisoned = false;
        int position = 0;
        byte[] payload = [];
        Dictionary<string, int> opens = [];
        string? line;
        while ((line = Console.ReadLine()) != null)
        {
            var command = JsonSerializer.Deserialize<Command>(line)!;
            switch (command.Op)
            {
                case "connect":
                    if (command.Device != SelectedDevice) Error("Synthetic worker received another phone ID.");
                    else Json(true);
                    break;
                case "inventory":
                    if (scenario == "cancel" && generation > 1) Thread.Sleep(30000);
                    bool incomplete = scenario == "incomplete" && generation > 1;
                    Json(new Inventory(items, incomplete ? ["Synthetic incomplete replacement inventory."] : [], !incomplete));
                    break;
                case "open":
                    var selected = items.SingleOrDefault(item => item.Id == command.Object);
                    if (selected == null) { Error("Stale synthetic object ID was opened."); break; }
                    position = 0;
                    opens[selected.Id] = opens.GetValueOrDefault(selected.Id) + 1;
                    if (poisoned)
                    {
                        // Cleanup can return success while the driver remains unusable.
                        // Keep poisoning every following read until the process is replaced.
                        payload = [17, 18]; Json(true); break;
                    }
                    if (selected.Name.StartsWith("blocked-", StringComparison.Ordinal))
                    {
                        switch (scenario)
                        {
                            case "oversize": payload = badPayload.Concat(new byte[] { 43 }).ToArray(); poisoned = true; break;
                            case "short": payload = badPayload[..^17]; poisoned = true; break;
                            case "hash":
                                payload = opens[selected.Id] == 1 ? badPayload : Fixtures.Video(501);
                                poisoned = opens[selected.Id] > 1;
                                break;
                            default:
                                poisoned = true;
                                Error("Synthetic triggering file error.");
                                continue;
                        }
                    }
                    else
                    {
                        int index = int.Parse(selected.Name["healthy-".Length..^4]);
                        payload = Fixtures.Video(1000 + index);
                    }
                    Json(true);
                    break;
                case "read":
                    int count = Math.Min(command.Count, payload.Length - position);
                    Frame(2, payload.AsSpan(position, count).ToArray()); position += count;
                    break;
                case "close":
                    // Deliberately acknowledge cleanup without clearing poisoned state.
                    Json(true);
                    break;
                default: Error("Unsupported synthetic command."); break;
            }
        }
        return 0;
    }

    public static void Add(List<(string, Action)> tests)
    {
        foreach (string scenario in new[] { "open", "oversize", "short", "hash", "many", "reorder" })
            tests.Add(($"rc7 isolated recovery after {scenario} failure preserves later healthy files in one pass", () =>
            {
                using var destination = new Destination(Fixtures.Root());
                using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                int connections = 0;
                WorkerClient Create() => new(Environment.ProcessPath!, Prefix + scenario + ":" + ++connections)
                    { Timeout = TimeSpan.FromSeconds(3) };
                using var source = new ReconnectingUsbSource(Create, SelectedDevice);
                var report = new ImportEngine(source, destination).Run(true, watchdog.Token);
                int badCount = scenario == "many" ? 5 : 1;
                CheckSaved(destination, 3);
                Fixtures.Check(report.Count(Outcome.Imported) == 3 && report.Count(Outcome.Error) == badCount
                    && report.Count(Outcome.Pending) == 0, "A healthy file was skipped, blamed for the prior failure, or copied without verification.");
                Fixtures.Check(connections == badCount + 1 && !watchdog.IsCancellationRequested,
                    "Recovery must replace each poisoned process once and remain bounded within one pass.");
                Fixtures.Check(report.UsbSessionRestarts==badCount && report.ApplicationVersion=="0.1.0-rc.9",
                    "The diagnostic report must identify this candidate and its actual session restart count.");
                Fixtures.Check(report.FinalInventoryChecked && !report.Status.StartsWith("Completati", StringComparison.Ordinal),
                    "The stable final inventory or the unresolved failed files were hidden.");
            }));

        foreach (string scenario in new[] { "remap", "metadata", "incomplete", "duplicate" })
            tests.Add(($"rc7 replacement inventory {scenario} keeps the unopened next video pending", () =>
            {
                using var destination = new Destination(Fixtures.Root());
                using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                int connections = 0;
                WorkerClient Create() => new(Environment.ProcessPath!, Prefix + scenario + ":" + ++connections)
                    { Timeout = TimeSpan.FromSeconds(3) };
                using var source = new ReconnectingUsbSource(Create, SelectedDevice);
                var report = new ImportEngine(source, destination).Run(true, watchdog.Token);
                Fixtures.Check(connections == 2 && !watchdog.IsCancellationRequested,
                    "Replacement validation did not occur or repeatedly reopened a changed inventory.");
                Fixtures.Check(Directory.GetFiles(destination.Videos).Length == 0 && report.Count(Outcome.Error) == 1,
                    "An obsolete observation was read, or the next unopened resource was blamed for recovery failure.");
                Fixtures.Check(report.Items.Where(item => item.Name.StartsWith("healthy-", StringComparison.Ordinal))
                    .All(item => item.Outcome == Outcome.Pending)
                    && report.Count(Outcome.Pending) == 3 && !report.Status.StartsWith("Completati", StringComparison.Ordinal),
                    "Changed, duplicate or incomplete replacement inventory discarded pending videos.");
            }));

        tests.Add(("rc7 final inventory after last failed video observes the new inventory", () =>
        {
            using var destination = new Destination(Fixtures.Root());
            int connections = 0;
            WorkerClient Create() => new(Environment.ProcessPath!, Prefix + "finalchange:" + ++connections)
                { Timeout = TimeSpan.FromSeconds(3) };
            using var source = new ReconnectingUsbSource(Create, SelectedDevice);
            var report = new ImportEngine(source, destination).Run(false, default);
            Fixtures.Check(connections == 2 && !report.FinalInventoryChecked
                && report.Items.Any(item => item.Name == "new-video.mov" && item.Outcome == Outcome.Pending),
                "Final inventory reused the poisoned worker or concealed the newly observed video.");
            Fixtures.Check(report.Count(Outcome.Error) == 1 && !report.Status.StartsWith("Completati", StringComparison.Ordinal));
        }));

        tests.Add(("rc7 clean reads retain one worker and exact verified copies", () =>
        {
            using var destination = new Destination(Fixtures.Root());
            int connections = 0;
            WorkerClient Create() => new(Environment.ProcessPath!, Prefix + "clean:" + ++connections);
            using var source = new ReconnectingUsbSource(Create, SelectedDevice);
            var report = new ImportEngine(source, destination).Run(true, default);
            CheckSaved(destination, 2);
            Fixtures.Check(connections == 1 && report.Count(Outcome.Imported) == 2
                && report.Status.StartsWith("Completati", StringComparison.Ordinal),
                "Successful deep verification unnecessarily replaced a worker or lost success.");
        }));

        tests.Add(("rc7 cancellation interrupts replacement inventory without blaming the next file", () =>
        {
            using var destination = new Destination(Fixtures.Root());
            using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
            List<WorkerClient> workers = [];
            int connections = 0;
            WorkerClient Create()
            {
                var worker = new WorkerClient(Environment.ProcessPath!, Prefix + "cancel:" + ++connections)
                    { Timeout = TimeSpan.FromSeconds(10) };
                workers.Add(worker); return worker;
            }
            using var source = new ReconnectingUsbSource(Create, SelectedDevice);
            var timer = Stopwatch.StartNew();
            var report = new ImportEngine(source, destination).Run(false, cancel.Token);
            source.Dispose();
            Fixtures.Check(cancel.IsCancellationRequested && connections == 2 && timer.Elapsed < TimeSpan.FromSeconds(6),
                "Cancellation did not interrupt the replacement inventory within its deadline.");
            Fixtures.Check(workers.All(worker => worker.IsDisposed) && report.Count(Outcome.Error) == 1
                && report.Count(Outcome.Pending) == 3 && !report.RetryRecommended,
                "Cancellation leaked a process or changed an unopened video into a transfer error.");
        }));

        tests.Add(("rc7 replacement factory failure preserves the original error and next pending item", () =>
        {
            using var destination = new Destination(Fixtures.Root());
            int connections = 0;
            WorkerClient Create()
            {
                if (++connections > 1) throw new IOException("Synthetic replacement process unavailable.");
                return new(Environment.ProcessPath!, Prefix + "open:1");
            }
            using var source = new ReconnectingUsbSource(Create, SelectedDevice);
            var report = new ImportEngine(source, destination).Run(false, default);
            Fixtures.Check(connections == 2 && report.Count(Outcome.Error) == 1 && report.Count(Outcome.Pending) == 3,
                "Replacement failure was attributed to the unopened next video or the original error disappeared.");
            Fixtures.Check(report.Items.Single(item => item.Outcome == Outcome.Error).Detail.Contains("Synthetic triggering file error.", StringComparison.Ordinal));
        }));

        tests.Add(("rc7 late old-stream disposal cannot affect a recovered worker",()=>
        {
            int connections=0;
            WorkerClient Create()=>new(Environment.ProcessPath!,Prefix+"open:"+ ++connections);
            using var source=new ReconnectingUsbSource(Create,SelectedDevice);
            var items=source.Inventory(default).Items;
            using var old=source.OpenRead(items[1],default);
            Fixtures.Throws<IOException>(()=>source.OpenRead(items[0],default));
            source.InvalidateRead();
            using var current=source.OpenRead(items[2],default);
            old.Dispose();
            using var output=new MemoryStream();current.CopyTo(output);
            Fixtures.Check(connections==2&&output.ToArray().SequenceEqual(Fixtures.Video(1001)));
        }));
        tests.Add(("rc7 recovery never opens a caller observation absent from the baseline",()=>
        {
            int connections=0;
            WorkerClient Create()=>new(Environment.ProcessPath!,Prefix+"clean:"+ ++connections);
            using var source=new ReconnectingUsbSource(Create,SelectedDevice);
            var items=source.Inventory(default).Items;
            source.InvalidateRead();
            Fixtures.Throws<PreviousReadCleanupException>(()=>source.OpenRead(items[0] with{Name="foreign.mov"},default));
            Fixtures.Check(connections==1,"A foreign observation caused a new connection or read.");
        }));
    }

    private static void CheckSaved(Destination destination, int count)
    {
        var files = Directory.GetFiles(destination.Videos);
        var expected = Enumerable.Range(0, count)
            .Select(index => Convert.ToHexString(SHA256.HashData(Fixtures.Video(1000 + index)))).ToHashSet(StringComparer.Ordinal);
        var actual = files.Select(path => VerifiedCopy.HashFile(path, default).Hash).ToHashSet(StringComparer.Ordinal);
        Fixtures.Check(files.Length == count && actual.SetEquals(expected),
            "Healthy resources did not produce exactly one byte-identical verified copy each.");
    }
}
