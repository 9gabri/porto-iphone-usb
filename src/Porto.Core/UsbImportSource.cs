using System.Security.Cryptography;
using System.Text;

namespace Porto.Core;

// Namespaces keep observations from separate devices apart. Full content reads remain mandatory.
public sealed class UsbImportSource : IVideoSource,IReadFailureRecovery,ISourceContext
{
    private readonly IVideoSource inner;
    private readonly string sourceNamespace;
    private readonly UsbFolderChoice? folder;
    private readonly UsbCollectionChoice? collection;
    private Dictionary<string,(SourceItem Public,SourceItem Raw)> observed=new(StringComparer.Ordinal);
    private bool disposed;
    public string SourceDescription {get;}
    public bool AppleResourceHints {get;}
    public int SessionRestarts=>(inner as IReadFailureRecovery)?.SessionRestarts??0;
    public UsbImportSource(IVideoSource inner,string deviceId,string deviceName,bool useAppleHints,UsbFolderChoice? folder=null,UsbCollectionChoice? collection=null)
    {
        if(string.IsNullOrWhiteSpace(deviceId)||deviceId.Length>32768||deviceId.Contains('\0'))
            throw new ArgumentException("Identificativo telefono non valido.",nameof(deviceId));
        if(folder!=null&&collection!=null)throw new ArgumentException("Scegli una cartella oppure una raccolta USB.");
        this.inner=inner;sourceNamespace="usb:"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(deviceId)));
        this.folder=folder==null?null:new(folder.DisplayPath,folder.Segments.ToArray());
        this.collection=collection==null?null:new(collection.DisplayPath,collection.Segments.ToArray());
        AppleResourceHints=useAppleHints;
        SourceDescription=$"{deviceName} tramite USB"+(collection!=null?$" — raccolta esposta da Windows: {collection.DisplayPath}":folder==null?" — tutti i file accessibili":$" — cartella fisica: {folder.DisplayPath}");
    }
    public Inventory Inventory(CancellationToken cancel)
    {
        ObjectDisposedException.ThrowIf(disposed,this);cancel.ThrowIfCancellationRequested();observed.Clear();
        var inventory=collection!=null
            ?inner is IUsbCollectionSource collections?collections.Inventory(collection,cancel)
                :throw new IOException("La sorgente non supporta le raccolte USB.")
            :folder==null?inner.Inventory(cancel)
            :inner is IUsbFolderSource scoped?scoped.Inventory(folder,cancel)
            :throw new IOException("La sorgente USB non supporta l’importazione limitata alla cartella scelta.");
        var items=folder==null?inventory.Items:UsbFolders.Select(inventory,folder);
        var next=new Dictionary<string,(SourceItem Public,SourceItem Raw)>(StringComparer.Ordinal);
        foreach(var raw in items)
        {
            cancel.ThrowIfCancellationRequested();
            var stamped=raw with{SourceNamespace=sourceNamespace};
            if(!next.TryAdd(stamped.Id,(stamped,raw)))throw new IOException("Identificativi USB duplicati: ripeti l'inventario.");
        }
        observed=next;
        return new(next.Values.Select(entry=>entry.Public).ToList(),[..inventory.Errors],inventory.EnumerationComplete);
    }
    public Stream OpenRead(SourceItem item,CancellationToken cancel)
    {
        ObjectDisposedException.ThrowIf(disposed,this);cancel.ThrowIfCancellationRequested();
        if(!observed.TryGetValue(item.Id,out var entry)||entry.Public!=item)
            throw new IOException("Il file non appartiene alla selezione corrente di questo telefono.");
        return inner.OpenRead(entry.Raw,cancel);
    }
    public void InvalidateRead()
    {
        if(inner is IReadFailureRecovery recovery)recovery.InvalidateRead();
        else observed.Clear();
    }
    public void Dispose(){disposed=true;observed.Clear();inner.Dispose();}
}
