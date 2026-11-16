using System.Buffers.Binary;
using System.Text;

namespace Porto.Core;

// Structural checks only: compressed pixels are not decoded and photographic originality is not certified.
// References: https://www.w3.org/Graphics/JPEG/itu-t81.pdf (Annex B),
// https://www.w3.org/TR/png-3/ (chunk layout/order/CRC), https://nokiatech.github.io/heif/technical.html.
public static class PhotoInspector
{
    public static readonly HashSet<string> SupportedExtensions=new(StringComparer.OrdinalIgnoreCase){".jpg",".jpeg",".png",".heic",".heif"};
    private static readonly HashSet<string> KnownExtensions=new(SupportedExtensions.Concat(new[]{".gif",".webp",".bmp",".tif",".tiff",".dng",".avif",".raw",".cr2",".cr3",".nef",".arw",".orf",".rw2",".raf",".jxl",".jp2",".j2k"}),StringComparer.OrdinalIgnoreCase);
    private const long MaximumFileBytes=512L*1024*1024;
    private const int MaximumStructures=200_000;
    public static bool Candidate(SourceItem item)=>item.Kind==MediaKind.Image||
        item.Kind is MediaKind.Other or MediaKind.Unknown&&KnownExtensions.Contains(Path.GetExtension(item.Name));

