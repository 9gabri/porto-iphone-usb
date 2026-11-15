namespace Porto.Core;

// Length of the opened resource, not the inventory object's potentially stale size.
public interface ISourceStreamInfo
{
    long? ResourceLength { get; }
}
