using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Porto.Core;
using static Fixtures;

internal static class FinalStorageTests
{
    public static void Add(List<(string, Action)> tests)
    {
        tests.Add(("final storage Ready cannot relocate an unrelated file in the destination", () =>
        {
            using var destination = new Destination(Root());
            byte[] bytes = Video();
            var entry = Seed(destination, "Ready", bytes);
            string foreign = Path.Combine(destination.Root, "unrelated.bin");
            File.WriteAllBytes(foreign, bytes);
            Mutate(destination, "UPDATE files SET temp='unrelated.bin'");

            RunRejected(destination, new FakeSource([], []));

            Fixtures.Check(File.Exists(foreign) && File.ReadAllBytes(foreign).SequenceEqual(bytes), "Recovery must preserve the unrelated file and its original name.");
            Fixtures.Check(!File.Exists(destination.Resolve(entry.Final)), "An unowned temporary must never be published.");
        }));

        foreach (string relative in new[]
        {
            "unrelated-output.mov",
            Path.Combine("Video Porto", "unowned-name.mov"),
            Path.Combine("Video Porto", "nested", "V_clip_11111111111111111111111111111111.mov")
        })
        tests.Add(("final storage Ready final path must be an immediate generated video name: " + relative, () =>
        {
            using var destination = new Destination(Root());
            byte[] bytes = Video();
            var entry = Seed(destination, "Ready", bytes);
            string temporary = destination.Resolve(entry.Temp);
            File.WriteAllBytes(temporary, bytes);
            Directory.CreateDirectory(Path.GetDirectoryName(destination.Resolve(relative))!);
            Mutate(destination, "UPDATE files SET final=" + Quote(relative));

            RunRejected(destination, new FakeSource([], []));

            Fixtures.Check(File.Exists(temporary) && File.ReadAllBytes(temporary).SequenceEqual(bytes), "The owned temporary must remain available after refusing an invalid final path.");
            Fixtures.Check(!File.Exists(destination.Resolve(relative)), "Recovery must not publish outside the generated immediate video namespace.");
        }));

        tests.Add(("final storage Copying traversal ID cannot append to an unrelated prefix", () =>
        {
            using var destination = new Destination(Root());
            byte[] bytes = Video(), prefix = bytes[..10003];
            Seed(destination, "Copying", bytes);
            string foreign = Path.Combine(destination.Root, "unrelated.part");
            File.WriteAllBytes(foreign, prefix);
            string maliciousId = Path.Combine("..", "unrelated");
            string relative = Path.Combine(".porto", maliciousId + ".part");
            Mutate(destination, "UPDATE files SET id=" + Quote(maliciousId) + ",temp=" + Quote(relative));

            var source = FakeSource.One(bytes);
            RunRejected(destination, source);

            Fixtures.Check(File.ReadAllBytes(foreign).SequenceEqual(prefix), "A catalog ID must not turn a user file into an appendable Porto partial.");
            Fixtures.Check(source.Reads == 0, "Invalid catalog paths must be refused before reading or copying a payload.");
        }));

        tests.Add(("final storage Complete cannot adopt an unrelated in-root file", () =>
        {
            using var destination = new Destination(Root());
            byte[] bytes = Video();
            Seed(destination, "Complete", bytes);
            string foreign = Path.Combine(destination.Root, "unrelated.mov");
            File.WriteAllBytes(foreign, bytes);
            Mutate(destination, "UPDATE files SET final='unrelated.mov'");

            var source = FakeSource.One(bytes);
            RunRejected(destination, source);

            Fixtures.Check(source.Reads == 0, "All catalog paths must be validated before a foreign file can satisfy a copy obligation.");
            Fixtures.Check(File.ReadAllBytes(foreign).SequenceEqual(bytes));
        }));

        foreach (string json in new[]
        {
            "null", "{", "{}",
            "{\"Id\":\"1\",\"ParentId\":\"dcim\",\"Name\":\"clip.mov\"}",
            "{\"Id\":\"1\",\"ParentId\":\"dcim\",\"Name\":\"clip.mov\",\"Kind\":99}",
            "{\"Id\":\"1\",\"ParentId\":\"dcim\",\"Name\":\"clip.mov\",\"Kind\":0,\"Unknown\":true}",
            "{\"Id\":\"1\",\"Id\":\"2\",\"ParentId\":\"dcim\",\"Name\":\"clip.mov\",\"Kind\":0}"
        })
        tests.Add(("final storage invalid catalog source is rejected deliberately: " + json, () =>
        {
            using var destination = new Destination(Root());
            byte[] bytes = Video();
            var entry = Seed(destination, "Ready", bytes);
            string temporary = destination.Resolve(entry.Temp);
            File.WriteAllBytes(temporary, bytes);
            Mutate(destination, "UPDATE files SET source=" + Quote(json));

            RunRejected(destination, new FakeSource([], []));

            Fixtures.Check(File.Exists(temporary) && File.ReadAllBytes(temporary).SequenceEqual(bytes));
            Fixtures.Check(!File.Exists(destination.Resolve(entry.Final)));
        }));

        tests.Add(("final storage validates every row before recovering an earlier valid row", () =>
        {
            using var destination = new Destination(Root());
            byte[] bytes = Video();
            var first = Seed(destination, "Ready", bytes);
            var invalid = Seed(destination, "Ready", bytes);
            string temporary = destination.Resolve(first.Temp);
            File.WriteAllBytes(temporary, bytes);
            Mutate(destination, "UPDATE files SET final='unrelated-output.mov' WHERE id=" + Quote(invalid.Id));
            string path = Path.Combine(destination.State, "catalog.db");
            byte[] before = File.ReadAllBytes(path);

            RunRejected(destination, new FakeSource([], []));

            Fixtures.Check(File.ReadAllBytes(path).SequenceEqual(before), "Invalid rows must be rejected before configuring or writing the original catalog.");
            Fixtures.Check(File.Exists(temporary) && File.ReadAllBytes(temporary).SequenceEqual(bytes));
            Fixtures.Check(!File.Exists(destination.Resolve(first.Final)));
        }));

        foreach (bool journal in new[] { false, true })
        tests.Add(("final storage preflights every physical path before recovery: journal=" + journal, () =>
        {
            using var destination = new Destination(Root());
            byte[] bytes = Video();
            var first = Seed(destination, "Ready", bytes);
            var second = Seed(destination, "Ready", bytes);
            string firstTemp = destination.Resolve(first.Temp), firstFinal = destination.Resolve(first.Final);
            string secondTemp = destination.Resolve(second.Temp);
            File.WriteAllBytes(firstTemp, bytes);
            string unrelated = Path.Combine(Root(), "unrelated.bin");
            File.WriteAllBytes(unrelated, bytes);
            Fixtures.Check(CreateHardLink(secondTemp, unrelated, 0), "Could not create the synthetic second-row hard link.");
            string path = Path.Combine(destination.State, "catalog.db");
            byte[] before = File.ReadAllBytes(path), journalBytes = NonHotJournal(before);
            if (journal) File.WriteAllBytes(path + "-journal", journalBytes);

            RunRejected(destination, new FakeSource([], []));

            Fixtures.Check(File.ReadAllBytes(path).SequenceEqual(before), "All physical paths must pass before the original catalog is opened for writes.");
            Fixtures.Check(File.Exists(firstTemp) && File.ReadAllBytes(firstTemp).SequenceEqual(bytes), "Even an owned first-row payload must stay in place until every row's physical path passes.");
            Fixtures.Check(!File.Exists(firstFinal));
            Fixtures.Check(File.ReadAllBytes(unrelated).SequenceEqual(bytes));
            if (journal) Fixtures.Check(File.ReadAllBytes(path + "-journal").SequenceEqual(journalBytes), "Snapshot validation must use the original root and preserve its journal on rejection.");
        }));

        tests.Add(("final storage replacement cycles are rejected before any recovery writes", () =>
        {
            using var destination = new Destination(Root());
            byte[] bytes = Video();
            var first = Seed(destination, "Ready", bytes);
            var second = Seed(destination, "Ready", bytes);
            string firstTemp = destination.Resolve(first.Temp), secondTemp = destination.Resolve(second.Temp);
            File.WriteAllBytes(firstTemp, bytes);
            File.WriteAllBytes(secondTemp, bytes);
            Mutate(destination, "UPDATE files SET replacement=" + Quote(second.Id) + " WHERE id=" + Quote(first.Id) + ";UPDATE files SET replacement=" + Quote(first.Id) + " WHERE id=" + Quote(second.Id));
            string path = Path.Combine(destination.State, "catalog.db");
            byte[] before = File.ReadAllBytes(path);

            RunRejected(destination, new FakeSource([], []));

            Fixtures.Check(File.ReadAllBytes(path).SequenceEqual(before));
            Fixtures.Check(File.Exists(firstTemp) && File.Exists(secondTemp));
            Fixtures.Check(!File.Exists(destination.Resolve(first.Final)) && !File.Exists(destination.Resolve(second.Final)));
        }));

        tests.Add(("final storage acyclic replacement history remains recoverable", () =>
        {
            using var destination = new Destination(Root());
            byte[] bytes = Video();
            var oldest = Seed(destination, "Replaced", bytes);
            var middle = Seed(destination, "Missing", bytes);
            var current = Seed(destination, "Ready", bytes);
            Mutate(destination, "UPDATE files SET replacement=" + Quote(oldest.Id) + " WHERE id=" + Quote(middle.Id) + ";UPDATE files SET replacement=" + Quote(middle.Id) + " WHERE id=" + Quote(current.Id));
            File.WriteAllBytes(destination.Resolve(current.Temp), bytes);

            using var catalog = new Catalog(destination);
            new ImportEngine(new FakeSource([], []), destination).Recover(catalog, default);

            var entries = catalog.Entries().ToDictionary(entry => entry.Id);
            Fixtures.Check(entries[oldest.Id].State == "Replaced" && entries[middle.Id].State == "Replaced" && entries[current.Id].State == "Complete");
            Fixtures.Check(File.ReadAllBytes(destination.Resolve(current.Final)).SequenceEqual(bytes));
        }));

        tests.Add(("final storage NUL suffixes cannot be hidden by SQLite string marshaling", () =>
        {
            using var destination = new Destination(Root());
            byte[] bytes = Video();
            var entry = Seed(destination, "Ready", bytes);
            string temporary = destination.Resolve(entry.Temp);
            File.WriteAllBytes(temporary, bytes);
            Mutate(destination, "UPDATE files SET temp=temp||char(0)||'unrecognized suffix'");

            RunRejected(destination, new FakeSource([], []));

            Fixtures.Check(File.Exists(temporary) && File.ReadAllBytes(temporary).SequenceEqual(bytes));
            Fixtures.Check(!File.Exists(destination.Resolve(entry.Final)));
        }));

        tests.Add(("final storage two catalog IDs cannot adopt the same final path", () =>
        {
            using var destination = new Destination(Root());
            byte[] bytes = Video();
            var first = Seed(destination, "Complete", bytes);
            var second = Seed(destination, "Complete", bytes);
            File.WriteAllBytes(destination.Resolve(first.Final), bytes);
            Mutate(destination, "UPDATE files SET final=" + Quote(first.Final) + " WHERE id=" + Quote(second.Id));

            var source = FakeSource.One(bytes);
            RunRejected(destination, source);

            Fixtures.Check(source.Reads == 0, "One path cannot satisfy two distinct stored copy obligations.");
            Fixtures.Check(File.ReadAllBytes(destination.Resolve(first.Final)).SequenceEqual(bytes));
        }));

        foreach (var index in new (string Name, string Sql)[]
        {
            ("unique", "CREATE UNIQUE INDEX ix_hash ON files(hash,length)"),
            ("partial", "CREATE INDEX ix_hash ON files(hash,length) WHERE hash IS NOT NULL"),
            ("collation", "CREATE INDEX ix_hash ON files(hash COLLATE NOCASE,length)"),
            ("descending", "CREATE INDEX ix_hash ON files(hash DESC,length)")
        })
        tests.Add(("final storage foreign index semantics are rejected without mutation: " + index.Name, () =>
        {
            using var destination = new Destination(Root());
            string path = Path.Combine(destination.State, "catalog.db");
            FixtureDatabase.Execute(path, LegacyCatalog(false, false) + index.Sql + ";");
            byte[] before = File.ReadAllBytes(path);

            Fixtures.Throws<IOException>(() => { using var catalog = new Catalog(destination); });

            Fixtures.Check(File.ReadAllBytes(path).SequenceEqual(before), "Reject the foreign index before writing Porto ownership or schema settings.");
        }));

        tests.Add(("final storage an added CHECK constraint is not an owned legacy schema", () =>
        {
            using var destination = new Destination(Root());
            string path = Path.Combine(destination.State, "catalog.db");
            string foreignSchema = LegacyCatalog(false).Replace("state TEXT NOT NULL", "state TEXT NOT NULL CHECK(state<>'Complete')", StringComparison.Ordinal);
            FixtureDatabase.Execute(path, foreignSchema);
            byte[] before = File.ReadAllBytes(path);

            Fixtures.Throws<IOException>(() => { using var catalog = new Catalog(destination); });

            Fixtures.Check(File.ReadAllBytes(path).SequenceEqual(before), "A schema that forbids Porto's normal state transition must remain untouched.");
        }));

        foreach (bool replacement in new[] { false, true })
        tests.Add(("final storage actual generated legacy paths and schema remain readable: replacement=" + replacement, () =>
        {
            using var destination = new Destination(Root());
            byte[] bytes = Video();
            var source = FakeSource.One(bytes).Inventory(default).Items.Single();
            string id = Guid.NewGuid().ToString("N");
            string temp = Path.Combine(".porto", id + ".part");
            string final = Path.Combine("Video Porto", Destination.SafeStem(source.Name) + "_" + id + ".mov");
            string hash = Convert.ToHexString(SHA256.HashData(bytes));
            string values = string.Join(",", new[] { Quote(id), "'Complete'", Quote(temp), Quote(final), Quote(hash), bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), Quote(JsonSerializer.Serialize(source)) });
            FixtureDatabase.Execute(Path.Combine(destination.State, "catalog.db"), LegacyCatalog(replacement) + "INSERT INTO files VALUES(" + values + (replacement ? ",NULL" : "") + ");");
            File.WriteAllBytes(destination.Resolve(final), bytes);

            using var catalog = new Catalog(destination);
            var row = catalog.Entries().Single();

            Fixtures.Check(row.Id == id && row.State == "Complete" && row.Temp == temp && row.Final == final && row.Hash == hash && row.Source == source);
            Fixtures.Check(File.ReadAllBytes(destination.Resolve(final)).SequenceEqual(bytes));
        }));

