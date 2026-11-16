using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Porto.Core;

// The folder is a user selection, not a PhotoKit inventory or proof of originality.
// File paths and metadata authorize a read; only the engine's full hashes prove bytes.
public sealed class FolderVideoSource : ISelectedVideoSource
{
    private readonly string sourceRoot,destinationRoot;
    private Dictionary<string,(SourceItem Item,string Path)> observed=new(StringComparer.Ordinal);
    private bool disposed;
    private const FileAttributes UnsupportedAttributes=FileAttributes.ReparsePoint|FileAttributes.Offline|
        (FileAttributes)0x40000|(FileAttributes)0x400000; // Recall-on-open / recall-on-data-access.

    public string SelectionDescription =>
        "Cartella selezionata dall'utente con video esportati da Foto come originali non modificati. " +
        "La selezione e l'originalità dipendono dall'esportazione; Porto verifica i byte dei file, non la libreria Foto.";

    public FolderVideoSource(string sourceRoot,string destinationRoot)
    {
        // Validate both roots before the caller constructs Destination (which creates state).
        string source=LocalPath(sourceRoot),destination=LocalPath(destinationRoot);
        RequireDirectory(source);
        Destination.CheckAncestors(source);Destination.CheckAncestors(destination);
        this.sourceRoot=CanonicalPath(source);this.destinationRoot=CanonicalPath(destination);
        if(IsWithin(this.sourceRoot,this.destinationRoot)||IsWithin(this.destinationRoot,this.sourceRoot))
            throw new IOException("La cartella sorgente e la destinazione devono essere separate: nessuna può contenere l'altra.");
    }

    public Inventory Inventory(CancellationToken cancel)
    {
        ObjectDisposedException.ThrowIf(disposed,this);cancel.ThrowIfCancellationRequested();
        observed.Clear();
        var items=new List<SourceItem>();var errors=new List<string>();
        var next=new Dictionary<string,(SourceItem Item,string Path)>(StringComparer.Ordinal);
        var directories=new Stack<string>();directories.Push(sourceRoot);
        while(directories.TryPop(out string? directory))
        {
            cancel.ThrowIfCancellationRequested();
            try
            {
                ValidateRoots();RequireDirectory(directory);Destination.CheckAncestors(directory);
                foreach(string path in Directory.GetFileSystemEntries(directory).Order(StringComparer.OrdinalIgnoreCase))
                {
                    cancel.ThrowIfCancellationRequested();
                    try
                    {
                        EnsureInside(path);FileAttributes attributes=File.GetAttributes(path);RequireDirect(attributes);
                        if((attributes&FileAttributes.Directory)!=0){directories.Push(path);continue;}
                        using var stream=OpenChecked(path);
                        SourceItem item=Describe(path,Information(stream.SafeFileHandle));
                        if(!next.TryAdd(item.Id,(item,path)))throw new IOException("Due percorsi differiscono solo per maiuscole/minuscole: selezione non supportata.");
                        items.Add(item);
                    }
                    catch(Exception ex) when(IsReadError(ex)) { errors.Add($"Impossibile inventariare '{path}': {ex.Message}"); }
                }
            }
            catch(Exception ex) when(IsReadError(ex)) { errors.Add($"Impossibile elencare '{directory}': {ex.Message}"); }
        }
        cancel.ThrowIfCancellationRequested();observed=next;
        return new(items,errors,errors.Count==0);
    }

    public Stream OpenRead(SourceItem item,CancellationToken cancel)
    {
        ObjectDisposedException.ThrowIf(disposed,this);cancel.ThrowIfCancellationRequested();
        if(!observed.TryGetValue(item.Id,out var entry)||entry.Item!=item||!MediaInspector.Candidate(item))
            throw new IOException("Il file non corrisponde a un video dell'inventario corrente della cartella.");
        FileStream stream;
        try{ValidateRoots();stream=OpenChecked(entry.Path);}
        catch(UnauthorizedAccessException ex)
        {throw new IOException("Permesso di lettura mancante per questo video della cartella; file conservato.",ex);}
        try
        {
            if(Describe(entry.Path,Information(stream.SafeFileHandle))!=item)
                throw new IOException("Il file sorgente è cambiato dopo l'inventario: ripeti il passaggio.");
            cancel.ThrowIfCancellationRequested();return stream;
        }
        catch {stream.Dispose();throw;}
    }

