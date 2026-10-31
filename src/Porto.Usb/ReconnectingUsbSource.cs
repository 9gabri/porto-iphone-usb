using Porto.Core;

namespace Porto.Usb;

// Reconnect only the selected device ID. Never switch to another attached phone.
public sealed class ReconnectingUsbSource(string executable, string deviceId, Action<int>? progress = null) : IVideoSource
{
    private WorkerClient? worker;
    private WorkerClient Get(CancellationToken cancel)
    {
        if(worker is { IsDisposed: false })return worker;
        worker?.Dispose();worker=new WorkerClient(executable){InventoryProgress=progress};
        try{worker.Connect(deviceId,cancel);return worker;}
        catch{worker.Dispose();throw;}
    }
    public Inventory Inventory(CancellationToken cancel)=>Get(cancel).Inventory(cancel);
    public Stream OpenRead(SourceItem item,CancellationToken cancel)=>Get(cancel).OpenRead(item,cancel);
    public void Dispose()=>worker?.Dispose();
}
