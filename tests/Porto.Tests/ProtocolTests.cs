using System.Text;
using Porto.Usb;

internal static class ProtocolTests
{
    public static int Child(string mode)
    {
        Console.ReadLine();using var writer=new BinaryWriter(Console.OpenStandardOutput());
        void Frame(byte kind,string text){byte[] bytes=Encoding.UTF8.GetBytes(text);writer.Write(kind);writer.Write(bytes.Length);writer.Write(bytes);writer.Flush();}
        if(mode=="--protocol-heartbeat")
        {for(int i=0;i<12;i++){Frame(1,i.ToString());Thread.Sleep(50);}Frame(0,"[]");return 0;}
        switch(mode)
        {
            case "--protocol-null":Frame(0,"null");break;
            case "--protocol-kind":Frame(2,"[]");break;
            case "--protocol-progress":Frame(1,"bad");Frame(0,"[]");break;
            default:Frame(0,"bad");break;
        }
        Thread.Sleep(5000);return 0;
    }
    public static void Add(List<(string,Action)> tests)
    {
        tests.Add(("responsive long worker operation renews per-frame deadline",()=>{
            int frames=0;using var worker=new WorkerClient(Environment.ProcessPath!,"--protocol-heartbeat"){Timeout=TimeSpan.FromMilliseconds(400),InventoryProgress=_=>frames++};
            Fixtures.Check(worker.Devices(default).Count==0&&frames==12&&!worker.IsDisposed);
        }));
        foreach(string mode in new[]{"null","kind","progress","json"})tests.Add(($"malformed {mode} frame invalidates worker",()=>{
            using var worker=new WorkerClient(Environment.ProcessPath!,"--protocol-"+mode);
            var timer=System.Diagnostics.Stopwatch.StartNew();Fixtures.Throws<IOException>(()=>worker.Devices(default));Fixtures.Check(worker.IsDisposed&&timer.Elapsed<TimeSpan.FromSeconds(3));
        }));
    }
}
