using Porto.Core;

namespace Porto.Apple;

public sealed record AppleDevice(string Id,string Name);
public sealed record AppleDiscoveryResult(IReadOnlyList<AppleDevice> Devices,string? UnavailableReason);
public sealed record AppleAlbum(string Id,string Name,int AssetCount,int PhotoCount,int VideoCount);

// The catalog is an observation of a private, undocumented on-device schema, never PhotoKit certification.
public sealed class AppleGalleryCatalog
{
    public IReadOnlyList<AppleAlbum> Albums {get;}
    public int VisibleAssetCount=>Assets.Count;
    public int VisiblePhotoCount=>Assets.Count(a=>a.Kind==MediaKind.Image);
    public int VisibleVideoCount=>Assets.Count(a=>a.Kind==MediaKind.Video);
    public int LivePhotoCount=>Assets.Count(a=>a.LivePhoto);
    public int UnresolvedAssetCount=>Assets.Count(a=>a.Path==null||a.Kind==MediaKind.Unknown||a.VisibilityUnresolved);
    public int VisibilityUnresolvedAssetCount=>Assets.Count(a=>a.VisibilityUnresolved);
    public bool VisibilityMetadataAvailable {get;}
    public int HiddenAssetCount {get;}
    public int TrashedAssetCount {get;}
    public bool AtomicSnapshotVerified=>false;
    public string ScopeNote=>"Catalogo Foto osservato; album personali riconosciuti, raccolte intelligenti e condivise non supportate. Esclusi Nascosti ed Eliminati di recente. Solo originali locali; Live Photo: sola immagine. La lettura ripetuta non certifica uno snapshot atomico della libreria."+
        (!VisibilityMetadataAvailable?" Indicatore di visibilità non disponibile: la selezione comprende le righe del catalogo non nascoste e non eliminate, senza certificare la corrispondenza al rullino.":
        VisibilityUnresolvedAssetCount>0?$" Nel catalogo {VisibilityUnresolvedAssetCount} elementi hanno visibilità non verificata: restano conteggiati e non vengono copiati se selezionati.":"");
    internal IReadOnlyList<AppleAsset> Assets {get;}
    private readonly IReadOnlyDictionary<string,IReadOnlyList<AppleAsset>> members;
    internal AppleGalleryCatalog(List<AppleAsset> assets,List<AppleAlbum> albums,
        Dictionary<string,IReadOnlyList<AppleAsset>> members,int hidden,int trashed,bool visibilityMetadataAvailable)
    {
        Assets=assets.AsReadOnly();Albums=albums.AsReadOnly();this.members=members;
        HiddenAssetCount=hidden;TrashedAssetCount=trashed;
        VisibilityMetadataAvailable=visibilityMetadataAvailable;
    }
    internal IReadOnlyList<AppleAsset> Select(string? albumId)
    {
        if(albumId==null)return Assets;
        if(!members.TryGetValue(albumId,out var selected))throw new IOException("L'album Foto selezionato non è più disponibile: selezionalo nuovamente.");
        return selected;
    }
}

internal sealed record AppleAsset(long Key,string Id,string Name,string? Path,MediaKind Kind,bool LivePhoto,bool VisibilityUnresolved);
internal sealed record AppleFileInfo(long Length,string Modification,bool IsFile);
internal interface IAppleFileSystem : IDisposable
{
    bool IsUsable=>true;
    AppleFileInfo? Stat(string path,CancellationToken cancel);
    Stream OpenRead(string path,CancellationToken cancel);
}