        tests.Add(("final storage collision recovery keeps a fresh generated UUID suffix readable", () =>
        {
            using var destination = new Destination(Root());
            byte[] bytes = Video();
            var entry = Seed(destination, "Ready", bytes);
            string alternate = Path.Combine("Video Porto", Destination.SafeStem(entry.Source.Name) + "_" + Guid.NewGuid().ToString("N") + ".mov");
            Mutate(destination, "UPDATE files SET final=" + Quote(alternate));
            File.WriteAllBytes(destination.Resolve(entry.Temp), bytes);

            using var catalog = new Catalog(destination);
            new ImportEngine(new FakeSource([], []), destination).Recover(catalog, default);

            Fixtures.Check(catalog.Entries().Single().State == "Complete");
            Fixtures.Check(File.ReadAllBytes(destination.Resolve(alternate)).SequenceEqual(bytes));
        }));

        tests.Add(("final storage verified source rename preserves the original published filename", () =>
        {
            using var destination = new Destination(Root());
            byte[] bytes = Video();
            var first = new ImportEngine(FakeSource.One(bytes, id: "old-id", name: "first.MOV"), destination).Run(false, default);
            Fixtures.Check(first.Count(Outcome.Imported) == 1);
            string original = Directory.GetFiles(destination.Videos).Single();
            var renamed = FakeSource.One(bytes, id: "new-id", name: "renamed.MP4");

            var second = new ImportEngine(renamed, destination).Run(false, default);

            Fixtures.Check(second.Count(Outcome.AlreadyPresent) == 1, second.Status);
            Fixtures.Check(Directory.GetFiles(destination.Videos).SequenceEqual(new[] { original }));
            using var catalog = new Catalog(destination);
            var entry = catalog.Entries().Single();
            Fixtures.Check(entry.Source.Name == "renamed.MP4" && destination.Resolve(entry.Final) == original);
        }));

