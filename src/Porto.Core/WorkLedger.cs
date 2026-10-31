using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Porto.Core;

// This ledger accounts for observations, never substitutes metadata for a content comparison.
// If IDs change before an item was read, its historical obligation remains explicitly unresolved.
public sealed class WorkLedger : IDisposable
{
    private readonly Sqlite db;
    public WorkLedger(Destination destination)
    {
        db = new Sqlite(Path.Combine(destination.State, "work.db"));
        db.Execute("CREATE TABLE IF NOT EXISTS work(id TEXT PRIMARY KEY,source TEXT NOT NULL,outcome TEXT NOT NULL,detail TEXT NOT NULL)");
    }
    public static string Key(SourceItem item) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(item))));
    public void Observe(IEnumerable<SourceItem> items) => db.Transaction(() =>
    {
        foreach (var item in items) db.Execute("INSERT INTO work VALUES(?,?,?,?) ON CONFLICT(id) DO UPDATE SET outcome=excluded.outcome,detail=excluded.detail",
            Key(item), JsonSerializer.Serialize(item), nameof(Outcome.Pending), "Video individuato, non ancora completato.");
    });
    public void Migrate(IEnumerable<CatalogEntry> entries) => db.Transaction(() =>
    {
        foreach (var entry in entries.Where(e => e.State is "Copying" or "Quarantined"))
            db.Execute("INSERT OR IGNORE INTO work VALUES(?,?,?,?)", Key(entry.Source), JsonSerializer.Serialize(entry.Source),
                entry.State == "Copying" ? nameof(Outcome.Pending) : nameof(Outcome.Ambiguous),
                "Tentativo precedente non concluso o ambiguo; temporaneo conservato.");
    });
    public void Record(SourceItem source, ReportItem result) => db.Execute("UPDATE work SET outcome=?,detail=? WHERE id=?",
        result.Outcome.ToString(), result.Detail, Key(source));
    public void Reconcile(SourceItem oldSource, SourceItem currentSource)
    {
        if(Key(oldSource)!=Key(currentSource))db.Execute("UPDATE work SET outcome='Reconciled',detail='Riconciliato tramite confronto completo del contenuto' WHERE id=?",Key(oldSource));
    }
    public List<(SourceItem Source, Outcome Outcome, string Detail)> Unresolved() => db.Query(
        "SELECT source,outcome,detail FROM work WHERE outcome NOT IN ('Imported','AlreadyPresent','Excluded','Reconciled')")
        .Select(r => (JsonSerializer.Deserialize<SourceItem>(r[0]!)!, Enum.Parse<Outcome>(r[1]!), r[2]!)).ToList();
    public void Dispose() => db.Dispose();
}
