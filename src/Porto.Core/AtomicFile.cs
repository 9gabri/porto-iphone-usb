namespace Porto.Core;

public static class AtomicFile
{
    // A failed write may leave an explicitly temporary file, never a partial final
    // report. Keep that temporary for diagnosis; no existing file is replaced.
    public static void WriteNew(string path,Action<Stream> write)
    {
        string target=Path.GetFullPath(path);
        Destination.CheckAncestors(target);
        string directory=Path.GetDirectoryName(target)!;
        string temporary=Path.Combine(directory,".porto-writing-"+Guid.NewGuid().ToString("N")+".tmp");
        Destination.CheckAncestors(temporary);
        using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
        {
            write(output);
            output.Flush(true);
        }
        Destination.Publish(temporary,target);
    }
}