    public static MediaInspection Inspect(string path,CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        string extension=".jpg";long trailingBytes=0;
        try
        {
            Require(file.Length is >= 8 and <= MaximumFileBytes,"Foto vuota, troncata o oltre il limite prudenziale di 512 MiB.");
            Span<byte> magic=stackalloc byte[8];file.ReadExactly(magic);file.Position=0;
            if(magic[0]==0xff&&magic[1]==0xd8){extension=".jpg";trailingBytes=CheckJpeg(file,cancel);}
            else if(magic.SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})){extension=".png";CheckPng(file,cancel);}
            else if(magic[4..].SequenceEqual("ftyp"u8)){extension=".heic";CheckHeif(file,cancel);}
            else throw new InvalidDataException("Formato foto non riconosciuto o non supportato. Supportati JPEG, PNG e HEIF con immagini HEVC.");
            string detail=trailingBytes==0?"Struttura della foto verificata; decodifica dei pixel e originalità non certificate."
                :$"Struttura dell'immagine JPEG principale verificata; {trailingBytes} byte aggiuntivi conservati integralmente ma non interpretati né verificati nella struttura. Decodifica dei pixel e originalità non certificate.";
            return new(false,false,false,extension,detail){IsImage=true};
        }
        catch(InvalidDataException error){return new(false,false,true,extension,error.Message);}
        catch(EndOfStreamException){return new(false,false,true,extension,"Foto troncata.");}
        catch(OverflowException){return new(false,false,true,extension,"Dimensioni della foto non valide.");}
    }
    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool valid,string message="Struttura della foto non valida o non supportata.")
    {if(!valid)throw new InvalidDataException(message);}
    private static int Byte(Stream stream)
    {int value=stream.ReadByte();if(value<0)throw new EndOfStreamException();return value;}
    private static int U16(Stream stream)=>(Byte(stream)<<8)|Byte(stream);

    private static long CheckJpeg(FileStream file,CancellationToken cancel)
    {
        Require(U16(file)==0xffd8);int marker=-1,structures=0;bool scanned=false,frame=false;int frameKind=0;
        var components=new Dictionary<int,int>();var quantizers=new HashSet<int>();var tables=new HashSet<int>();
        var scannedComponents=new HashSet<int>();
        while(marker>=0||file.Position<file.Length)
        {
            cancel.ThrowIfCancellationRequested();Require(++structures<=MaximumStructures,"JPEG troppo complesso.");
            if(marker<0){Require(Byte(file)==0xff);do{cancel.ThrowIfCancellationRequested();marker=Byte(file);}while(marker==0xff);}
            if(marker==0xd9)
            {
                Require(frame,"JPEG senza frame immagine prima del marcatore finale.");
                Require(scanned,"JPEG senza scansione immagine prima del marcatore finale.");
                Require(scannedComponents.Count==components.Count,"JPEG con componenti immagine prive di scansione.");
                // EOI terminates the primary image, not necessarily the file: camera metadata,
                // secondary images and Motion Photo payloads may follow it. Preserve all bytes;
                // their interpretation/validation is deliberately not claimed by this inspector.
                return file.Length-file.Position;
            }
            Require(marker is not 0 and not 0xd8 && !(marker is >=0xd0 and <=0xd7));
            int length=U16(file);Require(length>=2&&length-2<=file.Length-file.Position,"Segmento JPEG troncato.");
            byte[] data=new byte[length-2];file.ReadExactly(data);
            if(marker is 0xc0 or 0xc1 or 0xc2)
            {
                Require(!frame&&data.Length>=6&&data[0]==8&&Read16(data,1)>0&&Read16(data,3)>0);
                int count=data[5];Require(count is >=1 and <=4&&data.Length==6+3*count);
                for(int i=0;i<count;i++)
                {
                    int p=6+i*3;Require((data[p+1]>>4) is >=1 and <=4&&(data[p+1]&15) is >=1 and <=4&&data[p+2]<=3);
                    Require(components.TryAdd(data[p],data[p+2]));
                }
                frame=true;frameKind=marker;
            }
            else if(marker==0xdb)
            {
                for(int p=0;p<data.Length;)
                {
                    int spec=data[p++],precision=spec>>4;Require(precision<=1&&(spec&15)<=3);
                    int count=64*(precision+1);Require(count<=data.Length-p);
                    for(int q=0;q<64;q++)Require(precision==0?data[p+q]!=0:Read16(data,p+q*2)!=0);
                    quantizers.Add(spec&15);p+=count;
                }
            }
            else if(marker==0xc4)
            {
                for(int p=0;p<data.Length;)
                {
                    Require(data.Length-p>=17);int spec=data[p++];Require((spec>>4)<=1&&(spec&15)<=3);
                    int count=0,available=1;
                    for(int i=0;i<16;i++){int n=data[p++];count+=n;available=available*2-n;Require(available>=0);}
                    Require(count>0&&count<=256&&count<=data.Length-p);tables.Add(spec);p+=count;
                }
            }
            else if(marker==0xda)
            {
                Require(frame&&data.Length>=6);int count=data[0];Require(count>=1&&count<=components.Count&&data.Length==4+2*count);
                int start=data[^3],end=data[^2];Require(start<=end&&end<=63);
                int high=data[^1]>>4,low=data[^1]&15;Require(high<=13&&low<=13&&(high==0||high==low+1));
                Require(frameKind==0xc2?((start==0&&end==0)||(start>0&&count==1)):start==0&&end==63&&data[^1]==0);
                var scanIds=new HashSet<int>();
                for(int i=0;i<count;i++)
                {
                    int id=data[1+i*2],table=data[2+i*2];Require(scanIds.Add(id)&&components.TryGetValue(id,out int quant)&&quantizers.Contains(quant));
                    Require((table>>4)<=3&&(table&15)<=3);
                    if(start==0)Require(tables.Contains(table>>4));
                    if(end>0)Require(tables.Contains(0x10|(table&15)));
                    scannedComponents.Add(id);
                }
                long entropy=0;marker=-1;
                while(file.Position<file.Length)
                {
                    if((entropy&0xffff)==0)cancel.ThrowIfCancellationRequested();
                    int b=Byte(file);if(b!=0xff){entropy++;continue;}
                    do{cancel.ThrowIfCancellationRequested();b=Byte(file);}while(b==0xff);
                    if(b==0){entropy++;continue;}
                    if(b is >=0xd0 and <=0xd7)continue;
                    marker=b;break;
                }
                Require(entropy>0&&marker>=0,"Dati JPEG assenti o troncati.");scanned=true;continue;
            }
            else if(marker==0xdd)Require(data.Length==2);
            else Require(marker is >=0xe0 and <=0xef or 0xfe,"Profilo o segmento JPEG non supportato.");
            marker=-1;
        }
        throw new InvalidDataException("JPEG senza fine immagine.");
    }
    private static int Read16(byte[] bytes,int offset)=>BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset,2));

    private static readonly uint[] CrcTable=CreateCrcTable();
    private static uint[] CreateCrcTable()
    {
        var result=new uint[256];for(uint i=0;i<256;i++){uint n=i;for(int j=0;j<8;j++)n=(n&1)!=0?(n>>1)^0xedb88320:n>>1;result[i]=n;}return result;
    }
    private static uint Crc(uint crc,ReadOnlySpan<byte> data)
    {foreach(byte b in data)crc=CrcTable[(crc^b)&255]^(crc>>8);return crc;}
    private static void CheckPng(FileStream file,CancellationToken cancel)
    {
        file.Position=8;bool header=false,palette=false,dataSeen=false,dataEnded=false;long dataBytes=0;int chunks=0,color=0,depth=0,interlace=0;
        uint width=0,height=0;List<(long Start,long Length)> imageData=[];
        byte[] buffer=new byte[65536];Span<byte> chunk=stackalloc byte[8];Span<byte> checksum=stackalloc byte[4];
        while(file.Position<file.Length)
        {
            cancel.ThrowIfCancellationRequested();Require(++chunks<=MaximumStructures&&file.Length-file.Position>=12);
            file.ReadExactly(chunk);uint length=BinaryPrimitives.ReadUInt32BigEndian(chunk);Require(length<=int.MaxValue&&length<=file.Length-file.Position-4,"Chunk PNG troncato.");
            for(int i=4;i<8;i++)Require(chunk[i] is >=65 and <=90 or >=97 and <=122);
            Require((chunk[6]&32)==0);string type=Encoding.ASCII.GetString(chunk[4..]);
            Require(header||type=="IHDR","Intestazione PNG assente.");
            long start=file.Position;uint crc=Crc(uint.MaxValue,chunk[4..]);long left=length;
            while(left>0){cancel.ThrowIfCancellationRequested();int n=(int)Math.Min(left,buffer.Length);file.ReadExactly(buffer.AsSpan(0,n));crc=Crc(crc,buffer.AsSpan(0,n));left-=n;}
            file.ReadExactly(checksum);Require(BinaryPrimitives.ReadUInt32BigEndian(checksum)==~crc,"Checksum PNG non valido.");
            if(type=="IHDR")
            {
                Require(!header&&length==13);long end=file.Position;file.Position=start;file.ReadExactly(buffer.AsSpan(0,13));file.Position=end;
                width=BinaryPrimitives.ReadUInt32BigEndian(buffer);height=BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(4));
                Require(width is >0 and <=int.MaxValue&&height is >0 and <=int.MaxValue);
                depth=buffer[8];color=buffer[9];Require(color switch{0=>depth is 1 or 2 or 4 or 8 or 16,2 or 4 or 6=>depth is 8 or 16,3=>depth is 1 or 2 or 4 or 8,_=>false});
                Require(buffer[10]==0&&buffer[11]==0&&buffer[12]<=1);interlace=buffer[12];header=true;
            }
            else if(type=="PLTE"){Require(!palette&&!dataSeen&&color is not 0 and not 4&&length is >=3 and <=768&&length%3==0);Require(color!=3||length/3<=1<<depth);palette=true;}
            else if(type=="IDAT"){Require(!dataEnded&&(color!=3||palette));dataSeen=true;dataBytes+=length;imageData.Add((start,length));}
            else if(type=="IEND")
            {
                Require(length==0&&dataSeen&&dataBytes>=6&&file.Position==file.Length);
                CheckPngRows(file,imageData,width,height,color,depth,interlace,cancel);return;
            }
            else
            {
                if(dataSeen)dataEnded=true;
                Require((chunk[4]&32)!=0,"Chunk PNG critico non supportato.");
                Require(type is not "acTL" and not "fcTL" and not "fdAT","PNG animato non supportato come foto singola.");
            }
        }
        throw new InvalidDataException("PNG senza fine immagine.");
    }

    // Inflate filtered scanlines, without reconstructing pixels. Bounded work rejects decompression bombs.
    private static void CheckPngRows(FileStream file,List<(long Start,long Length)> ranges,uint width,uint height,int color,int depth,int interlace,CancellationToken cancel)
    {
        using var input=new RangeStream(file,ranges);
        int first=input.At(0),second=input.At(1);
        Require((first&15)==8&&(first>>4)<=7&&((first<<8)|second)%31==0&&(second&32)==0,"Intestazione compressa PNG non valida.");
        uint expected=0;for(int i=4;i>0;i--)expected=(expected<<8)|(uint)input.At(input.Length-i);
        int channels=color switch{0 or 3=>1,2=>3,4=>2,6=>4,_=>0};
        (int X,int Y,int Dx,int Dy)[] passes=interlace==0?[(0,0,1,1)]:[(0,0,8,8),(4,0,8,8),(0,4,4,8),(2,0,4,4),(0,2,2,4),(1,0,2,2),(0,1,1,2)];
        List<(long Rows,long Bytes)> layout=[];long total=0;
        foreach(var pass in passes)
        {
            long w=width<=pass.X?0:((long)width-pass.X+pass.Dx-1)/pass.Dx;
            long h=height<=pass.Y?0:((long)height-pass.Y+pass.Dy-1)/pass.Dy;
            if(w==0||h==0)continue;
            long bytes=checked((w*channels*depth+7)/8);total=checked(total+(bytes+1)*h);layout.Add((h,bytes));
        }
        Require(total<=MaximumFileBytes,"PNG decompresso oltre il limite prudenziale di 512 MiB.");
        using var inflated=new System.IO.Compression.ZLibStream(input,System.IO.Compression.CompressionMode.Decompress,true);
        byte[] buffer=new byte[65536];uint a=1,b=0;
        void Adler(ReadOnlySpan<byte> bytes){foreach(byte value in bytes){a=(a+value)%65521;b=(b+a)%65521;}}
        foreach(var pass in layout)for(long row=0;row<pass.Rows;row++)
        {
            cancel.ThrowIfCancellationRequested();int filter=inflated.ReadByte();Require(filter is >=0 and <=4,"Filtro o dati PNG non validi.");
            buffer[0]=(byte)filter;Adler(buffer.AsSpan(0,1));long remaining=pass.Bytes;
            while(remaining>0)
            {
                cancel.ThrowIfCancellationRequested();int n=inflated.Read(buffer,0,(int)Math.Min(remaining,buffer.Length));Require(n>0,"Dati PNG troncati.");Adler(buffer.AsSpan(0,n));remaining-=n;
            }
        }
        Require(inflated.ReadByte()==-1&&expected==((b<<16)|a)&&input.Position==input.Length,"Dimensioni, fine del flusso o checksum dei dati PNG non validi.");
    }
    private sealed class RangeStream(FileStream file,List<(long Start,long Length)> ranges):Stream
    {
        private long position,inRange;
        private int rangeIndex;
        private readonly long length=ranges.Sum(range=>range.Length);
        public int At(long offset)
        {
            foreach(var range in ranges){if(offset<range.Length){file.Position=range.Start+offset;return Byte(file);}offset-=range.Length;}
            throw new EndOfStreamException();
        }
        public override int Read(byte[] buffer,int offset,int count)
        {
            // Inflaters may buffer beyond their end marker. Feed the final 64 bytes one at a
            // time, so their actual EOF is observable; the bulk of large files stays buffered.
            long tail=Math.Max(0,length-64);
            count=position>=tail?Math.Min(count,1):(int)Math.Min(count,tail-position);
            int total=0;
            while(rangeIndex<ranges.Count&&total<count)
            {
                var range=ranges[rangeIndex];
                if(inRange==range.Length){rangeIndex++;inRange=0;continue;}
                int n=(int)Math.Min(count-total,range.Length-inRange);file.Position=range.Start+inRange;
                file.ReadExactly(buffer.AsSpan(offset+total,n));total+=n;position+=n;inRange+=n;
            }
            return total;
        }
        public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;
        public override long Length=>length;
        public override long Position{get=>position;set=>throw new NotSupportedException();}
        public override void Flush(){}public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long length)=>throw new NotSupportedException();public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    }

    private static void CheckHeif(FileStream file,CancellationToken cancel)=>new HeifStructure(file,cancel).Validate();

    private sealed class HeifStructure(FileStream file,CancellationToken cancel)
    {
        private const int MetadataLimit=16*1024*1024;
        private readonly record struct Box(string Type,long Start,long End);
        private sealed record ItemLocation(int Method,ulong Base,List<(ulong Offset,ulong Length)> Extents);
        private readonly Dictionary<uint,string> types=[];
        private readonly Dictionary<uint,ItemLocation> locations=[];
        private readonly Dictionary<uint,List<int>> associations=[];
        private readonly Dictionary<uint,List<uint>> derived=[];
        private readonly List<Box> properties=[];
        private readonly List<Box> media=[];
        private readonly HashSet<uint> validated=[];
        private readonly Dictionary<long,int> configurations=[];
        private readonly Dictionary<uint,(uint Width,uint Height)> imageDimensions=[];
        private Box? itemData;
        private int structureCount,nalCount;
        private uint primary;

        public void Validate()
        {
            var root=Boxes(0,file.Length);Require(root.Count>0&&root[0].Type=="ftyp");
            byte[] brands=Payload(root[0]);Require(brands.Length>=8&&brands.Length%4==0);
            var knownBrands=new HashSet<string>();for(int p=0;p<brands.Length;p+=4)if(p!=4)knownBrands.Add(Encoding.ASCII.GetString(brands,p,4));
            Require(knownBrands.Contains("heic")||knownBrands.Contains("heix"),"Profilo HEIF non supportato: occorre un'immagine HEVC HEIC/HEIX.");
            Require(!knownBrands.Contains("avif")&&!knownBrands.Contains("avis"));
            media.AddRange(root.Where(b=>b.Type=="mdat"));
            var meta=One(root,"meta");Require(meta.End-meta.Start<=MetadataLimit);Header(meta,0);
            var children=Boxes(meta.Start+4,meta.End);
            byte[] handler=Payload(One(children,"hdlr"));Require(handler.Length>=24&&handler[0]==0&&Encoding.ASCII.GetString(handler,8,4)=="pict");
            var pitm=new Data(Payload(One(children,"pitm")));int version=pitm.Full(1);primary=pitm.UInt(version==0?2:4);pitm.End();Require(primary!=0);
            ReadInfo(One(children,"iinf"));ReadLocations(One(children,"iloc"));
            var idats=children.Where(b=>b.Type=="idat").ToList();Require(idats.Count<=1);if(idats.Count==1)itemData=idats[0];
            var iprp=BoxesOf(One(children,"iprp"));properties.AddRange(BoxesOf(One(iprp,"ipco")));
            foreach(var box in iprp.Where(b=>b.Type=="ipma"))ReadAssociations(box);
            foreach(var box in children.Where(b=>b.Type=="iref"))ReadReferences(box);
            Require(types.ContainsKey(primary));
            foreach(var location in locations.Values)foreach(var extent in location.Extents)Extent(location,extent);
            ValidateImage(primary,new HashSet<uint>(),0);
        }
        private List<Box> Boxes(long start,long end)
        {
            List<Box> boxes=[];Span<byte> header=stackalloc byte[16];
            while(start<end)
            {
                cancel.ThrowIfCancellationRequested();Require(++structureCount<=MaximumStructures&&end-start>=8);
                file.Position=start;file.ReadExactly(header[..8]);ulong length=BinaryPrimitives.ReadUInt32BigEndian(header);int headerSize=8;
                string type=Encoding.ASCII.GetString(header.Slice(4,4));
                if(length==1){Require(end-start>=16);file.ReadExactly(header[8..]);length=BinaryPrimitives.ReadUInt64BigEndian(header[8..]);headerSize=16;}
                if(length==0)length=(ulong)(end-start);
                Require(length>=(ulong)headerSize&&length<=(ulong)(end-start),"Box HEIF troncato.");
                long next=checked(start+(long)length);boxes.Add(new(type,start+headerSize,next));start=next;
            }
            return boxes;
        }
        private List<Box> BoxesOf(Box box)=>Boxes(box.Start,box.End);
        private static Box One(List<Box> boxes,string type)
        {var values=boxes.Where(b=>b.Type==type).ToList();Require(values.Count==1,$"Box HEIF {type} assente o duplicato.");return values[0];}
        private byte[] Payload(Box box)
        {Require(box.End-box.Start<=MetadataLimit,"Metadati HEIF troppo grandi.");file.Position=box.Start;byte[] data=new byte[checked((int)(box.End-box.Start))];file.ReadExactly(data);return data;}
        private void Header(Box box,int version)
        {Require(box.End-box.Start>=4);file.Position=box.Start;Require(Byte(file)==version&&Byte(file)==0&&Byte(file)==0&&Byte(file)==0);}
        private void ReadInfo(Box box)
        {
            byte[] bytes=Payload(box);var data=new Data(bytes);int version=data.Full(1);uint count=data.UInt(version==0?2:4);
            var children=Boxes(box.Start+data.Position,box.End);Require(count==children.Count&&count<=100_000);
            foreach(var entry in children)
            {
                Require(entry.Type=="infe");var info=new Data(Payload(entry));int v=info.Full(3,true);Require(v is 2 or 3);
                uint id=info.UInt(v==2?2:4);Require(id!=0&&info.UInt(2)==0,"Foto HEIF protetta o ID non valido.");string type=info.Text(4);
                Require(types.TryAdd(id,type));info.ZeroTerminated();
            }
        }
        private void ReadLocations(Box box)
        {
            var data=new Data(Payload(box));int version=data.Full(2);int sizes=data.Byte(),sizes2=data.Byte();
            int offset=sizes>>4,length=sizes&15,baseSize=sizes2>>4,index=version>0?sizes2&15:0;
            Require(new[]{offset,length,baseSize,index}.All(n=>n is 0 or 4 or 8));
            uint count=data.UInt(version<2?2:4);Require(count<=100_000);
            for(uint i=0;i<count;i++)
            {
                cancel.ThrowIfCancellationRequested();uint id=data.UInt(version<2?2:4);int method=version>0?(int)data.UInt(2):0;
                Require(method is 0 or 1&&data.UInt(2)==0,"Riferimenti HEIF esterni o costruzioni non supportate.");
                ulong baseOffset=data.ULong(baseSize);int extents=(int)data.UInt(2);Require(extents is >0 and <=4096&&length>0);
                List<(ulong,ulong)> ranges=[];for(int e=0;e<extents;e++){if(version>0&&index>0)Require(data.ULong(index)==0);ranges.Add((data.ULong(offset),data.ULong(length)));}
                Require(locations.TryAdd(id,new(method,baseOffset,ranges)));
            }
            data.End();
        }
        private void ReadAssociations(Box box)
        {
            var data=new Data(Payload(box));int version=data.Full(1,true);int wide=data.Flags&1;Require((data.Flags&~1)==0);
            uint count=data.UInt(4);Require(count<=100_000);
            for(uint i=0;i<count;i++)
            {
                uint id=data.UInt(version==0?2:4);int countProperties=data.Byte();List<int> list=[];
                for(int p=0;p<countProperties;p++)
                {
                    int value=(int)data.UInt(wide==0?1:2),mask=wide==0?127:32767,index=value&mask;
                    Require(index<=properties.Count);if(index==0)continue;
                    var property=properties[index-1];
                    if((value&~mask)!=0)Require(property.Type is "ispe" or "hvcC" or "pixi" or "irot" or "imir" or "colr" or "clap" or "auxC","Proprietà HEIF essenziale non supportata.");
                    Require(!list.Contains(index));list.Add(index);
                }
                Require(associations.TryAdd(id,list));
            }
            data.End();
        }
        private void ReadReferences(Box box)
        {
            var data=new Data(Payload(box));int version=data.Full(1);
            foreach(var entry in Boxes(box.Start+4,box.End))
            {
                if(entry.Type!="dimg")continue;
                var reference=new Data(Payload(entry));uint from=reference.UInt(version==0?2:4);int count=(int)reference.UInt(2);Require(count is >0 and <=4096);
                List<uint> to=[];for(int i=0;i<count;i++)to.Add(reference.UInt(version==0?2:4));reference.End();Require(derived.TryAdd(from,to));
            }
        }
        private (long Start,long Length) Extent(ItemLocation location,(ulong Offset,ulong Length) extent)
        {
            ulong relative=checked(location.Base+extent.Offset);Require(extent.Length>0&&extent.Length<=MaximumFileBytes);
            if(location.Method==1)
            {
                Require(itemData.HasValue);var data=itemData!.Value;Require(relative<=(ulong)(data.End-data.Start)&&extent.Length<=(ulong)(data.End-data.Start)-relative);
                return(checked(data.Start+(long)relative),(long)extent.Length);
            }
            Require(relative<=long.MaxValue&&extent.Length<=long.MaxValue-relative);long start=(long)relative,end=start+(long)extent.Length;
            int lo=0,hi=media.Count-1,found=-1;
            while(lo<=hi){int mid=lo+(hi-lo)/2;if(media[mid].Start<=start){found=mid;lo=mid+1;}else hi=mid-1;}
            Require(found>=0&&end<=media[found].End,"Dati HEIF fuori dai contenitori dichiarati.");return(start,(long)extent.Length);
        }
        private void ValidateImage(uint id,HashSet<uint> visiting,int depth)
        {
            if(validated.Contains(id))return;
            cancel.ThrowIfCancellationRequested();Require(depth<=8&&visiting.Add(id),"Derivazione HEIF ciclica o troppo profonda.");
            Require(types.TryGetValue(id,out string? type)&&type is "hvc1" or "grid");
            Require(locations.TryGetValue(id,out var location));Require(associations.TryGetValue(id,out var indexes));
            var associated=indexes!.Select(i=>properties[i-1]).ToList();var (width,height)=Dimensions(id);
            if(type=="hvc1")
            {
                var configuration=One(associated,"hvcC");
                if(!configurations.TryGetValue(configuration.Start,out int lengthSize))
                {lengthSize=CheckConfiguration(Payload(configuration));configurations.Add(configuration.Start,lengthSize);}
                // Common still-image files use a single extent. Multi-extent codestreams are retained as ambiguous.
                Require(location!.Extents.Count==1,"Immagine HEIF con più frammenti non supportata.");
                var extent=Extent(location,location.Extents[0]);long end=extent.Start+extent.Length;file.Position=extent.Start;bool picture=false;
                while(file.Position<end)
                {
                    cancel.ThrowIfCancellationRequested();Require(++nalCount<=MaximumStructures&&end-file.Position>=lengthSize,"Limite complessivo della struttura HEIF raggiunto.");
                    uint length=0;for(int i=0;i<lengthSize;i++)length=(length<<8)|(uint)Byte(file);
                    Require(length>=3&&length<=end-file.Position);long next=file.Position+length;int first=Byte(file),second=Byte(file);Require((first&128)==0&&(second&7)!=0);
                    int nalType=(first>>1)&63;picture|=nalType<=31;file.Position=next;
                }
                Require(picture,"HEIF senza dati di immagine.");
            }
            else
            {
                Require(location!.Extents.Count==1);Require(derived.TryGetValue(id,out var tiles));var extent=Extent(location,location.Extents[0]);
                Require(extent.Length is 8 or 12);file.Position=extent.Start;byte[] grid=new byte[(int)extent.Length];file.ReadExactly(grid);
                Require(grid[0]==0&&(grid[1]&~1)==0);int size=(grid[1]&1)!=0?4:2;Require(grid.Length==4+2*size);
                uint gridWidth=size==2?(uint)Read16(grid,4):BinaryPrimitives.ReadUInt32BigEndian(grid.AsSpan(4));
                uint gridHeight=size==2?(uint)Read16(grid,6):BinaryPrimitives.ReadUInt32BigEndian(grid.AsSpan(8));
                int rows=grid[2]+1,columns=grid[3]+1;
                Require(gridWidth==width&&gridHeight==height&&tiles!.Count==rows*columns);
                var tileDimensions=Dimensions(tiles[0]);
                Require((long)tileDimensions.Width*(columns-1)<width&&width<=(long)tileDimensions.Width*columns&&
                    (long)tileDimensions.Height*(rows-1)<height&&height<=(long)tileDimensions.Height*rows,"Le tessere HEIF non coprono le dimensioni dell'immagine.");
                foreach(uint tile in tiles)
                {Require(types.GetValueOrDefault(tile)=="hvc1"&&Dimensions(tile)==tileDimensions,"Tessere HEIF incompatibili.");ValidateImage(tile,visiting,depth+1);}
            }
            visiting.Remove(id);validated.Add(id);
        }
        private (uint Width,uint Height) Dimensions(uint id)
        {
            if(imageDimensions.TryGetValue(id,out var cached))return cached;
            Require(associations.TryGetValue(id,out var indexes));
            var dimensions=Payload(One(indexes.Select(i=>properties[i-1]).ToList(),"ispe"));
            Require(dimensions.Length==12&&dimensions.Take(4).All(b=>b==0));
            uint width=BinaryPrimitives.ReadUInt32BigEndian(dimensions.AsSpan(4)),height=BinaryPrimitives.ReadUInt32BigEndian(dimensions.AsSpan(8));Require(width>0&&height>0);
            var result=(width,height);imageDimensions.Add(id,result);return result;
        }
        private static int CheckConfiguration(byte[] bytes)
        {
            Require(bytes.Length>=23&&bytes[0]==1);int lengthSize=(bytes[21]&3)+1;Require(lengthSize!=3);
            int count=bytes[22],p=23;HashSet<int> sets=[];
            for(int i=0;i<count;i++)
            {
                Require(bytes.Length-p>=3);int type=bytes[p++]&63,nals=Read16(bytes,p);p+=2;Require(nals>0);
                for(int n=0;n<nals;n++)
                {
                    Require(bytes.Length-p>=2);int length=Read16(bytes,p);p+=2;Require(length>=3&&length<=bytes.Length-p&&(bytes[p]&128)==0&&(bytes[p+1]&7)!=0&&((bytes[p]>>1)&63)==type);p+=length;
                }
                sets.Add(type);
            }
            Require(p==bytes.Length&&sets.Contains(32)&&sets.Contains(33)&&sets.Contains(34),"Configurazione HEVC incompleta.");return lengthSize;
        }
        private sealed class Data(byte[] bytes)
        {
            public int Position {get;private set;}
            public int Flags {get;private set;}
            public int Byte(){Require(Position<bytes.Length,"Metadati HEIF troncati.");return bytes[Position++];}
            public uint UInt(int size){Require(size<=4);return(uint)ULong(size);}
            public ulong ULong(int size){Require(size<=8);ulong n=0;for(int i=0;i<size;i++)n=(n<<8)|(uint)Byte();return n;}
            public int Full(int maximumVersion,bool flagsAllowed=false){int version=Byte();Flags=(int)UInt(3);Require(version<=maximumVersion&&(flagsAllowed||Flags==0));return version;}
            public string Text(int count){Require(count<=bytes.Length-Position);string value=Encoding.ASCII.GetString(bytes,Position,count);Position+=count;return value;}
            public void ZeroTerminated(){while(Byte()!=0){} }
            public void End()=>Require(Position==bytes.Length,"Metadati HEIF con lunghezza incoerente.");
        }
    }
}
