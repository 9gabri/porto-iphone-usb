using System.Runtime.InteropServices;
using Porto.Core;

namespace Porto.App;

internal static class AppPreferences
{
    private static string PathName=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Porto","destination.txt");
    public static string LoadFolder()=>LoadFolder(PathName);
    internal static string LoadFolder(string path)
    {
        try
        {
            Destination.CheckAncestors(path);
            if(!File.Exists(path)||new FileInfo(path).Length>4096)return "";
            string folder=File.ReadAllText(path).Trim();
            return folder.Length<=120&&!folder.Contains('\0')&&Path.IsPathFullyQualified(folder)?folder:"";
        }
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or ArgumentException){return "";}
    }
    public static void SaveFolder(string folder)=>SaveFolder(PathName,folder);
    internal static void SaveFolder(string path,string folder)
    {
        try
        {
            Destination.CheckAncestors(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            Destination.CheckAncestors(path);
            File.WriteAllText(path,folder);
        }
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){/* Optional preference; copy is unaffected. */}
    }
}

// Only prevents idle system sleep while this worker thread runs; display locking remains unchanged.
internal sealed class AwakeScope:IDisposable
{
    public AwakeScope()=>SetThreadExecutionState(0x80000001);
    public void Dispose()=>SetThreadExecutionState(0x80000000);
    [DllImport("kernel32.dll")][DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint SetThreadExecutionState(uint flags);
}
