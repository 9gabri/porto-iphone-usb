using System.Diagnostics;

namespace Porto.Core;

// Aggregate counters only: never paths, names, device identifiers or media contents.
public sealed class ImportPerformance
{
    public double TotalMilliseconds {get;set;}
    public double InventoryMilliseconds {get;set;}
    public double InitialInventoryMilliseconds {get;set;}
    public double FinalInventoryMilliseconds {get;set;}
    public double MediaOpenMilliseconds {get;set;}
    public double MediaReadMilliseconds {get;set;}
    public double LocalVerificationMilliseconds {get;set;}
    public double ReportMilliseconds {get;set;}
    public long MediaSourceBytes {get;set;}
    public long MediaReadCalls {get;set;}
    public long MediaOpenAttempts {get;set;}
    public long LocalReplayBytes {get;set;}
    public double ProcessCpuMilliseconds {get;set;}
    public long ProcessPeakWorkingSetBytes {get;set;}
    public SourceTransportMetrics? AppleTransport {get;set;}
    public string TimingScope {get;}="Per passaggio, dall'ingresso Run fino alla scrittura del report principale; scelta album precedente, chiusura finale e scrittura di questo file esclusi. Tempi di lettura e verifica inclusi nel totale, non sommabili. CPU/memoria riguardano il processo. Byte AFC sono payload, non traffico USB fisico. Il riepilogo nel report principale precede le misure finali di questo file.";
}

public interface ISourceTransportDiagnostics { SourceTransportMetrics TransportMetrics {get;} }
public sealed class SourceTransportMetrics
{
    public long ConnectionAttempts {get;set;}
    public long Commands {get;set;}
    public long ReadRequests {get;set;}
    public long OpenRequests {get;set;}
    public long StatRequests {get;set;}
    public long FailedExchanges {get;set;}
    public long CatalogBytes {get;set;}
    public long MediaBytes {get;set;}
    public double CommandMilliseconds {get;set;}
    public int ReadBlockBytes {get;set;}
}

internal sealed class MeasuredSourceStream(Stream inner,ImportPerformance metrics):Stream,ISourceStreamInfo
{
    public long? ResourceLength=>(inner as ISourceStreamInfo)?.ResourceLength;
    public override int Read(byte[] buffer,int offset,int count)
    {
        long started=Stopwatch.GetTimestamp();metrics.MediaReadCalls++;
        try{int read=inner.Read(buffer,offset,count);metrics.MediaSourceBytes+=read;return read;}
        finally{metrics.MediaReadMilliseconds+=Stopwatch.GetElapsedTime(started).TotalMilliseconds;}
    }
    protected override void Dispose(bool disposing){if(disposing)inner.Dispose();base.Dispose(disposing);}
    public override bool CanRead=>inner.CanRead;public override bool CanWrite=>false;public override bool CanSeek=>false;
    public override long Length=>inner.Length;public override long Position{get=>inner.Position;set=>throw new NotSupportedException();}
    public override void Flush()=>inner.Flush();public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
    public override void SetLength(long value)=>throw new NotSupportedException();public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
}
