using System.Runtime.InteropServices;
using Porto.App;

internal static class FinalPreferenceTests
{
    public static void Add(List<(string,Action)> tests)
    {
        tests.Add(("final preferences normal settings remain optional and readable",()=>
        {
            string path=Path.Combine(Fixtures.Root(),"prefs","destination.txt");
            AppPreferences.SaveFolder(path,@"C:\Video\First");
            Fixtures.Check(AppPreferences.LoadFolder(path)==@"C:\Video\First");
            AppPreferences.SaveFolder(path,@"C:\Video\Second");
            Fixtures.Check(AppPreferences.LoadFolder(path)==@"C:\Video\Second");
        }));
        tests.Add(("final preferences a hard link cannot overwrite an unrelated file",()=>
        {
            var (path,foreign)=LinkedPreference();
            byte[] original=File.ReadAllBytes(foreign);
            AppPreferences.SaveFolder(path,@"C:\Video\NewFolder");
            Fixtures.Check(File.ReadAllBytes(foreign).SequenceEqual(original),"Saving a preference wrote through a hard link into a foreign file.");
        }));
        tests.Add(("final preferences linked settings are ignored on load",()=>
        {
            var (path,_)=LinkedPreference();
            Fixtures.Check(AppPreferences.LoadFolder(path)=="","The app followed a linked preference file.");
        }));
    }
    private static (string Path,string Foreign) LinkedPreference()
    {
        string root=Fixtures.Root(),directory=Path.Combine(root,"prefs");
        Directory.CreateDirectory(directory);
        string foreign=Path.Combine(root,"unrelated.txt"),path=Path.Combine(directory,"destination.txt");
        File.WriteAllText(foreign,@"C:\Video\Unrelated");
        Fixtures.Check(CreateHardLink(path,foreign,0),"Could not create the synthetic NTFS hard link.");
        return(path,foreign);
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string path,string existing,nint reserved);
}
