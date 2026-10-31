using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Porto.Core;

public sealed class Destination : IDisposable
{
    public string Root { get; }
    public string State { get; }
    public string Videos { get; }
    private readonly FileStream gate;
    public Destination(string path)
    {
        Root = Path.GetFullPath(path);
        if (Root.StartsWith(@"\\", StringComparison.Ordinal) || Root.Length > 120)
            throw new IOException("Scegli una cartella locale con percorso breve (massimo 120 caratteri).");
        var drive = new DriveInfo(Path.GetPathRoot(Root)!);
        if (!drive.IsReady || drive.DriveType == DriveType.Network || drive.DriveFormat != "NTFS")
            throw new IOException("Questa candidata richiede una cartella locale su NTFS. Rete ed exFAT non sono supportati.");
        CheckAncestors(Root);
        Directory.CreateDirectory(Root);
        State = Path.Combine(Root, ".porto"); Videos = Path.Combine(Root, "Video Porto");
        CheckAncestors(State); CheckAncestors(Videos);
        Directory.CreateDirectory(State); Directory.CreateDirectory(Videos);
        CheckAncestors(Path.Combine(State,"import.lock"));
        gate = new FileStream(Path.Combine(State,"import.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        try
        {
            foreach (string database in new[]{"catalog.db","work.db"})
                foreach(string suffix in new[]{"","-journal","-wal","-shm"})CheckAncestors(Path.Combine(State,database+suffix));
        }
        catch { gate.Dispose(); throw; }
    }
    public string Resolve(string relative)
    {
        if (Path.IsPathRooted(relative)) throw new IOException("Percorso catalogo non valido.");
        string result = Path.GetFullPath(Path.Combine(Root,relative));
        if (!result.StartsWith(Root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
            throw new IOException("Percorso fuori dalla destinazione.");
        CheckAncestors(result); return result;
    }
    public static void CheckAncestors(string path)
    {
        for (string? current=Path.GetFullPath(path); current!=null; current=Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)
                throw new IOException("Collegamenti e cartelle reindirizzate non sono supportati: scegli una cartella NTFS diretta.");
        }
    }
    public static string SafeStem(string name)
    {
        // Never use a device-supplied filename as a relative path (including ADS and reserved names).
        string stem=Regex.Replace(Path.GetFileNameWithoutExtension(name.Replace('\\','/')), @"[^\p{L}\p{N}_-]", "_");
        if (string.IsNullOrWhiteSpace(stem)) stem="Video";
        return "V_"+stem[..Math.Min(stem.Length,65)];
    }
    public static void Publish(string source,string target)
    {
        CheckAncestors(source); CheckAncestors(target);
        // No REPLACE_EXISTING. WRITE_THROUGH asks Windows to complete the move before returning.
        if (!MoveFileEx(source,target,8)) throw new IOException("Pubblicazione file non riuscita; nessun file esistente è stato sostituito.",Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool MoveFileEx(string existing,string target,uint flags);
    public void Dispose() => gate.Dispose();
}
