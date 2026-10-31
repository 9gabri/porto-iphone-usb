using System.Text.Json;

namespace Porto.Core;

public sealed record CatalogEntry(string Id,string State,string Temp,string Final,string? Hash,long Length,SourceItem Source,string? Replaces=null);
public sealed class Catalog : IDisposable
{
    private readonly Sqlite db;
    private readonly string root;
    public Catalog(Destination destination)
    {
        root=destination.Root;
        db=new Sqlite(Path.Combine(destination.State,"catalog.db"),SqliteDatabaseKind.Catalog);
        try
        {
            db.Execute("CREATE TABLE IF NOT EXISTS files(id TEXT PRIMARY KEY,state TEXT NOT NULL,temp TEXT NOT NULL,final TEXT NOT NULL,hash TEXT,length INTEGER NOT NULL,source TEXT NOT NULL,replacement TEXT)");
            if(!db.Query("PRAGMA table_info(files)").Any(r=>r[1]=="replacement"))db.Execute("ALTER TABLE files ADD COLUMN replacement TEXT");
            db.Execute("CREATE INDEX IF NOT EXISTS ix_hash ON files(hash,length)");
            db.Execute("CREATE TABLE IF NOT EXISTS runs(id TEXT PRIMARY KEY,report TEXT NOT NULL)");
            db.Execute("CREATE TABLE IF NOT EXISTS run_items(run TEXT NOT NULL,item TEXT NOT NULL)");
        }
        catch {db.Dispose();throw;}
    }
    public void Save(CatalogEntry entry) => SaveAll(entry);
    public void SaveAll(params CatalogEntry[] entries)
    {
        foreach(var entry in entries)CatalogValidation.Validate(entry);
        db.Transaction(()=>{foreach(var entry in entries)Upsert(entry);});
    }
    private void Upsert(CatalogEntry entry)=>db.Execute(
        "INSERT INTO files VALUES(?,?,?,?,?,?,?,?) ON CONFLICT(id) DO UPDATE SET state=excluded.state,temp=excluded.temp,final=excluded.final,hash=excluded.hash,length=excluded.length,source=excluded.source,replacement=excluded.replacement",
        entry.Id,entry.State,entry.Temp,entry.Final,entry.Hash,entry.Length,JsonSerializer.Serialize(entry.Source),entry.Replaces);
    public List<CatalogEntry> Entries() => CatalogValidation.Read(db,hasReplacement:true,destinationRoot:root);
    public void SaveReport(string id,ImportReport report)=>db.Transaction(()=>db.Execute("INSERT INTO runs VALUES(?,?) ON CONFLICT(id) DO UPDATE SET report=excluded.report",id,JsonSerializer.Serialize(report)));
    public void AppendResult(string run,ReportItem item)=>db.Transaction(()=>db.Execute("INSERT INTO run_items VALUES(?,?)",run,JsonSerializer.Serialize(item)));
    public void Dispose()=>db.Dispose();
}
