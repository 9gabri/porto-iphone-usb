namespace Porto.Core;

public static class MediaSelectionRules
{
    public static readonly HashSet<string> SupportedExtensions=new(MediaInspector.VideoExtensions.Concat(PhotoInspector.SupportedExtensions),StringComparer.OrdinalIgnoreCase);
    public static bool Candidate(SourceItem item,ImportMedia selection)=>selection switch
    {
        ImportMedia.Videos=>MediaInspector.Candidate(item),
        ImportMedia.Photos=>PhotoInspector.Candidate(item),
        ImportMedia.PhotosAndVideos=>MediaInspector.Candidate(item)||PhotoInspector.Candidate(item),
        _=>throw new ArgumentOutOfRangeException(nameof(selection))
    };
    public static MediaInspection Inspect(string path,SourceItem item,CancellationToken cancel)=>
        PhotoInspector.Candidate(item)&&!MediaInspector.Candidate(item)?PhotoInspector.Inspect(path,cancel):MediaInspector.Inspect(path,cancel);
    // A prediction for staging only. The inspector's content-derived extension decides publication.
    public static string ExpectedExtension(SourceItem item)
    {
        string extension=Path.GetExtension(item.Name).ToLowerInvariant();
        return SupportedExtensions.Contains(extension)?extension:PhotoInspector.Candidate(item)?".jpg":".mp4";
    }
}
