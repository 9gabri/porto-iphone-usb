namespace Porto.Core;

public enum MediaKind { Video, Image, Container, Other, Unknown }
public enum Outcome { Imported, AlreadyPresent, Excluded, Ambiguous, Missing, Error, Pending }
public sealed record DeviceInfo(string Id, string Name);
public sealed record SourceItem(string Id, string ParentId, string Name, MediaKind Kind,
    long? Size = null, string? PersistentId = null, string? Format = null, bool Offline = false);
public sealed record Inventory(List<SourceItem> Items, List<string> Errors, bool EnumerationComplete);
public interface IVideoSource : IDisposable
{
    Inventory Inventory(CancellationToken cancel);
    Stream OpenRead(SourceItem item, CancellationToken cancel);
}
public sealed record ReportItem(string SourceId, string Name, Outcome Outcome, string Detail,
    string? File = null, long? Bytes = null, string? Sha256 = null);
public sealed class ImportReport
{
    public DateTimeOffset StartedUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedUtc { get; set; }
    public bool UsbEnumerationComplete { get; set; }
    public bool EntireLibraryVerified => false;
    public bool OriginalityIndependentlyVerified => false;
    public bool SecondSourceRead { get; set; }
    public bool FinalInventoryChecked { get; set; }
    public bool RetryRecommended { get; set; }
    public bool StoppedForCapacity { get; set; }
    public int CandidateCount { get; set; }
    public int Attempt { get; set; } = 1;
    public List<string> InventoryErrors { get; set; } = [];
    public List<ReportItem> Items { get; } = [];
    public string Status { get; set; } = "In corso";
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
public sealed class SourceUnavailableException(string message,Exception? inner=null) : IOException(message,inner);
