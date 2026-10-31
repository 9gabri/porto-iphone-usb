using Porto.Core;
using static Fixtures;

internal static class FinalLedgerTests
{
    public static void Add(List<(string, Action)> tests)
    {
        tests.Add(("final ledger historical corruption stays visible without its source", () =>
        {
            using var destination = new Destination(Root());
            byte[] first = Video(1), second = Video(2), damaged = Video(3);
            var a = Item("a", first);
            var b = Item("b", second);
            var initial = new ImportEngine(Source((a, first), (b, second)), destination).Run(false, default);
            Fixtures.Check(initial.Count(Outcome.Imported) == 2 && initial.Status.StartsWith("Completati"), initial.Status);
            string historical;
            using (var catalog = new Catalog(destination))
                historical = destination.Resolve(catalog.Entries().Single(entry => entry.Source.Id == a.Id).Final);
            File.WriteAllBytes(historical, damaged);

            for (int pass = 0; pass < 2; pass++)
            {
                var report = new ImportEngine(Source((b, second)), destination).Run(false, default);
                Fixtures.Check(!report.Status.StartsWith("Completati"), "The missing source must not hide a corrupt historical copy: " + report.Status);
                Fixtures.Check(report.Items.Any(item => item.SourceId == a.Id && item.Outcome is Outcome.Error or Outcome.Missing),
                    "The corrupt historical copy must remain explicitly unresolved on every later run.");
                Fixtures.Check(File.ReadAllBytes(historical).SequenceEqual(damaged), "Historical damaged bytes must be preserved.");
            }
        }));

        tests.Add(("final ledger unknown initial observation survives a later absent item", () =>
        {
            using var destination = new Destination(Root());
            byte[] bytes = Video();
            var unknown = new SourceItem("unknown", "p", "unclassified", MediaKind.Unknown, bytes.LongLength);
            var uncertain = Source((unknown, bytes));
            uncertain.Errors.Add("Synthetic unknown USB type; content was not explored.");
            uncertain.Reader = (_, _) => throw new IOException("The synthetic unclassified resource cannot be read.");
            var initial = new ImportEngine(uncertain, destination).Run(false, default);
            AssertUnknownRemains(initial, unknown);

            var b = Item("b", bytes);
            var later = new ImportEngine(Source((b, bytes)), destination).Run(false, default);
            AssertUnknownRemains(later, unknown);
            Fixtures.Check(Directory.GetFiles(destination.Videos).Length == 1, "The later accessible video should still be copied.");
            AssertUnknownRemains(new ImportEngine(Source(), destination).Run(false, default), unknown);
        }));

        tests.Add(("final ledger unknown final observation survives a later absent item", () =>
        {
            using var destination = new Destination(Root());
            byte[] bytes = Video();
            var b = Item("b", bytes);
            var unknown = new SourceItem("unknown-final", "p", "unclassified", MediaKind.Unknown, bytes.LongLength);
            var source = new ChangedInventorySource([b], [b, unknown], new() { [b.Id] = bytes, [unknown.Id] = bytes });
            var initial = new ImportEngine(source, destination).Run(false, default);
            Fixtures.Check(!initial.FinalInventoryChecked && initial.InventoryErrors.Count > 0, "The final inventory must change.");

            var later = new ImportEngine(Source((b, bytes)), destination).Run(false, default);
            AssertUnknownRemains(later, unknown);
            Fixtures.Check(Directory.GetFiles(destination.Videos).Length == 1, "The stable known video must not be duplicated.");
        }));

        tests.Add(("final ledger reclassified metadata cannot settle an unread unknown observation", () =>
        {
            using var destination = new Destination(Root());
            byte[] bytes = Video();
            var unknown = new SourceItem("same-id", "p", "unclassified", MediaKind.Unknown, bytes.LongLength, "same-persistent-id");
            var uncertain = Source((unknown, bytes));
            uncertain.Reader = (_, _) => throw new IOException("The synthetic unclassified resource cannot be read.");
            AssertUnknownRemains(new ImportEngine(uncertain, destination).Run(false, default), unknown);

            var classified = unknown with { Kind = MediaKind.Video };
            var report = new ImportEngine(Source((classified, bytes)), destination).Run(false, default);
            Fixtures.Check(report.Count(Outcome.Imported) == 1, "The newly classified video must be copied.");
            AssertUnknownRemains(report, unknown);
            Fixtures.Check(Directory.GetFiles(destination.Videos).Length == 1, "Classification uncertainty must not invent another video copy.");
        }));
    }

    private static SourceItem Item(string id, byte[] bytes) => new(id, "p", id + ".mov", MediaKind.Video, bytes.LongLength);
    private static FakeSource Source(params (SourceItem Item, byte[] Bytes)[] items) =>
        new(items.Select(item => item.Item).ToList(), items.ToDictionary(item => item.Item.Id, item => item.Bytes));

    private static void AssertUnknownRemains(ImportReport report, SourceItem unknown)
    {
        Fixtures.Check(!report.Status.StartsWith("Completati"), "An unread unknown observation cannot disappear: " + report.Status);
        Fixtures.Check(report.Items.Any(item => item.SourceId == unknown.Id && item.Outcome is Outcome.Ambiguous or Outcome.Pending or Outcome.Error),
            "The previously seen unknown item must remain explicitly unresolved.");
    }

    private sealed class ChangedInventorySource(List<SourceItem> initial, List<SourceItem> final, Dictionary<string, byte[]> bytes) : IVideoSource
    {
        private int calls;
        public Inventory Inventory(CancellationToken cancel) => ++calls == 1
            ? new(initial, [], true)
            : new(final, ["Synthetic unknown USB type in final inventory."], false);
        public Stream OpenRead(SourceItem item, CancellationToken cancel) => new MemoryStream(bytes[item.Id], false);
        public void Dispose() { }
    }
}
