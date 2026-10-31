using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Porto.Core;
using Porto.Usb;

internal static unsafe class FinalUsbTests
{
    private const string SelectedDevice = "synthetic-final-selected-phone";
    private const int Disconnected = unchecked((int)0x8007048F);
    private static bool disconnectInMetadata;
    private static nint currentEnumerator;

    public static int Child(string mode)
    {
        if (mode.StartsWith("--final-usb-tied-", StringComparison.Ordinal)) return TiedItemsChild(mode);
        bool hangOpen = mode.StartsWith("--final-usb-hang-open-", StringComparison.Ordinal);
        bool hangRead = mode.StartsWith("--final-usb-hang-read-", StringComparison.Ordinal);
        string generation = mode[(mode.LastIndexOf('-') + 1)..];
        byte[] first = Fixtures.Video(202), second = Fixtures.Video(203);
        List<SourceItem> items =
        [
            new(generation + "-bad", generation + "-folder", "blocked.mov", MediaKind.Video, first.Length),
            new(generation + "-good-1", generation + "-folder", "healthy-one.mov", MediaKind.Video, first.Length),
            new(generation + "-good-2", generation + "-folder", "healthy-two.mov", MediaKind.Video, second.Length)
        ];
        using var writer = new BinaryWriter(Console.OpenStandardOutput());
        void Frame(byte kind, byte[] data) { writer.Write(kind); writer.Write(data.Length); writer.Write(data); writer.Flush(); }
        void Json<T>(T value) => Frame(0, JsonSerializer.SerializeToUtf8Bytes(value));
        string? current = null, line;
        int position = 0;
        while ((line = Console.ReadLine()) != null)
        {
            try
            {
                var command = JsonSerializer.Deserialize<Command>(line)!;
                switch (command.Op)
                {
                    case "connect":
                        if (command.Device != SelectedDevice) throw new IOException("Synthetic worker received another phone ID.");
                        Json(true);
                        break;
                    case "inventory": current = null; Json(new Inventory(items, [], true)); break;
                    case "open":
                        if (!items.Any(item => item.Id == command.Object)) throw new IOException("Stale synthetic object ID.");
                        current = command.Object; position = 0;
                        if (hangOpen && current == items[0].Id) Thread.Sleep(30000);
                        Json(true);
                        break;
                    case "read":
                        if (mode == "--final-usb-eof") return 77;
                        if (mode == "--final-usb-native-session") Com.Check(Disconnected);
                        if (hangRead && current == items[0].Id) Thread.Sleep(30000);
                        byte[] data = current == items[1].Id ? first : second;
                        int count = Math.Min(command.Count, data.Length - position);
                        Frame(2, data.AsSpan(position, count).ToArray()); position += count;
                        break;
                    case "close": current = null; Json(true); break;
                    default: throw new IOException("Unsupported synthetic command.");
                }
            }
            // The fatal response must originate from the same native HRESULT helper used by WPD.
            catch (SourceUnavailableException ex) { Frame(4, JsonSerializer.SerializeToUtf8Bytes(WorkerSessionError.From(ex))); }
            catch (IOException ex) { Frame(3, Encoding.UTF8.GetBytes(ex.Message)); }
        }
        return 0;
    }

    private static int[] BlockedOccurrences(string shape) => shape switch
    {
        "first" => [0],
        "middle" => [2],
        "siblings" => [1, 3],
        "mixed-singleton" => [0, 1],
        "mixed-pairs" => [0, 2],
        _ => throw new ArgumentException("Unknown tied-inventory shape.")
    };

    private static int ItemCount(string shape) => shape switch
    {
        "first" or "middle" => 5,
        "siblings" => 6,
        "mixed-singleton" => 3,
        "mixed-pairs" => 4,
        _ => throw new ArgumentException("Unknown tied-inventory shape.")
    };

    private static string ItemName(string shape, int index) => shape switch
    {
        "mixed-singleton" => index == 0 ? "a.mov" : "b.mov",
        "mixed-pairs" => index < 2 ? "a.mov" : "b.mov",
        _ => "same.mov"
    };