    private FileStream OpenChecked(string path)
    {
        EnsureInside(path);RequireDirect(File.GetAttributes(path));Destination.CheckAncestors(path);
        var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,1024*1024,FileOptions.SequentialScan);
        try
        {
            FileInformation information=Information(stream.SafeFileHandle);RequireDirect((FileAttributes)information.Attributes);
            if(information.NumberOfLinks!=1)throw new IOException("I file con più collegamenti NTFS non sono supportati.");
            if(!ResolvedPath(stream.SafeFileHandle).Equals(path,StringComparison.OrdinalIgnoreCase))
                throw new IOException("Il percorso del file è stato reindirizzato: lettura rifiutata.");
            Destination.CheckAncestors(path);
            return stream;
        }
        catch {stream.Dispose();throw;}
    }

    private SourceItem Describe(string path,FileInformation information)
    {
        string relative=Path.GetRelativePath(sourceRoot,path).Replace('\\','/');
        string parent=Path.GetDirectoryName(relative.Replace('/',Path.DirectorySeparatorChar))??"";
        string id=Identity(relative),name=Path.GetFileName(path);
        long length=checked((long)(((ulong)information.FileSizeHigh<<32)|information.FileSizeLow));
        string version=FormattableString.Invariant($"{information.VolumeSerialNumber:X8}:{information.FileIndexHigh:X8}{information.FileIndexLow:X8}:{length}:{information.LastWriteTime.dwHighDateTime:X8}{information.LastWriteTime.dwLowDateTime:X8}");
        var item=new SourceItem(id,Identity(parent),name,MediaKind.Other,length,version);
        if(MediaInspector.Candidate(item))return item with{Kind=MediaKind.Video};
        // Common video containers outside the supported MOV/MP4/M4V parser must
        // remain unresolved, never be silently counted as non-video exclusions.
        return Path.GetExtension(name).ToLowerInvariant() is ".avi" or ".mkv" or ".webm" or ".3gp" or ".3g2" or ".mts" or ".m2ts" or ".ts" or ".wmv" or ".mpg" or ".mpeg"
            ?item with{Kind=MediaKind.Unknown}:item;
    }

    private string Identity(string relative)=>"folder:"+Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(sourceRoot.ToUpperInvariant()+"\n"+relative.Replace('\\','/').ToUpperInvariant())));
    private void EnsureInside(string path)
    {
        if(!IsWithin(Path.GetFullPath(path),sourceRoot)||path.Equals(sourceRoot,StringComparison.OrdinalIgnoreCase))
            throw new IOException("Percorso fuori dalla cartella sorgente.");
    }
    private void ValidateRoots()
    {
        RequireDirectory(sourceRoot);Destination.CheckAncestors(sourceRoot);Destination.CheckAncestors(destinationRoot);
        if(!CanonicalPath(sourceRoot).Equals(sourceRoot,StringComparison.OrdinalIgnoreCase)||
           !CanonicalPath(destinationRoot).Equals(destinationRoot,StringComparison.OrdinalIgnoreCase))
            throw new IOException("Una cartella è stata reindirizzata: ripeti la selezione.");
    }
    private static bool IsWithin(string path,string root)=>path.Equals(root,StringComparison.OrdinalIgnoreCase)||
        path.StartsWith(Path.TrimEndingDirectorySeparator(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
    private static bool IsReadError(Exception ex)=>ex is IOException or UnauthorizedAccessException or System.Security.SecurityException;
    private static void RequireDirect(FileAttributes attributes)
    {
        if((attributes&UnsupportedAttributes)!=0)
            throw new IOException("Collegamenti, cartelle reindirizzate e file cloud non disponibili localmente non sono supportati.");
    }
    private static void RequireDirectory(string path)
    {
        FileAttributes attributes=File.GetAttributes(path);RequireDirect(attributes);
        if((attributes&FileAttributes.Directory)==0)throw new IOException("La sorgente deve essere una cartella locale esistente.");
    }
    private static string LocalPath(string path)
    {
        if(string.IsNullOrWhiteSpace(path)||!Path.IsPathFullyQualified(path)||path.StartsWith(@"\\",StringComparison.Ordinal))
            throw new IOException("Scegli una cartella locale con un percorso completo, senza rete o percorsi dispositivo.");
        string full=Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if(full.AsSpan(2).Contains(':'))throw new IOException("I flussi alternativi NTFS non sono percorsi di cartella supportati.");
        var drive=new DriveInfo(Path.GetPathRoot(full)!);
        if(!drive.IsReady||drive.DriveType==DriveType.Network||drive.DriveFormat!="NTFS")
            throw new IOException("La sorgente e la destinazione devono essere cartelle locali su NTFS.");
        return full;
    }
    private static string CanonicalPath(string path)
    {
        var missing=new Stack<string>();string current=path;
        while(!Directory.Exists(current)&&!File.Exists(current))
        {
            missing.Push(Path.GetFileName(current));
            current=Path.GetDirectoryName(current)??throw new IOException("Impossibile risolvere la cartella locale.");
        }
        using var handle=CreateFile(current,0x80,7,0,3,0x02200000,0); // Read attributes, open existing, directory/reparse-safe handle.
        if(handle.IsInvalid)throw NativeError("Impossibile verificare il percorso della cartella.");
        RequireDirect((FileAttributes)Information(handle).Attributes);
        string result=ResolvedPath(handle);
        while(missing.TryPop(out string? component))result=Path.Combine(result,component);
        return Path.TrimEndingDirectorySeparator(result);
    }
    private static string ResolvedPath(SafeFileHandle handle)
    {
        var buffer=new StringBuilder(32768);uint length=GetFinalPathNameByHandle(handle,buffer,(uint)buffer.Capacity,0);
        if(length==0||length>=buffer.Capacity)throw NativeError("Impossibile verificare il percorso effettivo del file.");
        string path=buffer.ToString();
        if(!path.StartsWith(@"\\?\",StringComparison.Ordinal)||path.StartsWith(@"\\?\UNC\",StringComparison.OrdinalIgnoreCase))
            throw new IOException("Il percorso effettivo non è una cartella locale supportata.");
        return Path.TrimEndingDirectorySeparator(path[4..]);
    }
    private static FileInformation Information(SafeFileHandle handle)
    {
        if(!GetFileInformationByHandle(handle,out FileInformation information))throw NativeError("Impossibile verificare i metadati del file.");
        return information;
    }
    private static IOException NativeError(string message)=>new(message,Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
    public void Dispose(){disposed=true;observed.Clear();}

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime,LastAccessTime,LastWriteTime;
        public uint VolumeSerialNumber,FileSizeHigh,FileSizeLow,NumberOfLinks,FileIndexHigh,FileIndexLow;
    }
    [DllImport("kernel32.dll",SetLastError=true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file,out FileInformation information);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle file,StringBuilder path,uint capacity,uint flags);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern SafeFileHandle CreateFile(string path,uint access,uint sharing,nint security,uint disposition,uint flags,nint template);
}
