namespace Porto.Core;

// A native WPD collection exposed by the device, distinct from a physical folder
// and from the complete album catalog of any phone gallery app.
public sealed record UsbCollectionChoice(string DisplayPath,IReadOnlyList<UsbFolderSegment> Segments)
{
    public override string ToString()=>"[Raccolta USB] "+DisplayPath;
}
public sealed record UsbNavigationLevel(IReadOnlyList<UsbFolderChoice> Folders,IReadOnlyList<UsbCollectionChoice> Collections,
    IReadOnlyList<string>? Warnings=null);

public interface IUsbCollectionSource : IUsbFolderSource
{
    UsbNavigationLevel ReadLevel(UsbFolderChoice? parent,CancellationToken cancel);
    IReadOnlyList<UsbCollectionChoice> ListCollections(UsbFolderChoice? parent,CancellationToken cancel);
    Inventory Inventory(UsbCollectionChoice collection,CancellationToken cancel);
}
