using System.Text.Json;

namespace Porto.Core;

public sealed record CatalogEntry(string Id,string State,string Temp,string Final,string? Hash,long Length,SourceItem Source);
public sealed class Catalog : IDisposable
{
    private readonly Sqlite db;
    public Catalog(Destination destination)
    {
        db=new Sqlite(Path.Combine(destination.State,"catalog.db"));
        db.Execute("CREATE TABLE IF NOT EXISTS files(id TEXT PRIMARY KEY,state TEXT NOT NULL,temp TEXT NOT NULL,final TEXT NOT NULL,hash TEXT,length INTEGER NOT NULL,source TEXT NOT NULL)");
        db.Execute("CREATE INDEX IF NOT EXISTS ix_hash ON files(hash,length)");
        db.Execute("CREATE TABLE IF NOT EXISTS runs(id TEXT PRIMARY KEY,report TEXT NOT NULL)");
        db.Execute("CREATE TABLE IF NOT EXISTS run_items(run TEXT NOT NULL,item TEXT NOT NULL)");
    }
    public void Save(CatalogEntry entry) => db.Transaction(()=>db.Execute(
        "INSERT INTO files VALUES(?,?,?,?,?,?,?) ON CONFLICT(id) DO UPDATE SET state=excluded.state,temp=excluded.temp,final=excluded.final,hash=excluded.hash,length=excluded.length,source=excluded.source",
        entry.Id,entry.State,entry.Temp,entry.Final,entry.Hash,entry.Length,JsonSerializer.Serialize(entry.Source)));
    public List<CatalogEntry> Entries() => db.Query("SELECT id,state,temp,final,hash,length,source FROM files").Select(r=>
        new CatalogEntry(r[0]!,r[1]!,r[2]!,r[3]!,r[4],long.Parse(r[5]!,System.Globalization.CultureInfo.InvariantCulture),JsonSerializer.Deserialize<SourceItem>(r[6]!)!)).ToList();
    public void SaveReport(string id,ImportReport report)=>db.Transaction(()=>db.Execute("INSERT INTO runs VALUES(?,?) ON CONFLICT(id) DO UPDATE SET report=excluded.report",id,JsonSerializer.Serialize(report)));
    public void AppendResult(string run,ReportItem item)=>db.Transaction(()=>db.Execute("INSERT INTO run_items VALUES(?,?)",run,JsonSerializer.Serialize(item)));
    public void Dispose()=>db.Dispose();
}