    private static int TiedItemsChild(string mode)
    {
        string suffix = mode["--final-usb-tied-".Length..];
        int separator = suffix.LastIndexOf('-');
        string shape = suffix[..separator], generation = suffix[(separator + 1)..];
        int count = ItemCount(shape);
        int[] blocked = BlockedOccurrences(shape);
        byte[][] videos = Enumerable.Range(0, count).Select(index => Fixtures.Video(300 + index)).ToArray();
        List<SourceItem> items = Enumerable.Range(0, count).Select(index =>
            new SourceItem($"generation-{generation}-{index}", "folder-" + generation, ItemName(shape, index), MediaKind.Video, videos[index].Length)).ToList();
        using var writer = new BinaryWriter(Console.OpenStandardOutput());
        void Frame(byte kind, byte[] data) { writer.Write(kind); writer.Write(data.Length); writer.Write(data); writer.Flush(); }
        void Json<T>(T value) => Frame(0, JsonSerializer.SerializeToUtf8Bytes(value));
        int current = -1, position = 0;
        string? line;
        while ((line = Console.ReadLine()) != null)
        {
            try
            {
                var command = JsonSerializer.Deserialize<Command>(line)!;
                switch (command.Op)
                {
                    case "connect":
                        if (command.Device != SelectedDevice) throw new IOException("Synthetic worker received another phone ID.");
                        Json(true); break;
                    case "inventory": current = -1; Json(new Inventory(items, [], true)); break;
                    case "open":
                        current = items.FindIndex(item => item.Id == command.Object); position = 0;
                        if (current < 0) throw new IOException("Stale synthetic ID in tied inventory.");
                        Json(true); break;
                    case "read":
                        if (current < 0) throw new IOException("No synthetic stream is open.");
                        if (blocked.Contains(current)) Thread.Sleep(30000);
                        int bytes = Math.Min(command.Count, videos[current].Length - position);
                        Frame(2, videos[current].AsSpan(position, bytes).ToArray()); position += bytes; break;
                    case "close": current = -1; Json(true); break;
                    default: throw new IOException("Unsupported synthetic command.");
                }
            }
            catch (IOException ex) { Frame(3, Encoding.UTF8.GetBytes(ex.Message)); }
        }
        return 0;
    }

