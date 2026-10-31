using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Porto.Core;

// Observations are not permanent identities: IDs can change, and former aliases can coexist later.
// A complete content read adds evidence to historical inventories. Their maximum simultaneous
// count is a lower bound on the number of distinct copies that must remain available.
public sealed class WorkLedger : IDisposable
{
    private readonly Sqlite db;
    private readonly Dictionary<string, CopyResult> fingerprints = new(StringComparer.Ordinal);
    private readonly Dictionary<CopyResult, int> minimumCopies = [];
    private const string LegacyDebtDetail = "Lavoro precedente senza inventario storico registrato: non è possibile certificare la molteplicità delle copie. Il debito storico resta da verificare anche se una copia corrente è presente.";
    public WorkLedger(Destination destination)
    {
        db = new Sqlite(Path.Combine(destination.State, "work.db"), SqliteDatabaseKind.Work);
        db.Execute("CREATE TABLE IF NOT EXISTS work(id TEXT PRIMARY KEY,source TEXT NOT NULL,outcome TEXT NOT NULL,detail TEXT NOT NULL)");
        db.Execute("CREATE TABLE IF NOT EXISTS snapshot_members(snapshot TEXT NOT NULL,source_id TEXT NOT NULL,PRIMARY KEY(snapshot,source_id))");
        db.Execute("CREATE TABLE IF NOT EXISTS fingerprints(source_id TEXT PRIMARY KEY,hash TEXT NOT NULL,length INTEGER NOT NULL)");
        db.Execute("CREATE TABLE IF NOT EXISTS snapshot_counts(snapshot TEXT NOT NULL,hash TEXT NOT NULL,length INTEGER NOT NULL,count INTEGER NOT NULL,PRIMARY KEY(snapshot,hash,length))");
        db.Execute("CREATE TABLE IF NOT EXISTS fingerprint_conflicts(source_id TEXT PRIMARY KEY,source TEXT NOT NULL,detail TEXT NOT NULL)");
        if (db.Query("SELECT name FROM sqlite_master WHERE type='table' AND name='unread_work'").Count == 0)
            db.Transaction(() =>
            {
                db.Execute("CREATE TABLE unread_work(id TEXT PRIMARY KEY)");
                db.Execute("INSERT INTO unread_work SELECT id FROM work WHERE outcome NOT IN ('Imported','AlreadyPresent','Excluded','Reconciled')");
            });
        db.Execute("CREATE INDEX IF NOT EXISTS ix_snapshot_source ON snapshot_members(source_id,snapshot)");
        db.Execute("CREATE INDEX IF NOT EXISTS ix_fingerprint_content ON fingerprints(hash,length)");
        db.Execute("CREATE INDEX IF NOT EXISTS ix_snapshot_content ON snapshot_counts(hash,length,count)");
        foreach (var row in db.Query("SELECT source_id,hash,length FROM fingerprints"))
            fingerprints.Add(row[0]!, new(ParseLong(row[2]), row[1]!));
        LoadMinimums();
    }
    public static string Key(SourceItem item) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(item))));

    public void Observe(IEnumerable<SourceItem> items, string snapshot, IReadOnlySet<string>? resetKeys = null)
    {
        db.Transaction(() =>
        {
            foreach (var item in items)
            {
                string key = Key(item);
                bool reset = resetKeys == null || resetKeys.Contains(key);
                string conflict = reset
                    ? " ON CONFLICT(id) DO UPDATE SET outcome=excluded.outcome,detail=excluded.detail"
                    : " ON CONFLICT(id) DO NOTHING";
                db.Execute("INSERT INTO work VALUES(?,?,?,?)" + conflict,
                    key, JsonSerializer.Serialize(item), nameof(Outcome.Pending), "Video individuato, non ancora completato.");
                db.Execute("INSERT OR IGNORE INTO snapshot_members VALUES(?,?)", snapshot, key);
                if (reset) db.Execute("INSERT OR IGNORE INTO unread_work VALUES(?)", key);
            }
            // One grouped scan per inventory; never materialize all pairs of co-observed items.
            db.Execute("INSERT INTO snapshot_counts SELECT m.snapshot,f.hash,f.length,COUNT(*) FROM snapshot_members m JOIN fingerprints f ON f.source_id=m.source_id WHERE m.snapshot=? GROUP BY m.snapshot,f.hash,f.length ON CONFLICT(snapshot,hash,length) DO UPDATE SET count=excluded.count", snapshot);
        });
        LoadMinimums();
    }

    public void Migrate(IEnumerable<CatalogEntry> entries)
    {
        var pending = new Dictionary<string, CopyResult>(StringComparer.Ordinal);
        db.Transaction(() =>
        {
            foreach (var entry in entries)
            {
                if (entry.State is "Copying" or "Quarantined")
                    db.Execute("INSERT OR IGNORE INTO work VALUES(?,?,?,?)", Key(entry.Source), JsonSerializer.Serialize(entry.Source),
                        entry.State == "Copying" ? nameof(Outcome.Pending) : nameof(Outcome.Ambiguous),
                        "Tentativo precedente non concluso o ambiguo; temporaneo conservato.");
                if (entry.Hash != null) RememberCore(entry.Source, new(entry.Length, entry.Hash), pending);
            }
            // Older ledgers did not retain co-observation evidence. A later singleton cannot
            // prove that their pending work was an alias rather than an additional video.
            // Preserve this uncertainty independently of subsequent per-item success.
            db.Execute("INSERT OR IGNORE INTO fingerprint_conflicts SELECT w.id,w.source,? FROM work w WHERE w.outcome NOT IN ('Imported','AlreadyPresent','Excluded','Reconciled') AND NOT EXISTS (SELECT 1 FROM snapshot_members m WHERE m.source_id=w.id)",
                LegacyDebtDetail);
        });
        foreach (var proof in pending) fingerprints.Add(proof.Key, proof.Value);
        LoadMinimums();
    }

    public void Remember(SourceItem source, CopyResult content)
    {
        string key = Key(source);
        if (fingerprints.TryGetValue(key, out var known) && known == content) return;
        var pending = new Dictionary<string, CopyResult>(StringComparer.Ordinal);
        db.Transaction(() => RememberCore(source, content, pending));
        foreach (var proof in pending) fingerprints.Add(proof.Key, proof.Value);
        if (pending.Count > 0)
        {
            var row = db.Query("SELECT MAX(count) FROM snapshot_counts WHERE hash=? AND length=?", content.Hash, content.Length).Single();
            minimumCopies[content] = row[0] == null ? 1 : checked((int)ParseLong(row[0]));
        }
    }

    // A historical hash is not evidence that the latest observation was read. Only the engine's
    // completed current read (including every requested verification) may clear this marker.
    public void ReadVerified(SourceItem source, CopyResult content)
    {
        Remember(source, content);
        db.Execute("DELETE FROM unread_work WHERE id=?", Key(source));
    }

    private void RememberCore(SourceItem source, CopyResult content, Dictionary<string, CopyResult> pending)
    {
        string key = Key(source);
        if (fingerprints.TryGetValue(key, out var known) || pending.TryGetValue(key, out known))
        {
            if (known != content)
                db.Execute("INSERT OR IGNORE INTO fingerprint_conflicts VALUES(?,?,?)", key, JsonSerializer.Serialize(source),
                    "La stessa osservazione USB ha restituito contenuti differenti. Le copie sono conservate, ma l'identità e la molteplicità precedenti restano da verificare.");
            return;
        }
        db.Execute("INSERT INTO fingerprints VALUES(?,?,?)", key, content.Hash, content.Length);
        db.Execute("INSERT INTO snapshot_counts SELECT snapshot,?,?,1 FROM snapshot_members WHERE source_id=? ON CONFLICT(snapshot,hash,length) DO UPDATE SET count=count+1", content.Hash, content.Length, key);
        pending.Add(key, content);
    }

    public int RequiredCopies(CopyResult content) => minimumCopies.GetValueOrDefault(content, 1);
    public IEnumerable<(CopyResult Content, int Count)> Requirements() => minimumCopies.Select(pair => (pair.Key, pair.Value));

    // Only full fingerprints and enough independently verified physical copies can settle old work.
    // Current observations still get their own per-item result, even if a previous copy exists.
    public void ReconcileKnown(CopyResult content, IReadOnlySet<string> currentKeys) => db.Transaction(() =>
    {
        foreach (var row in db.Query("SELECT w.id FROM work w JOIN fingerprints f ON f.source_id=w.id WHERE f.hash=? AND f.length=? AND w.outcome NOT IN ('Imported','AlreadyPresent','Excluded','Reconciled') AND NOT EXISTS (SELECT 1 FROM fingerprint_conflicts c WHERE c.source_id=w.id) AND NOT EXISTS (SELECT 1 FROM unread_work u WHERE u.id=w.id)", content.Hash, content.Length))
            if (!currentKeys.Contains(row[0]!))
                db.Execute("UPDATE work SET outcome='Reconciled',detail='Contenuto verificato con un numero sufficiente di copie distinte per tutti gli inventari osservati' WHERE id=?", row[0]);
    });

    public void Record(SourceItem source, ReportItem result) => db.Execute("UPDATE work SET outcome=?,detail=? WHERE id=?",
        result.Outcome.ToString(), result.Detail, Key(source));
    public List<(SourceItem Source, Outcome Outcome, string Detail)> Unresolved() => db.Query(
        "SELECT source,outcome,detail FROM work WHERE outcome NOT IN ('Imported','AlreadyPresent','Excluded','Reconciled')")
        .Select(r => (JsonSerializer.Deserialize<SourceItem>(r[0]!)!, Enum.Parse<Outcome>(r[1]!), r[2]!)).ToList();

    // Byte disagreements are always explicit. For legacy debt an unresolved item already blocks
    // success; once it settles, the independent uncertainty must still remain visible.
    public List<(SourceItem Source, string Detail)> Conflicts() => db.Query("SELECT c.source,c.detail FROM fingerprint_conflicts c LEFT JOIN work w ON w.id=c.source_id WHERE c.detail<>? OR w.id IS NULL OR w.outcome IN ('Imported','AlreadyPresent','Excluded','Reconciled')", LegacyDebtDetail)
        .Select(row => (JsonSerializer.Deserialize<SourceItem>(row[0]!)!, row[1]!)).ToList();

    private void LoadMinimums()
    {
        minimumCopies.Clear();
        foreach (var row in db.Query("SELECT hash,length,MAX(count) FROM snapshot_counts GROUP BY hash,length"))
            minimumCopies.Add(new(ParseLong(row[1]), row[0]!), checked((int)ParseLong(row[2])));
    }
    private static long ParseLong(string? value) => long.Parse(value!, System.Globalization.CultureInfo.InvariantCulture);
    public void Dispose() => db.Dispose();
}
