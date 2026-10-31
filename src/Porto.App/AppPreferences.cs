using System.Runtime.InteropServices;

namespace Porto.App;

internal static class AppPreferences
{
    private static string PathName=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Porto","destination.txt");
    public static string LoadFolder()
    {
        try
        {
            if(!File.Exists(PathName)||new FileInfo(PathName).Length>4096)return "";
            string folder=File.ReadAllText(PathName).Trim();
            return folder.Length<=120&&!folder.Contains('\0')&&Path.IsPathFullyQualified(folder)?folder:"";
        }
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or ArgumentException){return "";}
    }
    public static void SaveFolder(string folder)
    {
        try{Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);File.WriteAllText(PathName,folder);}
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