    public static void Add(List<(string, Action)> tests)
    {
        // Microsoft WPD Error Constants documents these as lost/hung/stale sessions or IDs.
        // https://learn.microsoft.com/en-us/windows/win32/wpd_sdk/error-constants
        // COM connection/server termination: https://learn.microsoft.com/en-us/windows/win32/com/com-error-codes-3
        // CO_E_OBJNOTCONNECTED: https://learn.microsoft.com/en-us/windows/win32/com/com-error-codes-1
        foreach (uint value in new uint[] { 0x8007048F, 0x802A0002, 0x802A0006, 0x80070015, 0x80042003, 0x80042009,
            0x80010108, 0x80010006, 0x80010007, 0x80010012, 0x800401FD })
        {
            int hr = unchecked((int)value);
            tests.Add(($"final USB native session HRESULT {value:X8} is preserved and invalidates the session", () =>
            {
                try { Com.Check(hr); }
                catch (SourceUnavailableException ex)
                {
                    Fixtures.Check(ex.HResult == hr || ex.InnerException?.HResult == hr,
                        "The native HRESULT was replaced by a generic IOException code.");
                    return;
                }
                throw new Exception("Native session loss did not produce SourceUnavailableException.");
            }));
        }
        foreach (uint value in new uint[] { 0x80070002, 0x80070005, 0x8007138E, 0x80004005 })
        {
            int hr = unchecked((int)value);
            tests.Add(($"final USB per-file HRESULT {value:X8} does not terminate a healthy session", () =>
            {
                Exception? error = null;
                try { Com.Check(hr); }
                catch (IOException ex) { error = ex; }
                Fixtures.Check(error != null && error is not SourceUnavailableException,
                    "An ordinary file failure was promoted to loss of the whole USB session.");
            }));
        }
        tests.Add(("final USB missing WPD registration reports the Windows component instead of phone trust", () =>
        {
            const int hr = unchecked((int)0x80040154);
            IOException? error = null;
            try { Com.Check(hr); }
            catch (IOException ex) { error = ex; }
            Fixtures.Check(error != null && error.HResult == hr && error.Message.Contains("WPD", StringComparison.Ordinal)
                && error.Message.Contains("non registrato", StringComparison.Ordinal)
                && !error.Message.Contains("Sblocca", StringComparison.OrdinalIgnoreCase),
                "A missing Windows component was diagnosed as a locked or untrusted phone.");
        }));
        foreach (string property in new[] { "name", "kind", "length" })
        {
            tests.Add(($"final USB optional {property} property does not hide a lost native session", () =>
            {
                using var native = new Native();
                native.Set(8, (nint)(delegate* unmanaged[Stdcall]<nint, PropertyKey*, nint*, int>)&StringDisconnected);
                native.Set(28, (nint)(delegate* unmanaged[Stdcall]<nint, PropertyKey*, Guid*, int>)&GuidDisconnected);
                native.Set(14, (nint)(delegate* unmanaged[Stdcall]<nint, PropertyKey*, ulong*, int>)&LengthDisconnected);
                using var values = new Com(native.Pointer);
                switch (property)
                {
                    case "name": Fixtures.Throws<SourceUnavailableException>(() => values.String(default)); break;
                    case "kind": Fixtures.Throws<SourceUnavailableException>(() => values.GuidValue(default)); break;
                    default: Fixtures.Throws<SourceUnavailableException>(() => values.Length(default)); break;
                }
            }));
        }
        foreach (bool metadata in new[] { false, true })
        {
            tests.Add(($"final USB native disconnect escapes {(metadata ? "metadata" : "folder")} inventory recovery", () =>
            {
                disconnectInMetadata = metadata;
                using var device = new Native(); using var content = new Native();
                using var properties = new Native(); using var resources = new Native(); using var iterator = new Native();
                currentEnumerator = iterator.Pointer;
                content.Set(3, (nint)(delegate* unmanaged[Stdcall]<nint, uint, char*, nint, nint*, int>)&Enumerate);
                properties.Set(5, (nint)(delegate* unmanaged[Stdcall]<nint, char*, nint, nint*, int>)&Values);
                iterator.Set(3, (nint)(delegate* unmanaged[Stdcall]<nint, uint, nint*, uint*, int>)&Next);
                using var source = new WpdSource(new(device.Pointer), new(content.Pointer), new(properties.Pointer), new(resources.Pointer));
                Fixtures.Throws<SourceUnavailableException>(() => source.Inventory(default));
            }));
        }
        foreach (string mode in new[] { "eof", "native-session" })
        {
            tests.Add(($"final USB {mode} during Read reports session loss before blaming the next healthy file", () =>
            {
                int connections = 0;
                WorkerClient Create() { connections++; return new(Environment.ProcessPath!, "--final-usb-" + mode); }
                using var source = new ReconnectingUsbSource(Create, SelectedDevice);
                var inventory = source.Inventory(default);
                using var stream = source.OpenRead(inventory.Items[0], default);
                SourceUnavailableException? loss = null;
                try { stream.ReadByte(); }
                catch (SourceUnavailableException ex) { loss = ex; }
                Fixtures.Check(loss != null, "The interrupted read did not report session loss on the current file.");
                if (mode == "native-session") Fixtures.Check(loss!.InnerException?.HResult == Disconnected,
                    "The native HRESULT was lost while crossing the worker protocol.");
                Fixtures.Throws<SourceUnavailableException>(() => source.OpenRead(inventory.Items[1], default));
                Fixtures.Check(connections == 1, "A lost session silently reconnected while retaining its old IDs.");
            }));
        }
        foreach (string operation in new[] { "open", "read" })
        {
            tests.Add(($"final USB hanging first {operation} does not starve later healthy videos across bounded attempts", () =>
            {
                using var destination = new Destination(Fixtures.Root());
                using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                int connections = 0;
                WorkerClient Create() => new(Environment.ProcessPath!, $"--final-usb-hang-{operation}-{++connections}")
                    { Timeout = TimeSpan.FromMilliseconds(700) };
                var session = new ImportSession(() => new ReconnectingUsbSource(Create, SelectedDevice), destination)
                    { RetryDelay = TimeSpan.Zero };
                var timer = Stopwatch.StartNew();
                var report = session.Run(false, watchdog.Token);
                Fixtures.Check(!watchdog.IsCancellationRequested && connections == ImportSession.MaximumAttempts,
                    "Recovery exceeded the three-attempt budget or stopped without retrying the failed item.");
                var files = Directory.GetFiles(destination.Videos);
                Fixtures.Check(files.Length == 2, $"The same first file starved healthy videos: {files.Length} copied in {connections} attempts.");
                var hashes = files.Select(path => VerifiedCopy.HashFile(path, default).Hash).ToHashSet(StringComparer.Ordinal);
                Fixtures.Check(hashes.SetEquals(new[] { 202, 203 }.Select(seed =>
                    Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Fixtures.Video(seed))))),
                    "Fresh inventories used an obsolete ID or imported the wrong content.");
                Fixtures.Check(!report.Items.Any(item => item.Name.StartsWith("healthy-", StringComparison.Ordinal) && item.Outcome == Outcome.Error),
                    "A healthy file was blamed for the prior file's lost USB session.");
                Fixtures.Check(!report.Status.StartsWith("Completati", StringComparison.Ordinal) && timer.Elapsed < TimeSpan.FromSeconds(15),
                    "The unresolved file was reported complete or recovery was unbounded.");
            }));
        }
        foreach (string shape in new[] { "first", "middle", "siblings", "mixed-singleton", "mixed-pairs" })
        {
            tests.Add(($"final USB tied metadata {shape} failures rotate past the failed original occurrence", () =>
            {
                using var destination = new Destination(Fixtures.Root());
                using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                int connections = 0;
                WorkerClient Create() => new(Environment.ProcessPath!, $"--final-usb-tied-{shape}-{++connections}")
                    { Timeout = TimeSpan.FromMilliseconds(700) };
                var session = new ImportSession(() => new ReconnectingUsbSource(Create, SelectedDevice), destination)
                    { RetryDelay = TimeSpan.Zero };
                var report = session.Run(false, watchdog.Token);
                Fixtures.Check(!watchdog.IsCancellationRequested && connections == ImportSession.MaximumAttempts,
                    "Recovery exceeded the three-session budget or silently skipped the blocked item.");
                int[] blocked = BlockedOccurrences(shape);
                int[] healthy = Enumerable.Range(0, ItemCount(shape)).Except(blocked).ToArray();
                var files = Directory.GetFiles(destination.Videos);
                Fixtures.Check(files.Length == healthy.Length,
                    $"Same-name/same-size {shape} failures starved healthy siblings: {files.Length}/{healthy.Length} copied.");
                var hashes = files.Select(path => VerifiedCopy.HashFile(path, default).Hash).ToHashSet(StringComparer.Ordinal);
                Fixtures.Check(hashes.SetEquals(healthy.Select(index =>
                    Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Fixtures.Video(300 + index))))),
                    "Retry ordering reused a stale ID, skipped a healthy sibling, or treated matching metadata as content identity.");
                Fixtures.Check(report.Items.Where(item => item.Outcome == Outcome.Error).All(item =>
                    blocked.Any(index => item.SourceId.EndsWith("-" + index, StringComparison.Ordinal))),
                    "A healthy sibling was blamed for a previous item's lost USB session.");
                Fixtures.Check(report.Items.Any(item => item.Outcome is Outcome.Error or Outcome.Pending)
                    && !report.Status.StartsWith("Completati", StringComparison.Ordinal),
                    "Unresolved blocked siblings disappeared from the final report.");
            }));
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint Release(nint self) => 0;
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Enumerate(nint self, uint flags, char* parent, nint filter, nint* result)
    { *result = disconnectInMetadata ? currentEnumerator : 0; return disconnectInMetadata ? 0 : Disconnected; }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Next(nint self, uint count, nint* ids, uint* fetched)
    { ids[0] = Marshal.StringToCoTaskMemUni("synthetic-lost-object"); *fetched = 1; return 1; }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Values(nint self, char* id, nint keys, nint* result)
    { *result = 0; return Disconnected; }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int StringDisconnected(nint self, PropertyKey* key, nint* result)
    { *result = 0; return Disconnected; }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int GuidDisconnected(nint self, PropertyKey* key, Guid* result)
    { *result = default; return Disconnected; }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int LengthDisconnected(nint self, PropertyKey* key, ulong* result)
    { *result = 0; return Disconnected; }

    private sealed class Native : IDisposable
    {
        private readonly nint table = (nint)NativeMemory.AllocZeroed(40, (nuint)IntPtr.Size);
        public nint Pointer { get; } = (nint)NativeMemory.AllocZeroed((nuint)IntPtr.Size);
        public Native() { *(nint*)Pointer = table; Set(2, (nint)(delegate* unmanaged[Stdcall]<nint, uint>)&Release); }
        public void Set(int slot, nint method) => ((nint*)table)[slot] = method;
        public void Dispose() { NativeMemory.Free((void*)Pointer); NativeMemory.Free((void*)table); }
    }
}
