namespace Porto.Core;

// A disposable replay of the first source read, never a durable or completed copy.
// DeleteOnClose applies only to this CreateNew handle, including after process death.
internal sealed class ComparisonBuffer(Destination destination,IDiskSpace space) : IDisposable
{
    private FileStream? stream;
    private bool complete;
    public Stream? Prepare(long? expected)
    {
        if(expected is not >0 || expected>(long.MaxValue-VerifiedCopy.ReserveBytes)/2)return null;
        // Retain room for BOTH replay and final copy. Low-space imports use the old hash-only path.
        if(space.Available(destination.Root)<expected.Value*2+VerifiedCopy.ReserveBytes)return null;
        string path=destination.Resolve(Path.Combine(".porto",$"comparison-{Guid.NewGuid():N}.tmp"));
        stream=new FileStream(path,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,
            VerifiedCopy.BufferSize,FileOptions.SequentialScan|FileOptions.DeleteOnClose);
        return stream;
    }
    public void Complete(long length)
    {
        if(stream==null)return;
        if(stream.Length!=length)throw new IOException("Preparazione locale incompleta; copia non pubblicata.");
        stream.Flush();stream.Position=0;complete=true;
    }
    public Stream? Replay()=>complete?stream:null;
    public void Dispose()=>stream?.Dispose();
}
