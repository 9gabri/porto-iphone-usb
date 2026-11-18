using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace Porto.Lan;

// Storage primitives from Porto, under LICENSE-Porto.
internal static class Destination
{
    public static void CheckAncestors(string path)
    {
        string fullPath=Path.GetFullPath(path);
        for (string? current=fullPath; current!=null; current=Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)
                throw new IOException("Collegamenti e cartelle reindirizzate non sono supportati: scegli una cartella NTFS diretta.");
        }
        // NTFS hard links are ordinary files, not reparse points. Never append to or
        // let SQLite modify a file that also has another name outside Porto's state.
        if(File.Exists(fullPath))
        {
            using var handle=File.OpenHandle(fullPath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
            if(!GetFileInformationByHandle(handle,out var information))
                throw new IOException("Impossibile verificare i collegamenti del file.",Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
            if(information.NumberOfLinks!=1)
                throw new IOException("Un file nella destinazione ha più collegamenti NTFS: conservato senza modifiche.");
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
        // Both paths were constructed under a fixed local drive and were checked above.
        // Extended absolute paths also work on machines with legacy MAX_PATH settings.
        if (!MoveFileEx(@"\\?\"+Path.GetFullPath(source),@"\\?\"+Path.GetFullPath(target),8))
            throw new IOException("Pubblicazione file non riuscita.",Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool MoveFileEx(string existing,string target,uint flags);
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
}
