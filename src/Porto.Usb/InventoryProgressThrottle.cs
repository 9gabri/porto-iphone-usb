namespace Porto.Usb;

internal sealed class InventoryProgressThrottle(Action<int> report,Func<long>? milliseconds=null)
{
    private readonly Func<long> clock=milliseconds??(()=>Environment.TickCount64);
    private long? lastHeartbeat;
    public void Reset()=>lastHeartbeat=null;
    public void Report(int count)
    {
        long now=clock();
        if(lastHeartbeat.HasValue&&now-lastHeartbeat.Value<250)return;
        lastHeartbeat=now;report(count);
    }
}
