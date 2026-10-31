using Porto.Core;

namespace Porto.Usb;

// ImportSession retries with a new source and inventory. Object IDs never cross WPD connections.
public sealed class ReconnectingUsbSource : IVideoSource
{
    private readonly Func<WorkerClient> createWorker;
    private readonly string deviceId;
    private readonly Action<int>? progress;
    private WorkerClient? worker;
    private bool hasInventory,disposed;
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
        catch(IOException ex){worker.Dispose();throw new SourceUnavailableException("Collegamento allo stesso iPhone non disponibile: "+ex.Message,ex);}
        catch{worker.Dispose();throw;}
    }
    public Inventory Inventory(CancellationToken cancel)
    {
        var current=Get(cancel);
        try{var inventory=current.Inventory(cancel);hasInventory=true;return inventory;}
        catch(SourceUnavailableException){hasInventory=false;throw;}
        catch(IOException ex)when(current.IsDisposed){hasInventory=false;throw SessionUnavailable(ex);}
    }
    public Stream OpenRead(SourceItem item,CancellationToken cancel)
    {
        ObjectDisposedException.ThrowIf(disposed,this);cancel.ThrowIfCancellationRequested();
        if(!hasInventory)throw SessionUnavailable();
        var current=Get(cancel);
        try{return current.OpenRead(item,cancel);}
        catch(SourceUnavailableException){hasInventory=false;throw;}
        catch(IOException ex)when(current.IsDisposed){hasInventory=false;throw SessionUnavailable(ex);}
    }
    private static SourceUnavailableException SessionUnavailable(Exception? inner=null)=>new(
        "La sessione USB è terminata. Serve un nuovo inventario dello stesso iPhone prima di continuare."+(inner==null?"":" "+inner.Message),inner);
    public void Dispose(){disposed=true;worker?.Dispose();}
}
