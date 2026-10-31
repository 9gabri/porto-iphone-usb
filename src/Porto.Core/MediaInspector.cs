using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;

namespace Porto.Core;

public sealed record MediaInspection(bool IsVideo,bool LivePhoto,bool Ambiguous,string Extension,string Detail);
public static partial class MediaInspector
{
    public static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase){".mov",".mp4",".m4v"};
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase){".jpg",".jpeg",".heic",".heif",".png",".dng"};
    public static bool Candidate(SourceItem item) => item.Kind==MediaKind.Video ||
        (item.Kind is MediaKind.Other or MediaKind.Unknown && (VideoExtensions.Contains(Path.GetExtension(item.Name)) ||
        item.Format?.StartsWith("b9820000",StringComparison.OrdinalIgnoreCase)==true || item.Format?.StartsWith("300b0000",StringComparison.OrdinalIgnoreCase)==true));

    public static Dictionary<string,Outcome> SuspectedCompanions(IEnumerable<SourceItem> items)
    {
        var photos = new HashSet<string>(items.Where(i=>i.Kind==MediaKind.Image||ImageExtensions.Contains(Path.GetExtension(i.Name)))
            .Select(Key),StringComparer.OrdinalIgnoreCase);
        Dictionary<string,Outcome> result=[];
        foreach(var i in items.Where(Candidate))
            if(photos.Contains(Key(i)) || EditedName().IsMatch(i.Name)) result[i.Id]=Outcome.Ambiguous;
        return result;
    }
    private static string Key(SourceItem item)=>item.ParentId+"/"+Path.GetFileNameWithoutExtension(item.Name);
    [GeneratedRegex(@"^IMG_E\d+",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)] private static partial Regex EditedName();

    public static MediaInspection Inspect(string path,CancellationToken cancel)
    {
        using var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        bool video=false,mdat=false,moov=false,live=false,identifier=false;
        string extension=".mp4"; int atoms=0;
        void Walk(long start,long end,int depth,string parentPath)
        {
            if(depth>20) throw new InvalidDataException("Struttura video troppo complessa.");
            byte[] head=new byte[16];
            for(long p=start;p<end;)
            {
                cancel.ThrowIfCancellationRequested();
                if(++atoms>200000 || end-p<8) throw new InvalidDataException("Struttura contenitore incompleta.");
                f.Position=p; f.ReadExactly(head.AsSpan(0,8));
                ulong size=BinaryPrimitives.ReadUInt32BigEndian(head); int header=8;
                string type=Encoding.ASCII.GetString(head,4,4);
                if(size==1){f.ReadExactly(head.AsSpan(8,8));size=BinaryPrimitives.ReadUInt64BigEndian(head.AsSpan(8));header=16;}
                if(size==0)size=(ulong)(end-p);
                if(size<(ulong)header || size>(ulong)(end-p)) throw new InvalidDataException("Dimensioni contenitore non valide.");
                long next=checked(p+(long)size),data=p+header;
                if(type=="ftyp" && size>=(ulong)header+8){f.Position=data;f.ReadExactly(head.AsSpan(0,4)); string brand=Encoding.ASCII.GetString(head,0,4); if(brand=="qt  ")extension=".mov";if(brand=="M4V ")extension=".m4v";}
                if(type=="mdat" && parentPath=="" && size>(ulong)header)mdat=true;
                if(type=="moov")
                {
                    if(parentPath!="")throw new InvalidDataException("Atomo moov fuori dalla radice del contenitore.");
                    moov=true;
                    if(size>256UL*1024*1024) throw new InvalidDataException("Metadati oltre il limite prudenziale di 256 MiB.");
                    var marks=ScanMetadata(f,data,next,cancel);live|=marks.Live;identifier|=marks.Identifier;
                }
                if(type=="trak"&&parentPath!="moov" || type=="mdia"&&parentPath!="moov/trak")
                    throw new InvalidDataException("Gerarchia della traccia video non valida.");
                if(type is "moov" or "trak" or "mdia") Walk(data,next,depth+1,parentPath.Length==0?type:parentPath+"/"+type);
                if(type=="hdlr" && parentPath=="moov/trak/mdia" && size>=(ulong)header+12)
                {f.Position=data+8;f.ReadExactly(head.AsSpan(0,4));video|=Encoding.ASCII.GetString(head,0,4)=="vide";}
                p=next;
            }
        }
        try {Walk(0,f.Length,0,"");}
        catch(InvalidDataException ex){return new(false,false,true,extension,ex.Message);}
        catch(EndOfStreamException){return new(false,false,true,extension,"Contenitore troncato.");}
        if(!moov||!mdat||!video)return new(false,false,true,extension,"Non è stato riconosciuto un contenitore MOV/MP4/M4V con traccia video.");
        if(live)return new(true,false,true,extension,"Indicatore still-image-time: possibile Live Photo, esclusa dall'importazione automatica. Associazione Foto non certificabile.");
        if(identifier)return new(true,false,true,extension,"Identificatore di contenuto Apple senza classificazione Live Photo certa.");
        return new(true,false,false,extension,"Contenitore e traccia video riconosciuti; originalità non certificabile tramite USB.");
    }
    private static (bool Live,bool Identifier) ScanMetadata(FileStream f,long start,long end,CancellationToken cancel)
    {
        byte[] bytes=new byte[64*1024];string tail="";bool live=false,id=false;f.Position=start;
        while(f.Position<end)
        {
            cancel.ThrowIfCancellationRequested();int n=f.Read(bytes,0,(int)Math.Min(bytes.Length,end-f.Position));if(n==0)throw new EndOfStreamException();
            string chunk=tail+Encoding.Latin1.GetString(bytes,0,n);
            live|=chunk.Contains("com.apple.quicktime.still-image-time",StringComparison.Ordinal);
            id|=chunk.Contains("com.apple.quicktime.content.identifier",StringComparison.Ordinal);
            tail=chunk[^Math.Min(100,chunk.Length)..];
        }
        return(live,id);
    }
}
