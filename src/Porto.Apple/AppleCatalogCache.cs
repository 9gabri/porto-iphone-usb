namespace Porto.Apple;

// At most one fully validated observation, in RAM only. No database bytes retained.
// Metadata alone never authorizes reuse: every DB/WAL byte must be freshly hashed.
public sealed class AppleCatalogCache
{
    private readonly object gate=new();
    private Entry? entry;
    internal AppleGalleryCatalog? Match(string device,AppleFileInfo db,AppleFileInfo? wal,AppleFileInfo? journal,string dbHash,string? walHash)
    {
        lock(gate)
        {
            var value=entry;
            return value!=null&&value.Device==device&&value.Database==db&&value.Wal==wal&&value.Journal==journal&&
                value.DatabaseHash==dbHash&&value.WalHash==walHash?value.Catalog:null;
        }
    }
    internal void Store(string device,AppleFileInfo db,AppleFileInfo? wal,AppleFileInfo? journal,string dbHash,string? walHash,AppleGalleryCatalog catalog)
    {lock(gate)entry=new(device,db,wal,journal,dbHash,walHash,catalog);}
    public void Clear(){lock(gate)entry=null;}
    private sealed record Entry(string Device,AppleFileInfo Database,AppleFileInfo? Wal,AppleFileInfo? Journal,string DatabaseHash,string? WalHash,AppleGalleryCatalog Catalog);
}
