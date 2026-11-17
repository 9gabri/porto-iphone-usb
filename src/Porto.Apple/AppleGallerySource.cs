using System.Security.Cryptography;
using System.Text;
using Porto.Core;

namespace Porto.Apple;

public sealed class AppleGallerySource : IGallerySource,IReadFailureRecovery,ISourceTransportDiagnostics
{
    private readonly AppleDevice device;
    private readonly string? albumId;
    private readonly ImportMedia media;
    private readonly Func<CancellationToken,IAppleFileSystem> connect;
    private readonly string sourceNamespace;
    private IAppleFileSystem? files;
    private readonly Dictionary<string,(SourceItem Item,string? Path,AppleFileInfo? Info,bool VisibilityUnresolved)> observed=new(StringComparer.Ordinal);
    private bool disposed;
    private bool needsRecovery;
    private bool recoveryBlocked;
    public int SessionRestarts {get;private set;}
    public SourceTransportMetrics TransportMetrics {get;}=new();
    public AppleGalleryCatalog? CurrentCatalog {get;private set;}
    public GalleryCoverage? Coverage {get;private set;}
    public bool AppleResourceHints=>false;
    public string SourceDescription {get;private set;}
    public AppleGallerySource(AppleDevice device,string? albumId=null,ImportMedia media=ImportMedia.PhotosAndVideos,bool compatibleUsb=false):this(device,albumId,null,media,compatibleUsb){}
    internal AppleGallerySource(AppleDevice device,string? albumId,Func<CancellationToken,IAppleFileSystem>? connect,ImportMedia media=ImportMedia.PhotosAndVideos,bool compatibleUsb=false)
    {
        if(string.IsNullOrWhiteSpace(device.Id)||device.Id.Length>128)throw new ArgumentException("Identificativo iPhone non valido.",nameof(device));
        this.device=device;this.albumId=albumId;this.connect=connect??(cancel=>AppleUsbTransport.Connect(device,cancel,compatibleUsb,TransportMetrics));this.media=media;
        sourceNamespace="apple:"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(device.Id)));
        SourceDescription=device.Name+" — Galleria Foto iPhone via USB";
    }
    public Inventory Inventory(CancellationToken cancel)
    {
        ObjectDisposedException.ThrowIf(disposed,this);cancel.ThrowIfCancellationRequested();
        observed.Clear();CurrentCatalog=null;Coverage=null;needsRecovery=false;recoveryBlocked=false;files?.Dispose();files=null;
        files=connect(cancel);
        try
        {
            var catalog=AppleCatalogCapture.Read(files,cancel);
            var selected=catalog.Select(albumId);
            var errors=new List<string>();int unresolved=0;bool unavailableFound=false,unknownFound=false,metadataFailureFound=false,visibilityFailureFound=false;
            foreach(var asset in selected)
            {
                cancel.ThrowIfCancellationRequested();
                bool relevant=asset.Kind==MediaKind.Unknown||asset.Kind==MediaKind.Image&&media!=ImportMedia.Videos||asset.Kind==MediaKind.Video&&media!=ImportMedia.Photos;
                AppleFileInfo? info=null;
                bool metadataFailed=false;
                bool visibilityFailed=relevant&&asset.VisibilityUnresolved;visibilityFailureFound|=visibilityFailed;
                if(relevant&&!visibilityFailed&&asset.Path!=null)
                {
                    try{info=files.Stat(asset.Path,cancel);}
                    catch(Exception error) when((error is IOException or UnauthorizedAccessException)&&files.IsUsable)
                    {
                        cancel.ThrowIfCancellationRequested();metadataFailed=true;metadataFailureFound=true;
                    }
                }
                bool unavailable=relevant&&!visibilityFailed&&(asset.Path==null||info==null||!info.IsFile);
                bool cannotRead=unavailable||visibilityFailed||asset.Kind==MediaKind.Unknown;
                if(relevant&&(unavailable||visibilityFailed||asset.Kind==MediaKind.Unknown))unresolved++;
                unavailableFound|=unavailable&&!metadataFailed;unknownFound|=relevant&&asset.Kind==MediaKind.Unknown;
                string id="asset:"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(asset.Id)));
                string version=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                    (asset.Path??"unresolved")+"\0"+asset.LivePhoto+"\0"+(info?.Modification??(relevant?"unavailable":"excluded-media"))+(visibilityFailed?"\0visibility-unresolved":""))));
                var item=new SourceItem(id,albumId??"gallery",asset.Name,asset.Kind,info?.Length,asset.Id,
                    "apple-original:"+version,cannotRead,sourceNamespace);
                observed.Add(id,(item,asset.Path,info,visibilityFailed));
            }
            if(unavailableFound)errors.Add("Uno o più elementi selezionati del catalogo Foto non hanno un originale locale accessibile via USB; restano inclusi nella selezione.");
            if(metadataFailureFound)errors.Add("I metadati di uno o più originali Foto non sono verificabili. Gli elementi restano non risolti; la lettura degli altri originali prosegue.");
            if(unknownFound)errors.Add("Uno o più elementi Foto hanno un tipo non riconosciuto e non vengono classificati per estensione.");
            if(visibilityFailureFound)errors.Add("Uno o più elementi Foto selezionati hanno visibilità non verificata nel catalogo: restano conteggiati come non risolti e non vengono copiati.");
            CurrentCatalog=catalog;
            SourceDescription=device.Name+" — "+(albumId==null?"tutta la galleria Foto osservata":"album Foto: "+catalog.Albums.Single(a=>a.Id==albumId).Name);
            Coverage=new(catalog.ScopeNote,media==ImportMedia.Videos?0:selected.Count(a=>a.Kind==MediaKind.Image),media==ImportMedia.Photos?0:selected.Count(a=>a.Kind==MediaKind.Video),unresolved,catalog.HiddenAssetCount,catalog.TrashedAssetCount);
            return new(observed.Values.Select(v=>v.Item).ToList(),errors,errors.Count==0);
        }
        catch{observed.Clear();files.Dispose();files=null;throw;}
    }
    public string? SelectionUncertainty(SourceItem item)=>observed.TryGetValue(item.Id,out var entry)&&entry.Item==item&&entry.VisibilityUnresolved?
        "Visibilità dell'elemento Foto non verificata nel catalogo; originale non aperto.":null;
    public Stream OpenRead(SourceItem item,CancellationToken cancel)
    {
        ObjectDisposedException.ThrowIf(disposed,this);cancel.ThrowIfCancellationRequested();
        if(recoveryBlocked)throw PreviousReadFailure();
        if(files is {IsUsable:false})needsRecovery=true;
        if(needsRecovery)RecoverBeforeRead(item,cancel);
        if(files==null||!observed.TryGetValue(item.Id,out var entry)||entry.Item!=item)throw new IOException("L'elemento non appartiene alla selezione Foto corrente.");
        if(entry.Path==null||entry.Info==null||!entry.Info.IsFile||item.Offline)throw new IOException("Originale Foto non disponibile localmente; Porto non scarica da iCloud.");
        if(files.Stat(entry.Path,cancel)!=entry.Info)throw new IOException("L'originale Foto è cambiato dopo l'inventario.");
        return new VerifiedSourceStream(files.OpenRead(entry.Path,cancel),files,entry.Path,entry.Info,cancel);
    }
    private void RecoverBeforeRead(SourceItem item,CancellationToken cancel)
    {
        try
        {
            if(!observed.TryGetValue(item.Id,out var requested)||requested.Item!=item)
                throw new IOException("Manca la selezione Foto precedente per verificare il recupero.");
            var previous=observed.Values.Select(v=>v.Item).OrderBy(i=>i.Id,StringComparer.Ordinal).ToArray();
            SessionRestarts++;
            var current=Inventory(cancel).Items.OrderBy(i=>i.Id,StringComparer.Ordinal).ToArray();
            if(!previous.SequenceEqual(current))throw new IOException("Il catalogo Foto o gli originali sono cambiati dopo un errore di lettura.");
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException)
        {
            observed.Clear();CurrentCatalog=null;Coverage=null;needsRecovery=false;recoveryBlocked=true;
            var rejected=files;files=null;
            try{rejected?.Dispose();}catch(Exception cleanup) when(cleanup is IOException or UnauthorizedAccessException){}
            throw PreviousReadFailure(error);
        }
    }
    private static PreviousReadCleanupException PreviousReadFailure(Exception? error=null)=>new(
        "Recupero della precedente lettura Foto non concluso. Il file successivo non è stato aperto; serve un nuovo inventario.",error);
    public void InvalidateRead(){needsRecovery=true;}
    public void Dispose(){if(disposed)return;disposed=true;observed.Clear();files?.Dispose();files=null;}
    private sealed class VerifiedSourceStream(Stream inner,IAppleFileSystem files,string path,AppleFileInfo expected,CancellationToken cancel) : Stream
    {
        private long length;private bool ended;
        public override int Read(byte[] buffer,int offset,int count)
        {
            cancel.ThrowIfCancellationRequested();if(count==0)return 0;if(ended)return 0;
            int read=inner.Read(buffer,offset,count);length=checked(length+read);
            if(length>expected.Length)throw new IOException("Originale Foto più lungo dell'inventario.");
            if(read==0)
            {
                if(length!=expected.Length||files.Stat(path,cancel)!=expected)throw new IOException("Originale Foto troncato o cambiato durante la lettura.");
                ended=true;
            }
            return read;
        }
        protected override void Dispose(bool disposing){if(disposing)inner.Dispose();base.Dispose(disposing);}
        public override bool CanRead=>inner.CanRead;public override bool CanSeek=>false;public override bool CanWrite=>false;
        public override long Length=>expected.Length;public override long Position{get=>length;set=>throw new NotSupportedException();}
        public override void Flush(){}public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    }
}

