using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;

namespace Porto.Core;

public sealed record MediaInspection(bool IsVideo,bool LivePhoto,bool Ambiguous,string Extension,string Detail)
{
    // A routing hint only: USB metadata cannot certify a complete Photos asset.
    public bool AdditionalResource {get;init;}
}
public static partial class MediaInspector
{
    public static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase){".mov",".mp4",".m4v"};
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase){".jpg",".jpeg",".heic",".heif",".png",".dng"};
    public static bool Candidate(SourceItem item) => item.Kind==MediaKind.Video ||
        (item.Kind is MediaKind.Other or MediaKind.Unknown && (VideoExtensions.Contains(Path.GetExtension(item.Name)) ||
        item.Format?.StartsWith("b9820000",StringComparison.OrdinalIgnoreCase)==true || item.Format?.StartsWith("300b0000",StringComparison.OrdinalIgnoreCase)==true));

    // Recognize only the documented adjustment-sidecar extension, never an
    // arbitrary unknown resource or metadata that explicitly identifies video.
    public static bool IsAdjustmentSidecar(SourceItem item) => item.Kind is MediaKind.Other or MediaKind.Unknown &&
        Path.GetExtension(item.Name).Equals(".aae",StringComparison.OrdinalIgnoreCase)&&!Candidate(item);

    public static Dictionary<string,string> SuspectedCompanions(IEnumerable<SourceItem> items)
    {
        var photos = new HashSet<string>(items.Where(i=>i.Kind==MediaKind.Image||
            i.Kind is MediaKind.Other or MediaKind.Unknown&&!Candidate(i)&&ImageExtensions.Contains(Path.GetExtension(i.Name)))
            .Select(Key),StringComparer.OrdinalIgnoreCase);
        Dictionary<string,string> result=[];
        foreach(var i in items.Where(Candidate))
        {
            bool paired=photos.Contains(Key(i)),edited=EditedName().IsMatch(Path.GetFileNameWithoutExtension(i.Name));
            if(paired||edited)result[i.Id]=
                (paired?"Foto omonima nella stessa cartella USB: possibile componente Live Photo. ":"")+
                (edited?"Nome IMG_E: possibile variante modificata. ":"")+
                "Indizi di classificazione, non prove dell'identità Foto: dopo la verifica dei byte, copia nelle risorse aggiuntive.";
        }
        return result;
    }
    private static string Key(SourceItem item)=>item.ParentId+"/"+Path.GetFileNameWithoutExtension(item.Name);
    [GeneratedRegex(@"^IMG_E\d+$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)] private static partial Regex EditedName();

    private sealed class TrackMetadata
    {
        public bool Video,Metadata,StillImageTimeKey;
    }

    public static MediaInspection Inspect(string path,CancellationToken cancel)
    {
        using var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        bool video=false,mdat=false,moov=false,stillImageTimeKey=false;
        string extension=".mp4"; int atoms=0;
        const string sampleTable="moov/trak/mdia/minf/stbl";
        const string sampleDescription=sampleTable+"/stsd";
        const string timedKeys=sampleDescription+"/mebx/keys";
        void Walk(long start,long end,int depth,string parentPath,TrackMetadata? track=null,uint? expectedCount=null)
        {
            if(depth>20) throw new InvalidDataException("Struttura video troppo complessa.");
            byte[] head=new byte[16];uint childCount=0;
            for(long p=start;p<end;)
            {
                cancel.ThrowIfCancellationRequested();
                // QTFF permits four trailing zero bytes within a sample description.
                if(parentPath==sampleDescription+"/mebx"&&end-p==4)
                {
                    f.Position=p;f.ReadExactly(head.AsSpan(0,4));
                    if(BinaryPrimitives.ReadUInt32BigEndian(head)==0)break;
                }
                childCount++;
                if(++atoms>200000 || end-p<8) throw new InvalidDataException("Struttura contenitore incompleta.");
                f.Position=p; f.ReadExactly(head.AsSpan(0,8));
                ulong size=BinaryPrimitives.ReadUInt32BigEndian(head); int header=8;
                string type=Encoding.ASCII.GetString(head,4,4);
                if(size==1)
                {
                    if(end-p<16)throw new InvalidDataException("Intestazione estesa del contenitore troncata.");
                    f.ReadExactly(head.AsSpan(8,8));size=BinaryPrimitives.ReadUInt64BigEndian(head.AsSpan(8));header=16;
                }
                if(size==0)size=(ulong)(end-p);
                if(size<(ulong)header || size>(ulong)(end-p)) throw new InvalidDataException("Dimensioni contenitore non valide.");
                long next=checked(p+(long)size),data=p+header;
                if(parentPath==timedKeys)
                {
                    // These types are local key IDs; even FourCCs such as "free" or
                    // "moov" are permitted and must not be interpreted as containers.
                    uint keyId=BinaryPrimitives.ReadUInt32BigEndian(head.AsSpan(4));
                    if(keyId is not 0 and not uint.MaxValue)Walk(data,next,depth+1,timedKeys+"/key",track);
                    p=next;continue;
                }
                if(parentPath==timedKeys+"/key")
                {
                    if(type=="keyd"&&track!=null)track.StillImageTimeKey|=MatchesStillImageTimeKey(f,data,next);
                    p=next;continue;
                }
                if(type=="ftyp" && size>=(ulong)header+8){f.Position=data;f.ReadExactly(head.AsSpan(0,4)); string brand=Encoding.ASCII.GetString(head,0,4); if(brand=="qt  ")extension=".mov";if(brand=="M4V ")extension=".m4v";}
                if(type=="mdat" && parentPath=="" && size>(ulong)header)mdat=true;
                if(type=="moov")
                {
                    if(parentPath!="")throw new InvalidDataException("Atomo moov fuori dalla radice del contenitore.");
                    moov=true;
                    if(size>256UL*1024*1024) throw new InvalidDataException("Metadati oltre il limite prudenziale di 256 MiB.");
                }
                if(type=="trak"&&parentPath!="moov" || type=="mdia"&&parentPath!="moov/trak")
                    throw new InvalidDataException("Gerarchia della traccia video non valida.");
                if(type=="trak")
                {
                    var childTrack=new TrackMetadata();Walk(data,next,depth+1,"moov/trak",childTrack);
                    video|=childTrack.Video;stillImageTimeKey|=childTrack.Metadata&&!childTrack.Video&&childTrack.StillImageTimeKey;
                }
                else if(type is "moov" or "mdia" || type=="minf"&&parentPath=="moov/trak/mdia" || type=="stbl"&&parentPath=="moov/trak/mdia/minf")
                    Walk(data,next,depth+1,parentPath.Length==0?type:parentPath+"/"+type,track);
                if(type=="hdlr" && parentPath=="moov/trak/mdia" && size>=(ulong)header+12)
                {
                    f.Position=data+8;f.ReadExactly(head.AsSpan(0,4));string handler=Encoding.ASCII.GetString(head,0,4);
                    if(track!=null){track.Video|=handler=="vide";track.Metadata|=handler=="meta";}
                }
                if(type=="stsd"&&parentPath==sampleTable)
                {
                    if(next-data<8)throw new InvalidDataException("Descrizione dei campioni troncata.");
                    f.Position=data;f.ReadExactly(head.AsSpan(0,8));
                    // Unknown versions are not interpreted as evidence about a Photos asset.
                    if(BinaryPrimitives.ReadUInt32BigEndian(head)==0)
                        Walk(data+8,next,depth+1,sampleDescription,track,BinaryPrimitives.ReadUInt32BigEndian(head.AsSpan(4)));
                }
                if(type=="mebx"&&parentPath==sampleDescription)
                {
                    if(next-data<8)throw new InvalidDataException("Descrizione dei metadati troncata.");
                    Walk(data+8,next,depth+1,sampleDescription+"/mebx",track);
                }
                if(type=="keys"&&parentPath==sampleDescription+"/mebx")Walk(data,next,depth+1,timedKeys,track);
                p=next;
            }
            if(expectedCount.HasValue&&childCount!=expectedCount.Value)
                throw new InvalidDataException("Conteggio delle descrizioni dei campioni non valido.");
        }
        try {Walk(0,f.Length,0,"");}
        catch(InvalidDataException ex){return new(false,false,true,extension,ex.Message);}
        catch(EndOfStreamException){return new(false,false,true,extension,"Contenitore troncato.");}
        if(!moov||!mdat||!video)return new(false,false,true,extension,"Non è stato riconosciuto un contenitore MOV/MP4/M4V con traccia video.");
        if(stillImageTimeKey)return new(true,false,false,extension,
            "Chiave strutturata still-image-time in una traccia di metadati: possibile componente Live Photo, conservata nelle risorse aggiuntive. La chiave non prova la presenza di un campione né l'associazione o l'originalità in Foto."){AdditionalResource=true};
        return new(true,false,false,extension,"Contenitore e traccia video riconosciuti; originalità non certificabile tramite USB.");
    }
    private static bool MatchesStillImageTimeKey(FileStream file,long start,long end)
    {
        // QTFF: mebx/keys/<local key ID>/keyd stores namespace + exact key bytes.
        // Key-table presence alone does not establish that a timed value was written.
        // Never search arbitrary payloads, free atoms or content identifiers for this hint.
        ReadOnlySpan<byte> key="mdtacom.apple.quicktime.still-image-time"u8;
        if(end-start!=key.Length)return false;
        Span<byte> value=stackalloc byte[key.Length];file.Position=start;file.ReadExactly(value);
        return value.SequenceEqual(key);
    }
}
