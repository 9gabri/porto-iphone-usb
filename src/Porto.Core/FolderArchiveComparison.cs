namespace Porto.Core;

// Read-only discovery also covers archives whose previous catalog was not copied.
// These entries must NEVER be written into the managed catalog or used as replacement targets.
internal static class FolderArchiveComparison
{
    public static List<CatalogEntry> FindUnregistered(Destination destination,List<CatalogEntry> registered,CancellationToken cancel,Action<string,long>? progress=null,ImportMedia media=ImportMedia.Videos)
    {
        var known=registered.Where(e=>!string.IsNullOrEmpty(e.Final)).Select(e=>e.Final).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var found=new List<CatalogEntry>();var pending=new Stack<string>();pending.Push(destination.Root);
        while(pending.TryPop(out string? directory))
        {
            cancel.ThrowIfCancellationRequested();Destination.CheckAncestors(directory);
            foreach(string path in Directory.EnumerateFileSystemEntries(directory))
            {
                cancel.ThrowIfCancellationRequested();
                string relative=Path.GetRelativePath(destination.Root,path);
                if(relative.Equals(".porto",StringComparison.OrdinalIgnoreCase)||relative.Equals("Report Porto",StringComparison.OrdinalIgnoreCase))continue;
                Destination.CheckAncestors(path);
                if(Directory.Exists(path)){pending.Push(path);continue;}
                string extension=Path.GetExtension(path);
                bool photo=PhotoInspector.SupportedExtensions.Contains(extension);
                if(known.Contains(relative)||!MediaSelectionRules.SupportedExtensions.Contains(extension)||
                    media==ImportMedia.Videos&&photo||media==ImportMedia.Photos&&!photo)continue;
                progress?.Invoke(relative,0);
                var fingerprint=VerifiedCopy.HashFile(path,cancel,n=>progress?.Invoke(relative,n));
                found.Add(new("external-"+Guid.NewGuid().ToString("N"),"Complete","",relative,fingerprint.Hash,fingerprint.Length,
                    new SourceItem("archive:"+relative,"archive",Path.GetFileName(path),photo?MediaKind.Image:MediaKind.Video,fingerprint.Length)));
            }
        }
        return found;
    }
}
