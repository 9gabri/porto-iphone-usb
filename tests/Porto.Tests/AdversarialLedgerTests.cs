using Porto.Core;
using System.Runtime.InteropServices;
using System.Text.Json;
using static Fixtures;

internal static class AdversarialLedgerTests
{
    public static void Add(List<(string, Action)> tests)
    {
        tests.Add(("ledger reverse return retains a co-observed duplicate", () =>
        {
            using var destination = new Destination(Root());
            var a = Item("a"); var b = Item("b");
            Import(destination, a);
            PauseAfterFirst(destination, b, a);
            var report = Import(destination, a);
            Fixtures.Check(Directory.GetFiles(destination.Videos).Length == 2, "Two observed videos require two physical copies.");
            Fixtures.Check(report.Status.StartsWith("Completati"), report.Status);
        }));
        tests.Add(("ledger alias chain cannot erase duplicate multiplicity", () =>
        {
            using var destination = new Destination(Root());
            var a = Item("a"); var b = Item("b"); var c = Item("c"); var d = Item("d");
            Import(destination, a);
            PauseAfterFirst(destination, b, a);
            Import(destination, c);
            Import(destination, d);
            var report = Import(destination, a);
            Fixtures.Check(Directory.GetFiles(destination.Videos).Length == 2, "Changing aliases must preserve the historical count of two.");
            Fixtures.Check(report.Status.StartsWith("Completati"), report.Status);
        }));
        tests.Add(("ledger previously equivalent aliases can later coexist", () =>
        {
            using var destination = new Destination(Root());
            var a = Item("a"); var b = Item("b");
            Import(destination, a);
            Import(destination, b);
            Fixtures.Check(Directory.GetFiles(destination.Videos).Length == 1, "An ordinary ID change should reuse its copy.");
            PauseAfterFirst(destination, a, b);
            var report = Import(destination, b);
            Fixtures.Check(Directory.GetFiles(destination.Videos).Length == 2, "Later coexistence supersedes an earlier possible alias.");
            Fixtures.Check(report.Status.StartsWith("Completati"), report.Status);
        }));
        tests.Add(("ledger final inventory preserves simultaneous duplicate debt", () =>
        {
            using var destination = new Destination(Root());
            var a = Item("a"); var b = Item("b");
            var first = new ImportEngine(new FinalInventorySource([a], [a, b]), destination).Run(false, default);
            Fixtures.Check(!first.FinalInventoryChecked && first.Count(Outcome.Pending) == 1, first.Status);
            var report = Import(destination, b);
            Fixtures.Check(Directory.GetFiles(destination.Videos).Length == 2, "Final inventory established two simultaneous videos.");
            Fixtures.Check(report.Status.StartsWith("Completati"), report.Status);
        }));
        tests.Add(("ledger verified duplicate counts survive several ordinary ID changes", () =>
        {
            using var destination = new Destination(Root());
            foreach (string prefix in new[] { "a", "b", "c" })
            {
                var report = Import(destination, Item(prefix + "1"), Item(prefix + "2"), Item(prefix + "3"));
                Fixtures.Check(report.Status.StartsWith("Completati"), report.Status);
                Fixtures.Check(Directory.GetFiles(destination.Videos).Length == 3, "Renaming three duplicates must not create more copies.");
            }
        }));
        tests.Add(("ledger corruption repair preserves old payload and clears future failure", () =>
        {
            using var destination = new Destination(Root());
            var a = Item("a");
            Import(destination, a);
            string damaged = Directory.GetFiles(destination.Videos).Single();
            File.WriteAllText(damaged, "synthetic damaged payload");
            var repaired = Import(destination, a);
            Fixtures.Check(repaired.Count(Outcome.Imported) == 1 && repaired.Count(Outcome.Error) == 1, repaired.Status);
            var report = Import(destination, a);
            Fixtures.Check(report.Count(Outcome.Error) == 0 && report.Status.StartsWith("Completati"), report.Status);
            Fixtures.Check(File.ReadAllText(damaged) == "synthetic damaged payload", "Damaged bytes must remain untouched.");
            Fixtures.Check(Directory.GetFiles(destination.Videos).Length == 2, "Repair should create only one replacement.");
        }));
        tests.Add(("ledger changed bytes under identical metadata remain explicitly ambiguous", () =>
        {
            using var destination = new Destination(Root());
            var a = Item("a");
            Import(destination, a);
            var changed = new FakeSource([a], new() { { a.Id, Video(2) } });
            var first = new ImportEngine(changed, destination).Run(false, default);
            Fixtures.Check(first.Count(Outcome.Imported) == 1 && first.Count(Outcome.Ambiguous) > 0, first.Status);
            var again = new ImportEngine(new FakeSource([a], new() { { a.Id, Video(2) } }), destination).Run(false, default);
            Fixtures.Check(again.Count(Outcome.Ambiguous) > 0 && !again.Status.StartsWith("Completati"), again.Status);
            Fixtures.Check(Directory.GetFiles(destination.Videos).Length == 2, "Both byte sequences must be preserved without another copy on retry.");
        }));
        tests.Add(("ledger current unprocessed item cannot be settled by its historical fingerprint", () =>
        {
            using var destination = new Destination(Root());
            var a = Item("a"); var b = Item("b");
            Import(destination, a);
            var source = new FakeSource([b, a], new() { { b.Id, Video() }, { a.Id, Video(2) } });
            using var cancel = new CancellationTokenSource();
            var report = new ImportEngine(source, destination)
            {
                Progress = progress => { if (progress.Phase == "Importazione" && source.Reads > 0) cancel.Cancel(); }
            }.Run(false, cancel.Token);
            Fixtures.Check(report.Items.Any(item => item.SourceId == a.Id && item.Outcome == Outcome.Pending), "The current unread a must remain pending.");
            var empty = Import(destination);
            Fixtures.Check(empty.Items.Any(item => item.SourceId == a.Id && item.Outcome == Outcome.Pending), "The obligation must also survive a later empty inventory.");
            var alias = Import(destination, Item("c"));
            Fixtures.Check(alias.Items.Any(item => item.SourceId == a.Id && item.Outcome == Outcome.Pending), "Reading c with the old hash cannot settle the latest unread a.");
            Fixtures.Check(!alias.Status.StartsWith("Completati"), alias.Status);
        }));
        tests.Add(("ledger disagreement on a requested second source read remains persistent", () =>
        {
            using var destination = new Destination(Root());
            var a = Item("a");
            Import(destination, a);
            var unstable = Source(a);
            unstable.Reader = (_, read) => new MemoryStream(Video(read == 1 ? 1 : 2));
            var rejected = new ImportEngine(unstable, destination).Run(true, default);
            Fixtures.Check(rejected.Count(Outcome.Error) == 1 && rejected.Count(Outcome.Ambiguous) > 0, rejected.Status);
            var alias = Import(destination, Item("c"));
            Fixtures.Check(alias.Count(Outcome.Ambiguous) > 0 && !alias.Status.StartsWith("Completati"), "An old matching hash cannot erase the witnessed disagreement.");
            Fixtures.Check(Directory.GetFiles(destination.Videos).Length == 1, "A failed second read must not publish another file.");
        }));
        tests.Add(("ledger rechecks the actual local copy after an earlier group verification", () =>
        {
            using var destination = new Destination(Root());
            var a = Item("a"); var b = Item("b"); var c = Item("c"); var d = Item("d");
            Import(destination, a, b);
            string changed;
            using (var catalog = new Catalog(destination)) changed = destination.Resolve(catalog.Entries().Single(entry => entry.Source.Id == b.Id).Final);
            bool mutated = false;
            var source = Source(c, d);
            source.Reader = (item, _) =>
            {
                if (item.Id == d.Id && !mutated)
                {
                    File.WriteAllBytes(changed, Video(2));
                    mutated = true;
                }
                return new MemoryStream(Video());
            };
            var report = new ImportEngine(source, destination).Run(false, default);
            Fixtures.Check(mutated && report.Count(Outcome.AlreadyPresent) == 1, "A stale cached hash must not classify d as already present.");
            Fixtures.Check(report.Count(Outcome.Imported) == 1 && report.Count(Outcome.Error) == 1, report.Status);
            Fixtures.Check(File.ReadAllBytes(changed).SequenceEqual(Video(2)), "The altered copy must be retained unchanged.");
        }));
        tests.Add(("ledger a multi-copy deficit retries until the historical count is restored", () =>
        {
            using var destination = new Destination(Root());
            var a = Item("a");
            Import(destination, a, Item("b"), Item("c"), Item("d"));
            foreach (string file in Directory.GetFiles(destination.Videos).Skip(1)) File.Move(file, file + ".synthetic-moved");
            var session = new ImportSession(() => Source(a), destination) { RetryDelay = TimeSpan.Zero };
            var report = session.Run(false, default);
            Fixtures.Check(session.Attempts == 3 && report.Status.StartsWith("Completati"), report.Status);
            Fixtures.Check(Directory.GetFiles(destination.Videos, "*.mov").Length == 4, "All four proven copies must be restored.");
        }));
        tests.Add(("ledger legacy pending debt without inventory provenance cannot disappear", () =>
        {
            using var destination = new Destination(Root());
            var a = Item("a"); var b = Item("b");
            byte[] bytes = Video();
            string relative = Path.Combine("Video Porto", "legacy.mov");
            File.WriteAllBytes(destination.Resolve(relative), bytes);
            var content = VerifiedCopy.Hash(new MemoryStream(bytes), default);
            using (var catalog = new Catalog(destination))
                catalog.Save(new("legacy", "Complete", Path.Combine(".porto", "legacy.part"), relative, content.Hash, content.Length, b));
            string Quote(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
            string Row(SourceItem item, string outcome) => $"({Quote(WorkLedger.Key(item))},{Quote(JsonSerializer.Serialize(item))},{Quote(outcome)},'synthetic rc2 work')";
            LegacyDatabase.Create(Path.Combine(destination.State, "work.db"),
                "CREATE TABLE work(id TEXT PRIMARY KEY,source TEXT NOT NULL,outcome TEXT NOT NULL,detail TEXT NOT NULL);" +
                "INSERT INTO work VALUES " + Row(a, "Pending") + "," + Row(b, "AlreadyPresent") + ";");
            var report = Import(destination, a);
            Fixtures.Check(!report.Status.StartsWith("Completati") && report.Count(Outcome.Ambiguous) > 0, report.Status);
            var empty = Import(destination);
            Fixtures.Check(empty.Count(Outcome.Ambiguous) > 0, "A later empty inventory cannot erase the legacy uncertainty.");
            Fixtures.Check(Directory.GetFiles(destination.Videos).Length == 1, "Migration must not invent an unproven duplicate count.");
        }));
        foreach (string phase in new[] { "after-ready", "after-rename", "after-complete" })
            tests.Add(($"ledger multiplicity recovery at {phase} reuses each copy once", () =>
            {
                using var destination = new Destination(Root());
                var a = Item("a"); var b = Item("b"); var c = Item("c"); var d = Item("d");
                Import(destination, a);
                new ImportEngine(Source(b, a), destination)
                {
                    FaultPoint = point => { if (point == phase) throw new OperationCanceledException(); }
                }.Run(false, default);
                var report = Import(destination, c, d);
                Fixtures.Check(report.Items.Any(item => item.SourceId == a.Id && item.Outcome == Outcome.Pending), "The latest a was never read, even though its earlier copy is intact.");
                Fixtures.Check(!report.Status.StartsWith("Completati"), report.Status);
                Fixtures.Check(Directory.GetFiles(destination.Videos).Length == 2, "Recovery must retain exactly two distinct complete copies.");
            }));
    }

    private static SourceItem Item(string id) => new(id, "p", "same.mov", MediaKind.Video, Video().LongLength);
    private static FakeSource Source(params SourceItem[] items) => new(items.ToList(), items.ToDictionary(i => i.Id, _ => Video()));
    private static ImportReport Import(Destination destination, params SourceItem[] items) =>
        new ImportEngine(Source(items), destination).Run(false, default);
    private static void PauseAfterFirst(Destination destination, params SourceItem[] items)
    {
        using var cancel = new CancellationTokenSource();
        var source = Source(items);
        var report = new ImportEngine(source, destination)
        {
            Progress = progress => { if (progress.Phase == "Importazione" && source.Reads > 0) cancel.Cancel(); }
        }.Run(false, cancel.Token);
        Fixtures.Check(cancel.IsCancellationRequested && report.Count(Outcome.Pending) > 0, "Fixture must pause with unprocessed work.");
    }
    private sealed class FinalInventorySource(List<SourceItem> initial, List<SourceItem> final) : IVideoSource
    {
        private int calls;
        public Inventory Inventory(CancellationToken cancel) => new(++calls == 1 ? initial : final, [], true);
        public Stream OpenRead(SourceItem item, CancellationToken cancel) => new MemoryStream(Video());
        public void Dispose() { }
    }

    private static class LegacyDatabase
    {
        public static void Create(string path, string sql)
        {
            int opened = sqlite3_open_v2(path, out var database, 2 | 4, 0);
            try
            {
                Fixtures.Check(opened == 0, "Could not create the synthetic legacy ledger.");
                int result = sqlite3_exec(database, sql, 0, 0, out var error);
                try { Fixtures.Check(result == 0, "Legacy fixture SQL failed: " + Marshal.PtrToStringUTF8(error)); }
                finally { if (error != 0) sqlite3_free(error); }
            }
            finally { if (database != 0) sqlite3_close_v2(database); }
        }
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out nint database, int flags, nint vfs);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_exec(nint database, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, nint callback, nint context, out nint error);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_close_v2(nint database);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void sqlite3_free(nint memory);
    }
}
