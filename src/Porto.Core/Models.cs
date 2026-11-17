namespace Porto.Core;

public enum MediaKind { Video, Image, Container, Other, Unknown, Collection }
public enum ImportMedia { Videos, Photos, PhotosAndVideos }
public enum Outcome { Imported, AlreadyPresent, Excluded, Ambiguous, Missing, Error, Pending, Additional, AdditionalAlreadyPresent }
public sealed record DeviceInfo(string Id, string Name, bool IsApple = false);
public sealed record SourceItem(string Id, string ParentId, string Name, MediaKind Kind,
    long? Size = null, string? PersistentId = null, string? Format = null, bool Offline = false,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? SourceNamespace = null);
public sealed record Inventory(List<SourceItem> Items, List<string> Errors, bool EnumerationComplete);
public interface IVideoSource : IDisposable
{
    Inventory Inventory(CancellationToken cancel);
    Stream OpenRead(SourceItem item, CancellationToken cancel);
}
// Enumerates only the opened level; a scoped inventory must never widen to the device root.
public interface IUsbFolderSource : IVideoSource
{
    IReadOnlyList<UsbFolderChoice> ListFolders(UsbFolderChoice? parent, CancellationToken cancel);
    Inventory Inventory(UsbFolderChoice folder, CancellationToken cancel);
}
// A folder explicitly selected by the user. This does not certify membership in Photos.
public interface ISelectedVideoSource : IVideoSource
{
    string SelectionDescription { get; }
}
public interface ISourceContext
{
    string SourceDescription { get; }
    bool AppleResourceHints { get; }
}
// The source supplies asset membership, rather than inferring it from USB filenames.
public interface IGallerySource : IVideoSource, ISourceContext
{
    GalleryCoverage? Coverage { get; }
    // Uncertain selected membership remains counted, but must not open the original.
    string? SelectionUncertainty(SourceItem item) => null;
}
public sealed record GalleryCoverage(string Note, int Photos, int Videos, int Unresolved,
    int HiddenExcluded, int DeletedExcluded, bool AtomicSnapshotVerified = false);
public sealed record PreviousCopyComparison(string File, long Bytes, string? Sha256,
    bool ReusedForSelection, bool IntegrityVerified, string Detail);
// Mark a failed read without performing I/O or replacing its original exception.
// The next read must use a fresh connection and a fully revalidated inventory.
public interface IReadFailureRecovery
{
    void InvalidateRead();
    int SessionRestarts { get; }
}
public sealed record ReportItem(string SourceId, string Name, Outcome Outcome, string Detail,
    string? File = null, long? Bytes = null, string? Sha256 = null, bool Historical = false);
public sealed class ImportReport
{
    public ImportPerformance? Performance {get;set;}
    public ImportMedia Media { get; init; } = ImportMedia.Videos;
    public int SelectedPhotoCount { get; set; }
    public int SelectedVideoCount { get; set; }
    public bool SelectedFolder { get; init; }
    public string SourceDescription { get; set; } = "Dispositivo tramite USB";
    public bool SelectedFolderVerified { get; set; }
    public bool SelectedGallery { get; init; }
    public bool GallerySelectionCopiesVerified { get; set; }
    // Only the current automatic retry sequence consumes these observation keys.
    // A new explicit session can legitimately select a changed gallery.
    [System.Text.Json.Serialization.JsonIgnore]
    internal HashSet<string> GalleryObservationKeys { get; } = new(StringComparer.Ordinal);
    public GalleryCoverage? GalleryCoverage { get; set; }
    public int ExcludedByMediaFilter { get; set; }
    public List<PreviousCopyComparison> PreviousCopies { get; } = [];
    public string ApplicationVersion => typeof(ImportReport).Assembly
        .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute),false)
        .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion.Split('+')[0]??"sconosciuta";
    public int UsbSessionRestarts { get; set; }
    public DateTimeOffset StartedUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedUtc { get; set; }
    public bool UsbEnumerationComplete { get; set; }
    public bool EntireLibraryVerified => false;
    public bool OriginalityIndependentlyVerified => false;
    public bool SecondSourceRead { get; set; }
    public bool FinalInventoryChecked { get; set; }
    public bool RetryRecommended { get; set; }
    public bool StoppedForCapacity { get; set; }
    // Runtime scheduling hints only; metadata never establishes identity or completion.
    [System.Text.Json.Serialization.JsonIgnore]
    public List<RetryHint> RetryLast { get; } = [];
    [System.Text.Json.Serialization.JsonIgnore]
    public List<RetryHint> RetryVisited { get; } = [];
    [System.Text.Json.Serialization.JsonIgnore]
    public bool ReadRecoveryInterrupted { get; set; }
    public int CandidateCount { get; set; }
    public int UsbItemCount { get; set; }
    public int ImageCount { get; set; }
    public int SidecarCount { get; set; }
    public int SuspectedCompanionCount { get; set; }
    public int TransferCandidateCount { get; set; }
    public int Attempt { get; set; } = 1;
    public List<string> InventoryErrors { get; set; } = [];
    public List<ReportItem> Items { get; } = [];
    public string Status { get; set; } = "In corso";
    public int CurrentCount(Outcome outcome) => Items.Count(i => !i.Historical && i.Outcome == outcome);
    public int HistoricalCount => Items.Count(i => i.Historical);
    public int Count(Outcome outcome) => Items.Count(i => i.Outcome == outcome);
}
public sealed record ImportProgress(string Phase, string Name, int Done, int Total, long Bytes,
    long? TotalBytes, long? FreeBytes, double BytesPerSecond = 0);
public sealed record CopyResult(long Length, string Hash);
public interface IDiskSpace { long Available(string directory); }
public sealed class LocalDiskSpace : IDiskSpace
{
    public long Available(string directory) => new DriveInfo(Path.GetPathRoot(directory)!).AvailableFreeSpace;
}
public sealed class SpaceException(string message) : IOException(message);
public class SourceUnavailableException(string message,Exception? inner=null) : IOException(message,inner);
// The next resource has not been opened: failure belongs to cleanup of the prior read.
public sealed class PreviousReadCleanupException(string message,Exception? inner=null) : SourceUnavailableException(message,inner);
