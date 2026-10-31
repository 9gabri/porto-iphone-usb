using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Porto.Core;

namespace Porto.Usb;

// Frame 3 rejects one operation; frame 4 invalidates the connection and its object IDs.
internal sealed class RemoteOperationException(string message):IOException(message);

public sealed class WorkerClient : IVideoSource
{
    private readonly Process process;
    private readonly Stream output;
    private readonly StreamWriter input;
    private Task? pendingWrite;
    private bool disposed;
    public bool IsDisposed=>disposed;
    public TimeSpan Timeout {get;set;}=TimeSpan.FromSeconds(60);
    public Action<int>? InventoryProgress {get;set;}
    public WorkerClient(string executable,string mode="--usb-worker")
    {
        var info=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true};
        info.ArgumentList.Add(mode);
        try{process=Process.Start(info)??throw new IOException("Impossibile avviare il collegamento USB.");}
        catch(System.ComponentModel.Win32Exception ex){throw new IOException("Windows non ha avviato il componente USB di Porto. Verifica che la cartella estratta sia completa.",ex);}
        output=process.StandardOutput.BaseStream;input=process.StandardInput;input.AutoFlush=true;
    }
    public List<DeviceInfo> Devices(CancellationToken cancel)=>Json<List<DeviceInfo>>(new("devices"),cancel);
    public DiscoveryResult Discover(CancellationToken cancel)=>Json<DiscoveryResult>(new("discover"),cancel);
    public void ValidateTransportWithoutDevices()=>Json<bool>(new("smoke"),default);
    public void Connect(string id,CancellationToken cancel)=>Json<bool>(new("connect",Device:id),cancel);
    public Inventory Inventory(CancellationToken cancel)=>Json<Inventory>(new("inventory"),cancel);
    public Stream OpenRead(SourceItem item,CancellationToken cancel)
    {Json<bool>(new("open",Object:item.Id),cancel);return new RemoteStream(this,cancel);}
    private T Json<T>(Command command,CancellationToken cancel)
    {
        var response=Exchange(command,cancel);
        if(response.Kind!=0)throw ProtocolError("Risposta USB inattesa.");
        try{return JsonSerializer.Deserialize<T>(response.Data)??throw ProtocolError("Risposta USB vuota.");}
        catch(JsonException ex){Dispose();throw new SourceUnavailableException("Risposta USB non leggibile. Serve un nuovo inventario.",ex);}
    }
    private SourceUnavailableException ProtocolError(string message){Dispose();return new SourceUnavailableException(message);}
    private (byte Kind,byte[] Data) Exchange(Command command,CancellationToken cancel)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        try
        {
            cancel.ThrowIfCancellationRequested();
            pendingWrite=input.WriteLineAsync(JsonSerializer.Serialize(command).AsMemory(),cancel);
            pendingWrite.WaitAsync(Timeout,cancel).GetAwaiter().GetResult();
            pendingWrite=null;
            while(true)
            {
                byte[] header=new byte[5];Read(header,cancel);
                int size=BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(1));
                int limit=header[0]==2?VerifiedCopy.BufferSize:header[0]==1?64:256*1024*1024;
                if(size<0||size>limit)throw new IOException("Risposta USB oltre i limiti.");
                byte[] data=new byte[size];Read(data,cancel);
                if(header[0]==1){int progress=JsonSerializer.Deserialize<int>(data);InventoryProgress?.Invoke(progress);continue;}
                if(header[0]==3)throw new RemoteOperationException(Encoding.UTF8.GetString(data));
                if(header[0]==4)
                {
                    var failure=JsonSerializer.Deserialize<WorkerSessionError>(data);
                    if(failure==null||string.IsNullOrWhiteSpace(failure.Message))throw ProtocolError("Errore di sessione USB non leggibile.");
                    throw failure.ToException();
                }
                return(header[0],data);
            }
        }
        catch(RemoteOperationException){throw;}
        catch(SourceUnavailableException){Dispose();throw;}
        catch(OperationCanceledException){Dispose();throw;}
        catch(TimeoutException ex){Dispose();throw new SourceUnavailableException($"Il telefono non risponde da {Timeout.TotalSeconds:0} secondi. Sbloccalo o ricollegalo; Porto tenterà il recupero con un nuovo inventario.",ex);}
        catch(JsonException ex){Dispose();throw new SourceUnavailableException("Risposta USB non valida. Serve un nuovo inventario.",ex);}
        catch(Exception ex) when(ex is IOException or InvalidDataException)
        {Dispose();throw new SourceUnavailableException("La comunicazione USB è stata interrotta. Serve un nuovo inventario. "+ex.Message,ex);}
    }
    private void Read(byte[] data,CancellationToken cancel)
    {
        // Per-frame deadline, renewed by inventory batches, not a deadline for the whole 250 GB transfer.
        output.ReadExactlyAsync(data,cancel).AsTask().WaitAsync(Timeout,cancel).GetAwaiter().GetResult();
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;
        try{if(!process.HasExited)process.Kill(entireProcessTree:false);}catch(InvalidOperationException){}
        finally
        {
            // A timed-out write may still own the writer. Do not flush or wait for it on the caller.
            // Killing the child releases the pipe; observe the write before disposing its wrappers.
            if(pendingWrite is { } write)_=Task.Run(async()=>
            {
                try{await write.ConfigureAwait(false);}catch(Exception ex)when(ex is IOException or OperationCanceledException or ObjectDisposedException){}
                finally{DisposeHandles();}
            });
            else DisposeHandles();
        }
    }
    private void DisposeHandles()
    {
        try{input.Dispose();}catch(IOException){}
        finally{try{output.Dispose();}catch(IOException){}finally{process.Dispose();}}
    }
    private sealed class RemoteStream(WorkerClient client,CancellationToken cancel):Stream
    {
        private bool disposed;
        public override int Read(byte[] buffer,int offset,int count)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            if(offset<0||count<0||offset>buffer.Length-count)throw new ArgumentOutOfRangeException(nameof(count));
            if(count==0)return 0;
            var response=client.Exchange(new("read",Count:Math.Min(count,VerifiedCopy.BufferSize)),cancel);
            if(response.Kind!=2||response.Data.Length>count)throw client.ProtocolError("Risposta flusso USB non valida.");
            response.Data.CopyTo(buffer,offset);return response.Data.Length;
        }
        protected override void Dispose(bool disposing)
        {
            // The worker releases its one stream before the next open/inventory, or when it exits.
            // Cleanup must not issue an RPC that replaces an exception already unwinding this scope.
            disposed=true;
            base.Dispose(disposing);
        }
        public override bool CanRead=>!disposed;public override bool CanSeek=>false;public override bool CanWrite=>false;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush(){}public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();
        public override void SetLength(long v)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
    }
}
