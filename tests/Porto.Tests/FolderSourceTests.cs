using System.Diagnostics;
using System.Runtime.InteropServices;
using Porto.Core;
using static Fixtures;

internal static class FolderSourceTests
{
    public static void Add(List<(string,Action)> tests)
    {
        tests.Add(("folder source inventories nested videos and transparently excludes other files",()=>
        {
            var (sourcePath,destinationPath)=Paths();
            string nested=Path.Combine(sourcePath,"nested");Directory.CreateDirectory(nested);
            File.WriteAllBytes(Path.Combine(sourcePath,"one.MOV"),Video());
            File.WriteAllBytes(Path.Combine(nested,"two.mp4"),Video(2));
            File.WriteAllBytes(Path.Combine(nested,"three.M4V"),Video(3));
            File.WriteAllBytes(Path.Combine(sourcePath,"one.HEIC"),[1,2]);
            File.WriteAllText(Path.Combine(sourcePath,"one.AAE"),"synthetic adjustment");
            File.WriteAllText(Path.Combine(sourcePath,"notes.txt"),"synthetic note");
            using var source=new FolderVideoSource(sourcePath,destinationPath);
            var inventory=source.Inventory(default);
            Fixtures.Check(inventory.EnumerationComplete&&inventory.Errors.Count==0&&inventory.Items.Count==6);
            Fixtures.Check(inventory.Items.Count(i=>i.Kind==MediaKind.Video)==3&&inventory.Items.Count(MediaInspector.Candidate)==3);
            Fixtures.Check(inventory.Items.Count(i=>i.Kind==MediaKind.Image)==1&&inventory.Items.Count(i=>i.Kind==MediaKind.Other)==2);
            Fixtures.Check(inventory.Items.Select(i=>i.Id).Distinct().Count()==6);
            Fixtures.Throws<IOException>(()=>source.OpenRead(inventory.Items.Single(i=>i.Name=="notes.txt"),default));
            Fixtures.Check(!Directory.Exists(destinationPath),"Source construction and inventory must never create the destination.");
            Fixtures.Check(source.SelectionDescription.Contains("selezion",StringComparison.OrdinalIgnoreCase));
        }));
        tests.Add(("folder source reads original bytes without modifying source and denies writing",()=>
        {
            var (sourcePath,destinationPath)=Paths();byte[] bytes=Video();
            string path=Path.Combine(sourcePath,"one.mov");File.WriteAllBytes(path,bytes);
            DateTime modified=File.GetLastWriteTimeUtc(path);
            using var source=new FolderVideoSource(sourcePath,destinationPath);
            var item=source.Inventory(default).Items.Single();
            using(var stream=source.OpenRead(item,default))
            {
                Fixtures.Check(stream.CanRead&&!stream.CanWrite);
                using var copy=new MemoryStream();stream.CopyTo(copy);Fixtures.Check(copy.ToArray().SequenceEqual(bytes));
                Fixtures.Throws<IOException>(()=>{using var writer=new FileStream(path,FileMode.Open,FileAccess.Write,FileShare.ReadWrite);});
                Fixtures.Throws<IOException>(()=>File.Delete(path));
            }
            Fixtures.Check(File.ReadAllBytes(path).SequenceEqual(bytes)&&File.GetLastWriteTimeUtc(path)==modified);
        }));
        tests.Add(("folder source IDs survive enumeration and normalized root spelling",()=>
        {
            var (sourcePath,destinationPath)=Paths();
            File.WriteAllBytes(Path.Combine(sourcePath,"one.mov"),Video());
            using var first=new FolderVideoSource(sourcePath,destinationPath);
            using var second=new FolderVideoSource(Path.Combine(sourcePath,"."),destinationPath);
            var initial=first.Inventory(default).Items.Single();
            Fixtures.Check(initial==first.Inventory(default).Items.Single()&&initial==second.Inventory(default).Items.Single());
        }));
        tests.Add(("folder source final inventory detects changed sizes and modification timestamps",()=>
        {
            var (sourcePath,destinationPath)=Paths();string path=Path.Combine(sourcePath,"one.mov");
            File.WriteAllBytes(path,Video());
            using var source=new FolderVideoSource(sourcePath,destinationPath);
            var initial=source.Inventory(default).Items.Single();
            File.SetLastWriteTimeUtc(path,File.GetLastWriteTimeUtc(path).AddMinutes(1));
            var modified=source.Inventory(default).Items.Single();
            Fixtures.Check(initial.Id==modified.Id&&initial.PersistentId!=modified.PersistentId&&WorkLedger.Key(initial)!=WorkLedger.Key(modified));
            using(var append=new FileStream(path,FileMode.Append,FileAccess.Write))append.WriteByte(1);
            var enlarged=source.Inventory(default).Items.Single();
            Fixtures.Check(enlarged.Id==initial.Id&&enlarged.Size==initial.Size+1&&enlarged.PersistentId!=modified.PersistentId);
        }));
        tests.Add(("folder source refuses unobserved forged and stale source items",()=>
        {
            var (sourcePath,destinationPath)=Paths();string path=Path.Combine(sourcePath,"one.mov");File.WriteAllBytes(path,Video());
            using var source=new FolderVideoSource(sourcePath,destinationPath);
            Fixtures.Throws<IOException>(()=>source.OpenRead(new("../outside.mov","","one.mov",MediaKind.Video),default));
            var item=source.Inventory(default).Items.Single();
            foreach(var forged in new[]{item with{Name="../outside.mov"},item with{Size=item.Size+1},item with{Id="../outside.mov"}})
                Fixtures.Throws<IOException>(()=>source.OpenRead(forged,default));
            File.SetLastWriteTimeUtc(path,File.GetLastWriteTimeUtc(path).AddMinutes(1));
            Fixtures.Throws<IOException>(()=>source.OpenRead(item,default));
        }));
        tests.Add(("folder source detects replacement even with identical length and timestamp",()=>
        {
            var (sourcePath,destinationPath)=Paths();string path=Path.Combine(sourcePath,"one.mov");
            byte[] bytes=Video();File.WriteAllBytes(path,bytes);DateTime modified=File.GetLastWriteTimeUtc(path);
            using var source=new FolderVideoSource(sourcePath,destinationPath);
            var original=source.Inventory(default).Items.Single();
            File.Move(path,Path.Combine(sourcePath,"preserved.bin"));
            File.WriteAllBytes(path,bytes);File.SetLastWriteTimeUtc(path,modified);
            Fixtures.Throws<IOException>(()=>source.OpenRead(original,default));
            var replacement=source.Inventory(default).Items.Single(i=>i.Name=="one.mov");
            Fixtures.Check(original.Id==replacement.Id&&original.Size==replacement.Size&&original.PersistentId!=replacement.PersistentId);
        }));
        tests.Add(("folder source rejects every ancestor overlap before destination writes",()=>
        {
            var (sourcePath,_)=Paths();string child=Path.Combine(sourcePath,"not-created");
            foreach(string destination in new[]{sourcePath,child,Path.GetDirectoryName(sourcePath)!})
                Fixtures.Throws<IOException>(()=>{using var source=new FolderVideoSource(sourcePath,destination);});
            Fixtures.Check(!Directory.Exists(child)&&!Directory.Exists(Path.Combine(sourcePath,".porto")));
            using var sibling=new FolderVideoSource(sourcePath,sourcePath+"-destination");
            Fixtures.Check(sibling.Inventory(default).EnumerationComplete);
        }));
        tests.Add(("folder source cancellation never becomes a partial success",()=>
        {
            var (sourcePath,destinationPath)=Paths();File.WriteAllBytes(Path.Combine(sourcePath,"one.mov"),Video());
            using var source=new FolderVideoSource(sourcePath,destinationPath);
            var item=source.Inventory(default).Items.Single();
            Fixtures.Throws<OperationCanceledException>(()=>source.Inventory(new CancellationToken(true)));
            Fixtures.Throws<OperationCanceledException>(()=>source.OpenRead(item,new CancellationToken(true)));
        }));
        tests.Add(("folder source reports a lost root as incomplete instead of silently empty",()=>
        {
            var (sourcePath,destinationPath)=Paths();
            using var source=new FolderVideoSource(sourcePath,destinationPath);Directory.Delete(sourcePath);
            var inventory=source.Inventory(default);
            Fixtures.Check(!inventory.EnumerationComplete&&inventory.Errors.Count>0);
        }));
        tests.Add(("folder source reports unreadable files and keeps readable siblings",()=>
        {
            var (sourcePath,destinationPath)=Paths();string locked=Path.Combine(sourcePath,"locked.mov");
            File.WriteAllBytes(locked,Video());File.WriteAllBytes(Path.Combine(sourcePath,"readable.mov"),Video(2));
            using var source=new FolderVideoSource(sourcePath,destinationPath);
            using var held=new FileStream(locked,FileMode.Open,FileAccess.Read,FileShare.None);
            var inventory=source.Inventory(default);
            Fixtures.Check(!inventory.EnumerationComplete&&inventory.Errors.Count>0&&inventory.Items.Single().Name=="readable.mov");
        }));
        tests.Add(("folder source refuses hard links during inventory and revalidates before reading",()=>
        {
            var (sourcePath,destinationPath)=Paths();string path=Path.Combine(sourcePath,"one.mov");byte[] bytes=Video();File.WriteAllBytes(path,bytes);
            using var source=new FolderVideoSource(sourcePath,destinationPath);
            var item=source.Inventory(default).Items.Single();
            Fixtures.Check(CreateHardLink(Path.Combine(Path.GetDirectoryName(sourcePath)!,"alias.mov"),path,0),"Could not create synthetic hard link.");
            Fixtures.Throws<IOException>(()=>source.OpenRead(item,default));
            var inventory=source.Inventory(default);
            Fixtures.Check(!inventory.EnumerationComplete&&inventory.Errors.Count>0&&inventory.Items.Count==0);
            Fixtures.Check(File.ReadAllBytes(path).SequenceEqual(bytes));
        }));
        tests.Add(("folder source refuses junction roots and never traverses linked subfolders",()=>
        {
            var (sourcePath,destinationPath)=Paths();string external=Path.Combine(Path.GetDirectoryName(sourcePath)!,"elsewhere");Directory.CreateDirectory(external);
            File.WriteAllBytes(Path.Combine(external,"outside.mov"),Video());
            string link=Path.Combine(sourcePath,"linked");Junction(link,external);
            Fixtures.Throws<IOException>(()=>{using var invalid=new FolderVideoSource(link,destinationPath);});
            using var source=new FolderVideoSource(sourcePath,destinationPath);var inventory=source.Inventory(default);
            Fixtures.Check(!inventory.EnumerationComplete&&inventory.Errors.Count>0&&inventory.Items.Count==0);
        }));
        tests.Add(("folder source rejects nonexistent network and device roots without creating them",()=>
        {
            var (sourcePath,destinationPath)=Paths();
            foreach(string invalid in new[]{Path.Combine(sourcePath,"missing"),@"\\localhost\absent",@"\\?\"+sourcePath})
                Fixtures.Throws<IOException>(()=>{using var source=new FolderVideoSource(invalid,destinationPath);});
            Fixtures.Check(!Directory.Exists(Path.Combine(sourcePath,"missing"))&&!Directory.Exists(destinationPath));
        }));
        tests.Add(("folder source disposal revokes inventory and read access",()=>
        {
            var (sourcePath,destinationPath)=Paths();File.WriteAllBytes(Path.Combine(sourcePath,"one.mov"),Video());
            var source=new FolderVideoSource(sourcePath,destinationPath);var item=source.Inventory(default).Items.Single();source.Dispose();
            Fixtures.Throws<ObjectDisposedException>(()=>source.Inventory(default));
            Fixtures.Throws<ObjectDisposedException>(()=>source.OpenRead(item,default));
        }));
    }

    private static (string Source,string Destination) Paths()
    {
        string root=Root(),source=Path.Combine(root,"source");Directory.CreateDirectory(source);
        return (source,Path.Combine(root,"destination"));
    }
    private static void Junction(string link,string target)
    {
        var start=new ProcessStartInfo("cmd.exe"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(string argument in new[]{"/d","/c","mklink","/J",link,target})start.ArgumentList.Add(argument);
        using var process=Process.Start(start)!;string output=process.StandardOutput.ReadToEnd(),error=process.StandardError.ReadToEnd();process.WaitForExit();
        Fixtures.Check(process.ExitCode==0,"Synthetic junction creation failed: "+output+error);
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string link,string existing,nint reserved);
}