        foreach (bool foreignSchema in new[] { false, true })
        tests.Add(("final storage journal validation leaves no redundant database snapshots: foreign=" + foreignSchema, () =>
        {
            using var destination = new Destination(Root());
            using (var catalog = new Catalog(destination)) { }
            string path = Path.Combine(destination.State, "catalog.db"), journal = path + "-journal";
            if (foreignSchema) FixtureDatabase.Execute(path, "CREATE TABLE unexpected(value TEXT);");
            byte[] database = File.ReadAllBytes(path), journalBytes = NonHotJournal(database);
            File.WriteAllBytes(journal, journalBytes);
            string existing = Path.Combine(destination.State, ".porto-validation-unrelated");
            Directory.CreateDirectory(existing);
            string unrelated = Path.Combine(existing, "preserve.txt");
            File.WriteAllText(unrelated, "unrelated data");

            for (int attempt = 0; attempt < (foreignSchema ? 3 : 1); attempt++)
            {
                if (foreignSchema) Fixtures.Throws<IOException>(() => { using var catalog = new Catalog(destination); });
                else { using var catalog = new Catalog(destination); Fixtures.Check(catalog.Entries().Count == 0); }

                Fixtures.Check(Directory.GetDirectories(destination.State, ".porto-validation-*").SequenceEqual(new[] { existing }), "Dispose and remove only the current owned validation copy after success or failure; retries must not consume another database-sized allocation.");
                Fixtures.Check(File.ReadAllText(unrelated) == "unrelated data", "Cleanup must not touch an existing similarly named directory.");
                if (foreignSchema)
                {
                    Fixtures.Check(File.ReadAllBytes(path).SequenceEqual(database));
                    Fixtures.Check(File.ReadAllBytes(journal).SequenceEqual(journalBytes));
                }
            }
        }));
    }

    private static CatalogEntry Seed(Destination destination, string state, byte[] bytes)
    {
        var item = FakeSource.One(bytes).Inventory(default).Items.Single();
        string id = Guid.NewGuid().ToString("N");
        var entry = new CatalogEntry(id, state, Path.Combine(".porto", id + ".part"),
            state == "Copying" ? "" : Path.Combine("Video Porto", Destination.SafeStem(item.Name) + "_" + id + ".mov"),
            state == "Copying" ? null : Convert.ToHexString(SHA256.HashData(bytes)), state == "Copying" ? 0 : bytes.Length, item);
        using var catalog = new Catalog(destination);
        catalog.Save(entry);
        return entry;
    }

    private static void Mutate(Destination destination, string sql) => FixtureDatabase.Execute(Path.Combine(destination.State, "catalog.db"), sql);

    private static void RunRejected(Destination destination, FakeSource source)
    {
        ImportReport? report = null;
        try { report = new ImportEngine(source, destination).Run(false, default); }
        catch (Exception error) when (error is IOException or InvalidDataException) { }
        Fixtures.Check(report == null || !report.Status.StartsWith("Completati", StringComparison.Ordinal), "Invalid catalog data must produce a deliberate failure, never completion.");
    }

    private static string Quote(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static string LegacyCatalog(bool replacement, bool index = true) =>
        "CREATE TABLE files(id TEXT PRIMARY KEY,state TEXT NOT NULL,temp TEXT NOT NULL,final TEXT NOT NULL,hash TEXT,length INTEGER NOT NULL,source TEXT NOT NULL" + (replacement ? ",replacement TEXT" : "") + ");" +
        (index ? "CREATE INDEX ix_hash ON files(hash,length);" : "") +
        "CREATE TABLE runs(id TEXT PRIMARY KEY,report TEXT NOT NULL);CREATE TABLE run_items(run TEXT NOT NULL,item TEXT NOT NULL);";

    private static byte[] NonHotJournal(byte[] database)
    {
        uint pageSize = BinaryPrimitives.ReadUInt16BigEndian(database.AsSpan(16, 2));
        if (pageSize == 1) pageSize = 65536;
        byte[] header = new byte[512];
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(12), 123456);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(16), (uint)(database.Length / pageSize));
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(20), 512);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(24), pageSize);
        return header;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string link, string existing, nint reserved);

    private static class FixtureDatabase
    {
        public static void Execute(string path, string sql)
        {
            int opened = sqlite3_open_v2(path, out var database, 2 | 4, 0);
            try
            {
                Fixtures.Check(opened == 0, "Could not open the synthetic catalog fixture.");
                int result = sqlite3_exec(database, sql, 0, 0, out var error);
                try { Fixtures.Check(result == 0, "Catalog fixture SQL failed: " + Marshal.PtrToStringUTF8(error)); }
                finally { if (error != 0) sqlite3_free(error); }
            }
            finally { if (database != 0) sqlite3_close_v2(database); }
        }

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out nint database, int flags, nint vfs);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int sqlite3_exec(nint database, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, nint callback, nint context, out nint error);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int sqlite3_close_v2(nint database);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern void sqlite3_free(nint memory);
    }
}