internal static class AppleCatalogCapture
{
    private const string Database="/PhotoData/Photos.sqlite";
    internal static AppleGalleryCatalog Read(IAppleFileSystem files,CancellationToken cancel)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancel);deadline.CancelAfter(TimeSpan.FromMinutes(10));
        AppleCatalogBuffer? db=null,merged=null;byte[]? wal=null;
        try
        {
            AppleFileInfo firstInfo;AppleFileInfo? firstWalInfo,firstJournal;
            (db,wal,firstInfo,firstWalInfo,firstJournal)=Capture(files,deadline.Token);
            VerifyCapture(files,db,wal,firstInfo,firstWalInfo,firstJournal,deadline.Token);
            merged=AppleSqliteImage.ApplyWal(db,wal,deadline.Token);
            return AppleCatalogDecoder.Decode(merged,deadline.Token);
        }
        catch(OperationCanceledException) when(!cancel.IsCancellationRequested){throw new IOException("Lettura del catalogo Foto oltre il limite di dieci minuti.");}
        catch(OutOfMemoryException){throw new IOException("[APPLE-CATALOG-MEMORY] Memoria insufficiente durante la lettura del catalogo. Chiudi le altre applicazioni e riprova.");}
        finally
        {
            merged?.Dispose();db?.Dispose();if(wal!=null)CryptographicOperations.ZeroMemory(wal);
        }
    }
    private static (AppleCatalogBuffer Database,byte[]? Wal,AppleFileInfo Info,AppleFileInfo? WalInfo,AppleFileInfo? Journal) Capture(IAppleFileSystem files,CancellationToken cancel)
    {
        var info=files.Stat(Database,cancel)??throw new IOException("L'iPhone non espone il catalogo Foto tramite il servizio USB Apple. Nessun elenco di file viene sostituito agli album.");
        var walInfo=files.Stat(Database+"-wal",cancel);var journalInfo=files.Stat(Database+"-journal",cancel);
        ValidateInactiveJournal(files,journalInfo,cancel);
        ValidateMetadata(info,walInfo);
        AppleCatalogMemory.EnsureAvailable(AppleCatalogMemory.CaptureAllocation(info.Length+(walInfo?.Length??0)));
        AppleCatalogBuffer? data=null;byte[]? wal=null;
        try
        {
            data=ReadDatabase(files,info.Length,cancel);
            if(walInfo!=null)wal=Read(files,Database+"-wal",walInfo.Length,cancel);
            ValidateInactiveJournal(files,journalInfo,cancel);
            if(info!=files.Stat(Database,cancel)||walInfo!=files.Stat(Database+"-wal",cancel)||journalInfo!=files.Stat(Database+"-journal",cancel))
                throw new IOException("Il catalogo Foto è cambiato durante l'acquisizione.");
            return(data,wal,info,walInfo,journalInfo);
        }
        catch{data?.Dispose();if(wal!=null)CryptographicOperations.ZeroMemory(wal);throw;}
    }
    internal static void ValidateMetadata(AppleFileInfo info,AppleFileInfo? wal)
    {
        string? reason=!info.IsFile?"DB-TYPE":info.Length<=0?"DB-EMPTY":
            info.Length>AppleSqliteImage.MaximumBytes?"DB-LIMIT":wal is {IsFile:false}?"WAL-TYPE":
            wal is {Length:<0}?"WAL-LENGTH":wal?.Length>AppleSqliteImage.MaximumBytes?"WAL-LIMIT":
            info.Length+(wal?.Length??0)>AppleSqliteImage.MaximumBytes?"TOTAL-LIMIT":null;
        if(reason==null)return;
        string detail=reason switch
        {
            "DB-TYPE"=>$"Il database Foto è esposto come {NonFileDescription(info.NonFileKind)}.",
            "DB-EMPTY"=>"Il database Foto risulta vuoto o con dimensione non valida.",
            "DB-LIMIT"=>"Il database Foto supera il limite di memoria.",
            "WAL-TYPE"=>$"Il journal WAL è esposto come {NonFileDescription(wal!.NonFileKind)}.",
            "WAL-LENGTH"=>"Il journal WAL ha una dimensione non valida.",
            "WAL-LIMIT"=>"Il journal WAL supera il limite di memoria.",
            _=>"La somma di database e WAL supera il limite di memoria."
        };
        // Only fixed roles and numeric metadata: no paths, contents, device IDs or library names.
        throw new IOException(FormattableString.Invariant($"[APPLE-CATALOG-{reason}] {detail} DB: {info.Length} byte; WAL: {(wal==null?"assente":wal.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)+" byte")}. Limite: {AppleSqliteImage.MaximumBytes} byte ({AppleSqliteImage.MaximumDescription})."));
    }
    private static string NonFileDescription(AppleNonFileKind kind)=>kind switch
    {AppleNonFileKind.Directory=>"cartella",AppleNonFileKind.SymbolicLink=>"collegamento",_=>"tipo non regolare non riconosciuto"};
    private static void ValidateInactiveJournal(IAppleFileSystem files,AppleFileInfo? info,CancellationToken cancel)
    {
        if(info==null)return;
        if(!info.IsFile||info.Length<0||info.Length>AppleSqliteImage.MaximumBytes)throw JournalUnavailable();
        if(info.Length==0)return;
        if(info.Length<28)throw JournalUnavailable();
        // SQLite PERSIST commits by zeroing its complete 28-byte rollback-journal header.
        // Other nonempty headers remain unsupported; no rollback or on-device write is attempted.
        byte[] header=new byte[28];
        try
        {
            using var input=files.OpenRead(Database+"-journal",cancel);int offset=0;
            while(offset<header.Length)
            {
                cancel.ThrowIfCancellationRequested();int read=input.Read(header,offset,header.Length-offset);
                if(read==0)throw JournalUnavailable();offset+=read;
            }
            if(header.AsSpan().IndexOfAnyExcept((byte)0)>=0)throw JournalUnavailable();
        }
        finally{CryptographicOperations.ZeroMemory(header);}
    }
    private static IOException JournalUnavailable()=>new("Catalogo Foto con journal attivo o non verificabile: lettura arrestata, riprova quando la libreria è stabile.");
    private static AppleCatalogBuffer ReadDatabase(IAppleFileSystem files,long length,CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        var result=new AppleCatalogBuffer(checked((int)length));
        byte[]? scratch=null;
        try
        {
            scratch=new byte[1024*1024];
            using var input=files.OpenRead(Database,cancel);int offset=0;
            while(offset<result.Length)
            {
                cancel.ThrowIfCancellationRequested();
                int read=input.Read(scratch,0,Math.Min(scratch.Length,result.Length-offset));
                if(read==0)throw new IOException("Catalogo Foto troncato durante la lettura.");
                scratch.AsSpan(0,read).CopyTo(result.Data.Slice(offset,read));offset+=read;
            }
            if(input.ReadByte()!=-1)throw new IOException("Il catalogo Foto è cresciuto durante la lettura.");
            return result;
        }
        catch{result.Dispose();throw;}
        finally{if(scratch!=null)CryptographicOperations.ZeroMemory(scratch);}
    }
    private static byte[] Read(IAppleFileSystem files,string path,long length,CancellationToken cancel)
    {
        byte[] result=new byte[checked((int)length)];
        try
        {
            using var input=files.OpenRead(path,cancel);int offset=0;
            while(offset<result.Length)
            {
                cancel.ThrowIfCancellationRequested();int read=input.Read(result,offset,Math.Min(1024*1024,result.Length-offset));
                if(read==0)throw new IOException("Catalogo Foto troncato durante la lettura.");offset+=read;
            }
            if(input.ReadByte()!=-1)throw new IOException("Il catalogo Foto è cresciuto durante la lettura.");return result;
        }
        catch{CryptographicOperations.ZeroMemory(result);throw;}
    }
    private static void VerifyCapture(IAppleFileSystem files,AppleCatalogBuffer database,byte[]? wal,AppleFileInfo info,AppleFileInfo? walInfo,AppleFileInfo? journalInfo,CancellationToken cancel)
    {
        void CheckMetadata()
        {
            cancel.ThrowIfCancellationRequested();
            if(info!=files.Stat(Database,cancel)||walInfo!=files.Stat(Database+"-wal",cancel)||journalInfo!=files.Stat(Database+"-journal",cancel))
                throw ChangedDuringVerification();
        }
        CheckMetadata();
        ValidateInactiveJournal(files,journalInfo,cancel);
        // Re-read every byte exactly, retaining only a bounded comparison buffer rather than a second catalog.
        byte[] scratch=new byte[1024*1024];
        try
        {
            Compare(Database,database.Data);
            if(wal!=null)Compare(Database+"-wal",wal);
            ValidateInactiveJournal(files,journalInfo,cancel);
            CheckMetadata();
        }
        finally{CryptographicOperations.ZeroMemory(scratch);}

        void Compare(string path,ReadOnlySpan<byte> expected)
        {
            using var input=files.OpenRead(path,cancel);int offset=0;
            while(offset<expected.Length)
            {
                cancel.ThrowIfCancellationRequested();
                int read=input.Read(scratch,0,Math.Min(scratch.Length,expected.Length-offset));
                if(read==0||!scratch.AsSpan(0,read).SequenceEqual(expected.Slice(offset,read)))throw ChangedDuringVerification();
                offset+=read;
            }
            cancel.ThrowIfCancellationRequested();
            if(input.ReadByte()!=-1)throw ChangedDuringVerification();
        }
    }
    private static IOException ChangedDuringVerification()=>new("Il catalogo Foto è cambiato durante la lettura. Ripeti senza modificare la libreria.");
}
