using Porto.Core;

namespace Porto.Usb;

// A rejected stream cannot be trusted to leave the native driver ready for another read.
// Replace the process, then validate the entire inventory before using a former observation.
public sealed class ReconnectingUsbSource : IUsbCollectionSource,IReadFailureRecovery
{
    private readonly Func<WorkerClient> createWorker;
    private readonly string deviceId;
    private readonly Action<int>? progress;
    private WorkerClient? worker;
    private bool hasInventory,disposed;
    private bool recoveryRequested;
    private Inventory? baseline;
    private UsbFolderChoice? inventoryFolder;
    private UsbCollectionChoice? inventoryCollection;
    public int SessionRestarts {get;private set;}
    public void InvalidateRead()=>recoveryRequested=true;
    public ReconnectingUsbSource(string executable,string deviceId,Action<int>? progress=null)
        :this(()=>new WorkerClient(executable),deviceId,progress){}
    internal ReconnectingUsbSource(Func<WorkerClient> createWorker,string deviceId,Action<int>? progress=null)
    {this.createWorker=createWorker;this.deviceId=deviceId;this.progress=progress;}
    private WorkerClient Get(CancellationToken cancel)
    {
        ObjectDisposedException.ThrowIf(disposed,this);cancel.ThrowIfCancellationRequested();
        if(worker is { IsDisposed: false })return worker;
        if(worker!=null)throw SessionUnavailable();
        worker=createWorker();worker.InventoryProgress=progress;
        try{worker.Connect(deviceId,cancel);return worker;}
        catch(SourceUnavailableException){worker.Dispose();throw;}
        catch(IOException ex){worker.Dispose();throw new SourceUnavailableException("Collegamento allo stesso dispositivo non disponibile: "+ex.Message,ex);}
        catch{worker.Dispose();throw;}
    }
    public Inventory Inventory(CancellationToken cancel)=>InventoryWithin(null,null,cancel);
    public Inventory Inventory(UsbFolderChoice folder,CancellationToken cancel)
    {ArgumentNullException.ThrowIfNull(folder);return InventoryWithin(folder,null,cancel);}
    public Inventory Inventory(UsbCollectionChoice collection,CancellationToken cancel)
    {ArgumentNullException.ThrowIfNull(collection);return InventoryWithin(null,collection,cancel);}
    public IReadOnlyList<UsbFolderChoice> ListFolders(UsbFolderChoice? parent,CancellationToken cancel)
        =>Browse(current=>current.ListFolders(parent,cancel),cancel);
    public IReadOnlyList<UsbCollectionChoice> ListCollections(UsbFolderChoice? parent,CancellationToken cancel)
        =>Browse(current=>current.ListCollections(parent,cancel),cancel);
    public UsbNavigationLevel ReadLevel(UsbFolderChoice? parent,CancellationToken cancel)
        =>Browse(current=>current.ReadLevel(parent,cancel),cancel);
    private T Browse<T>(Func<WorkerClient,T> list,CancellationToken cancel)
    {
        // Browsing does not authorize reads from an earlier inventory.
        hasInventory=false;baseline=null;
        var current=recoveryRequested?ReplaceWorker(cancel):Get(cancel);
        try{var result=list(current);recoveryRequested=false;return result;}
        catch(IOException ex)when(current.IsDisposed&&ex is not SourceUnavailableException){throw SessionUnavailable(ex);}
    }
    private Inventory InventoryWithin(UsbFolderChoice? folder,UsbCollectionChoice? collection,CancellationToken cancel)
    {
        hasInventory=false;baseline=null;
        var current=recoveryRequested?ReplaceWorker(cancel):Get(cancel);
        try
        {
            var scope=folder==null?null:new UsbFolderChoice(folder.DisplayPath,folder.Segments.ToArray());
            var collectionScope=collection==null?null:new UsbCollectionChoice(collection.DisplayPath,collection.Segments.ToArray());
            var inventory=collectionScope!=null?current.Inventory(collectionScope,cancel):scope==null?current.Inventory(cancel):current.Inventory(scope,cancel);
            baseline=new([..inventory.Items],[..inventory.Errors],inventory.EnumerationComplete);
            inventoryFolder=scope;
            inventoryCollection=collectionScope;
            hasInventory=true;recoveryRequested=false;return inventory;
        }
        catch(SourceUnavailableException){hasInventory=false;throw;}
        catch(IOException ex)when(current.IsDisposed){hasInventory=false;throw SessionUnavailable(ex);}
    }
    public Stream OpenRead(SourceItem item,CancellationToken cancel)
    {
        ObjectDisposedException.ThrowIf(disposed,this);cancel.ThrowIfCancellationRequested();
        if(recoveryRequested)RecoverBeforeRead(item,cancel);
        if(!hasInventory)throw SessionUnavailable();
        var current=Get(cancel);
        try{return current.OpenRead(item,cancel);}
        catch(SourceUnavailableException){hasInventory=false;throw;}
        catch(IOException ex)when(current.IsDisposed){hasInventory=false;throw SessionUnavailable(ex);}
    }
    private WorkerClient ReplaceWorker(CancellationToken cancel)
    {
        ObjectDisposedException.ThrowIf(disposed,this);cancel.ThrowIfCancellationRequested();
        // WorkerClient terminates only its own child; no blocking native Cancel/Close RPC.
        var previous=worker;worker=null;hasInventory=false;
        previous?.Dispose();SessionRestarts++;
        return Get(cancel);
    }
    private void RecoverBeforeRead(SourceItem item,CancellationToken cancel)
    {
        try
        {
            if(baseline==null||!baseline.EnumerationComplete||baseline.Errors.Count!=0||!baseline.Items.Contains(item))
                throw new IOException("Manca un inventario completo per verificare il recupero USB.");
            var current=ReplaceWorker(cancel);
            var fresh=inventoryCollection!=null?current.Inventory(inventoryCollection,cancel):
                inventoryFolder==null?current.Inventory(cancel):current.Inventory(inventoryFolder,cancel);
            if(!fresh.EnumerationComplete||fresh.Errors.Count!=0)
                throw new IOException("Il nuovo inventario USB è incompleto o contiene errori.");
            // Sorted fingerprints include every field AND duplicate multiplicity. This is
            // continuity validation, never evidence that a video's bytes were copied.
            if(!baseline.Items.Select(WorkLedger.Key).Order(StringComparer.Ordinal)
                .SequenceEqual(fresh.Items.Select(WorkLedger.Key).Order(StringComparer.Ordinal)))
                throw new IOException("L'inventario USB è cambiato: gli identificativi precedenti non saranno riutilizzati.");
            baseline=new([..fresh.Items],[..fresh.Errors],true);
            hasInventory=true;recoveryRequested=false;
        }
        catch(Exception error)when(error is IOException or UnauthorizedAccessException)
        {
            hasInventory=false;
            throw new PreviousReadCleanupException("Recupero della precedente lettura USB non concluso. Il video successivo non è stato aperto. "+error.Message,error);
        }
    }
    private static SourceUnavailableException SessionUnavailable(Exception? inner=null)=>new(
        "La sessione USB è terminata. Serve un nuovo inventario dello stesso dispositivo prima di continuare."+(inner==null?"":" "+inner.Message),inner);
    public void Dispose(){disposed=true;worker?.Dispose();}
}
