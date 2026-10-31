using System.Text;
using System.Text.Json;
using Porto.Core;

namespace Porto.Usb;

public sealed record Command(string Op,string? Device=null,string? Object=null,int Count=0);
public static class WorkerProtocol
{
    public static int Run()
    {
        Com.Check(Com.CoInitializeEx(0,0));
        using var writer=new BinaryWriter(Console.OpenStandardOutput(),Encoding.UTF8,true);
        using var reader=new StreamReader(Console.OpenStandardInput(),Encoding.UTF8);
        WpdSource? source=null;Stream? stream=null;
        var heartbeat=new InventoryProgressThrottle(n=>Frame(1,JsonSerializer.SerializeToUtf8Bytes(n)));
        try
        {
            string? line;
            while((line=reader.ReadLine())!=null)
            {
                try
                {
                    if(line.Length>65536)throw new IOException("Comando troppo lungo.");
                    var command=JsonSerializer.Deserialize<Command>(line)??throw new IOException("Comando vuoto.");
                    switch(command.Op)
                    {
                        case "smoke":WpdSource.SmokeWithoutDevices();Json(true);break;
                        case "devices":Json(WpdSource.Devices());break;
                        case "discover":Json(WpdSource.Discover());break;
                        case "connect":stream?.Dispose();stream=null;source?.Dispose();source=new(command.Device??throw new IOException("Dispositivo assente"));
                            source.InventoryProgress=heartbeat.Report;
                            Json(true);break;
                        case "inventory":stream?.Dispose();stream=null;heartbeat.Reset();Json((source??throw new IOException("Non connesso")).Inventory(default));break;
                        case "open":stream?.Dispose();stream=null;stream=(source??throw new IOException("Non connesso")).OpenRead(new(command.Object!,"","",MediaKind.Video),default);Json(true);break;
                        case "read":
                            if(command.Count<0||command.Count>VerifiedCopy.BufferSize)throw new IOException("Lettura non valida");
                            var buffer=new byte[command.Count];int n=(stream??throw new IOException("Nessun flusso aperto")).Read(buffer,0,buffer.Length);
                            Frame(2,buffer.AsSpan(0,n));break;
                        case "close":stream?.Dispose();stream=null;Json(true);break;
                        default:throw new IOException("Operazione non supportata.");
                    }
                }
                catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
                {Frame(3,Encoding.UTF8.GetBytes(ex.Message));}
            }
            return 0;
        }
        finally{stream?.Dispose();source?.Dispose();Com.CoUninitialize();}
        void Json<T>(T value)=>Frame(0,JsonSerializer.SerializeToUtf8Bytes(value));
        void Frame(byte kind,ReadOnlySpan<byte> data){writer.Write(kind);writer.Write(data.Length);writer.Write(data);writer.Flush();}
    }
}
