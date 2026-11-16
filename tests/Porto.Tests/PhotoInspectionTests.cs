using System.Buffers.Binary;
using System.Text;
using Porto.Core;

internal static class PhotoInspectionTests
{
    public static void Add(List<(string,Action)> tests)
    {
        foreach(var sample in new[]{(Name:"JPEG",Bytes:Jpeg(),Extension:".jpg"),(Name:"PNG",Bytes:Png(),Extension:".png"),(Name:"HEIC",Bytes:Heic(),Extension:".heic")})
        {
            tests.Add(($"photo valid {sample.Name} structure is recognized without changing bytes",()=>
            {
                string path=Write(sample.Bytes);var result=PhotoInspector.Inspect(path,default);
                Fixtures.Check(result.IsImage&&result.IsSupportedMedia&&!result.IsVideo&&!result.Ambiguous,result.Detail);
                Fixtures.Check(result.Extension==sample.Extension&&File.ReadAllBytes(path).SequenceEqual(sample.Bytes));
                Fixtures.Check(result.Detail.Contains("decodifica",StringComparison.OrdinalIgnoreCase),"Structural checking must disclose its limit.");
            }));
            tests.Add(($"photo truncated {sample.Name} cannot pass",()=>
            {
                foreach(int length in new[]{0,1,8,sample.Bytes.Length/2,sample.Bytes.Length-1}.Distinct())
                {
                    var result=PhotoInspector.Inspect(Write(sample.Bytes[..length]),default);
                    Fixtures.Check(result.Ambiguous&&!result.IsSupportedMedia,$"{sample.Name} accepted {length} bytes.");
                }
            }));
        }
        tests.Add(("photo wrong magic and unsupported formats remain ambiguous",()=>
        {
            foreach(byte[] bytes in new[]{Encoding.ASCII.GetBytes("GIF89a1234567890"),Encoding.ASCII.GetBytes("RIFF1234WEBPdata"),Fixtures.Video(),new byte[30]})
            {
                var result=PhotoInspector.Inspect(Write(bytes),default);
                Fixtures.Check(result.Ambiguous&&!result.IsSupportedMedia);
            }
        }));
        tests.Add(("photo PNG rejects bad checksum lengths ordering and dimensions",()=>
        {
            byte[] crc=Png();crc[^1]^=1;
            byte[] length=Png();BinaryPrimitives.WriteUInt32BigEndian(length.AsSpan(8),uint.MaxValue);
            byte[] zero=Png(width:0);
            byte[] missingData=Png(withData:false);
            byte[] trailing=Png().Concat(new byte[]{0}).ToArray();
            foreach(var bytes in new[]{crc,length,zero,missingData,trailing})
                Fixtures.Check(PhotoInspector.Inspect(Write(bytes),default).Ambiguous);
        }));
        tests.Add(("photo PNG validates compressed scanline size filter and checksum",()=>
        {
            foreach(byte[] compressed in new[]{new byte[]{0x78,0x9c,0,0,0,0},Compress([5,0]),Compress([0]),Compress([0,0,0])})
                Fixtures.Check(PhotoInspector.Inspect(Write(Png(compressed:compressed)),default).Ambiguous);
            byte[] badAdler=Compress([0,0]);badAdler[^1]^=1;
            Fixtures.Check(PhotoInspector.Inspect(Write(Png(compressed:badAdler)),default).Ambiguous);
        }));
        tests.Add(("photo PNG rejects trailing compressed junk even with duplicated Adler checksum",()=>
        {
            byte[] valid=Compress([0,0]);
            foreach(int length in new[]{1,100,100000})
            {
                byte[] extra=valid.Concat(new byte[length]).Concat(valid[^4..]).ToArray();
                Fixtures.Check(PhotoInspector.Inspect(Write(Png(compressed:extra)),default).Ambiguous,"Trailing compressed bytes were accepted.");
            }
        }));
        tests.Add(("photo HEIC image grid resolves and validates its tile data",()=>
        {
            var valid=PhotoInspector.Inspect(Write(Heic(grid:true)),default);
            Fixtures.Check(valid.IsImage&&!valid.Ambiguous,valid.Detail);
            Fixtures.Check(PhotoInspector.Inspect(Write(Heic(grid:true,offsetDelta:1)),default).Ambiguous);
            Fixtures.Check(PhotoInspector.Inspect(Write(Heic(grid:true,gridWidth:2)),default).Ambiguous,"One 1px tile cannot cover a 2px grid.");
        }));
        tests.Add(("photo HEIC repeated tile extents share a cumulative parsing budget",()=>
        {
            // A small file can otherwise multiply 800 NAL records by 256 distinct tile IDs.
            byte[] bytes=HeicRepeatedTiles();Fixtures.Check(bytes.Length<100_000);
            Fixtures.Check(PhotoInspector.Inspect(Write(bytes),default).Ambiguous,"HEIF work exceeded the cumulative structural budget.");
        }));
        tests.Add(("photo JPEG requires frame scan tables entropy and exact ending",()=>
        {
            byte[] invalidLength=Jpeg();invalidLength[4]=0;invalidLength[5]=1;
            byte[] missingFrame=[0xff,0xd8,0xff,0xda,0,8,1,1,0,0,63,0,0x3f,0xff,0xd9];
            byte[] missingScan=[0xff,0xd8,0xff,0xd9];
            foreach(var bytes in new[]{invalidLength,missingFrame,missingScan,Jpeg().Concat(new byte[]{9}).ToArray()})
                Fixtures.Check(PhotoInspector.Inspect(Write(bytes),default).Ambiguous);
        }));
        tests.Add(("photo HEIC rejects bogus brands primary extents config and absent image metadata",()=>
        {
            foreach(var bytes in new[]{Heic(offsetDelta:1),Heic(primary:9),Heic(withConfig:false),Heic(brand:"avif"),
                Fixtures.Atom("ftyp",Encoding.ASCII.GetBytes("heic\0\0\0\0mif1")).Concat(Fixtures.Atom("mdat",new byte[]{1,2,3})).ToArray()})
                Fixtures.Check(PhotoInspector.Inspect(Write(bytes),default).Ambiguous);
        }));
        tests.Add(("photo inspection observes cancellation",()=>
            Fixtures.Throws<OperationCanceledException>(()=>PhotoInspector.Inspect(Write(Png()),new CancellationToken(true)))));
        tests.Add(("photo media selection keeps unsupported pictures visible and separates photo video modes",()=>
        {
            foreach(string name in new[]{"image.JPG","image.heic","image.gif","image.webp","image.dng","image.avif","image.raw"})
            {
                var item=new SourceItem(name,"root",name,MediaKind.Unknown);
                Fixtures.Check(MediaSelectionRules.Candidate(item,ImportMedia.Photos));
                Fixtures.Check(MediaSelectionRules.Candidate(item,ImportMedia.PhotosAndVideos));
                Fixtures.Check(!MediaSelectionRules.Candidate(item,ImportMedia.Videos));
            }
            var video=new SourceItem("v","root","v.MOV",MediaKind.Video);
            Fixtures.Check(!MediaSelectionRules.Candidate(video,ImportMedia.Photos)&&MediaSelectionRules.Candidate(video,ImportMedia.Videos));
            Fixtures.Check(!MediaSelectionRules.Candidate(new("d","root","folder.jpg",MediaKind.Container),ImportMedia.Photos));
            Fixtures.Check(MediaSelectionRules.Inspect(Write(Jpeg()),new("p","r","p.jpg",MediaKind.Image),default).IsImage);
            Fixtures.Check(MediaSelectionRules.Inspect(Write(Fixtures.Video()),video,default).IsVideo);
            Fixtures.Check(!MediaSelectionRules.SupportedExtensions.Contains(".gif")&&MediaSelectionRules.SupportedExtensions.Contains(".mp4"));
        }));
    }
    private static string Write(byte[] bytes)
    {string path=Path.Combine(Fixtures.Root(),"synthetic.part");File.WriteAllBytes(path,bytes);return path;}

    // A one-pixel grayscale JPEG: one DC zero code and one AC end-of-block code.
    internal static byte[] Jpeg()
    {
        byte[] Segment(byte marker,byte[] payload)=>new byte[]{0xff,marker,(byte)((payload.Length+2)>>8),(byte)(payload.Length+2)}.Concat(payload).ToArray();
        byte[] table=new byte[18];table[1]=1;
        byte[] ac=(byte[])table.Clone();ac[0]=0x10;
        return new byte[]{0xff,0xd8}.Concat(Segment(0xdb,new byte[]{0}.Concat(Enumerable.Repeat((byte)1,64)).ToArray()))
            .Concat(Segment(0xc0,[8,0,1,0,1,1,1,0x11,0]))
            .Concat(Segment(0xc4,table.Concat(ac).ToArray()))
            .Concat(Segment(0xda,[1,1,0,0,63,0])).Concat(new byte[]{0x3f,0xff,0xd9}).ToArray();
    }
    internal static byte[] Png(uint width=1,bool withData=true,byte[]? compressed=null)
    {
        byte[] header=new byte[13];BinaryPrimitives.WriteUInt32BigEndian(header,width);BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4),1);header[8]=8;header[9]=0;
        // zlib stream for filter byte 0 and grayscale pixel 0.
        byte[] data=compressed??[0x78,0x9c,0x63,0x60,0,0,0,2,0,1];
        return new byte[]{137,80,78,71,13,10,26,10}.Concat(Chunk("IHDR",header))
            .Concat(withData?Chunk("IDAT",data):[]).Concat(Chunk("IEND",[])).ToArray();
    }
    private static byte[] Compress(byte[] data)
    {using var output=new MemoryStream();using(var compressed=new System.IO.Compression.ZLibStream(output,System.IO.Compression.CompressionLevel.SmallestSize,true))compressed.Write(data);return output.ToArray();}
    private static byte[] Chunk(string type,byte[] bytes)
    {
        byte[] output=new byte[bytes.Length+12];BinaryPrimitives.WriteUInt32BigEndian(output,(uint)bytes.Length);
        Encoding.ASCII.GetBytes(type).CopyTo(output,4);bytes.CopyTo(output,8);uint crc=uint.MaxValue;
        foreach(byte b in output.AsSpan(4,bytes.Length+4)){crc^=b;for(int i=0;i<8;i++)crc=(crc&1)!=0?(crc>>1)^0xedb88320:crc>>1;}
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(output.Length-4),~crc);return output;
    }
    internal static byte[] Heic(int offsetDelta=0,ushort primary=1,bool withConfig=true,string brand="heic",bool grid=false,uint gridWidth=1)
    {
        byte[] Full(string type,params byte[][] data)=>Fixtures.Atom(type,new byte[4],data.SelectMany(x=>x).ToArray());
        byte[] U16(ushort n){byte[] b=new byte[2];BinaryPrimitives.WriteUInt16BigEndian(b,n);return b;}
        byte[] U32(uint n){byte[] b=new byte[4];BinaryPrimitives.WriteUInt32BigEndian(b,n);return b;}
        byte[] ftyp=Fixtures.Atom("ftyp",Encoding.ASCII.GetBytes(brand+"\0\0\0\0mif1"));
        byte[] infe=Fixtures.Atom("infe",new byte[]{2,0,0,0},U16(1),U16(0),Encoding.ASCII.GetBytes("hvc1image\0"));
        byte[] config=new byte[23];config[0]=1;config[21]=3;config[22]=3;
        foreach(byte type in new byte[]{32,33,34})config=config.Concat(new byte[]{type,0,1,0,3,(byte)(type<<1),1,0x80}).ToArray();
        byte[] ipco=Fixtures.Atom("ipco",Full("ispe",U32(1),U32(1)),withConfig?Fixtures.Atom("hvcC",config):Fixtures.Atom("free",new byte[]{0}),grid?Full("ispe",U32(gridWidth),U32(1)):[]);
        byte[] ipma=Full("ipma",U32(grid?2u:1u),U16(1),new byte[]{2,1,0x82},grid?U16(2).Concat(new byte[]{1,3}).ToArray():[]);
        byte[] payload=[0,0,0,3,0x26,1,0x80]; // Length-prefixed HEVC intra NAL structure, no pixel-decoding claim.
        byte[] gridData=[0,0,0,0,0,1,0,1];
        BinaryPrimitives.WriteUInt16BigEndian(gridData.AsSpan(4),(ushort)gridWidth);
        byte[] gridInfo=Fixtures.Atom("infe",new byte[]{2,0,0,0},U16(2),U16(0),Encoding.ASCII.GetBytes("gridimage\0"));
        byte[] Meta(uint offset)=>Full("meta",Full("hdlr",new byte[4],Encoding.ASCII.GetBytes("pict"),new byte[12],new byte[]{0}),
            Full("pitm",U16(grid?(ushort)2:primary)),Full("iinf",U16(grid?(ushort)2:(ushort)1),infe,grid?gridInfo:[]),
            Full("iloc",new byte[]{0x44,0},U16(grid?(ushort)2:(ushort)1),U16(1),U16(0),U16(1),U32(offset),U32((uint)payload.Length),
                grid?U16(2).Concat(U16(0)).Concat(U16(1)).Concat(U32(offset+(uint)payload.Length)).Concat(U32((uint)gridData.Length)).ToArray():[]),
            Fixtures.Atom("iprp",ipco,ipma),grid?Full("iref",Fixtures.Atom("dimg",U16(2),U16(1),U16(1))):[]);
        byte[] meta=Meta(0);meta=Meta((uint)(ftyp.Length+meta.Length+8+offsetDelta));
        return ftyp.Concat(meta).Concat(Fixtures.Atom("mdat",payload,grid?gridData:[])).ToArray();
    }
    private static byte[] HeicRepeatedTiles()
    {
        const ushort count=256,primary=count+1;
        byte[] Full(string type,params byte[][] data)=>Fixtures.Atom(type,new byte[4],data.SelectMany(x=>x).ToArray());
        byte[] U16(ushort n){byte[] b=new byte[2];BinaryPrimitives.WriteUInt16BigEndian(b,n);return b;}
        byte[] U32(uint n){byte[] b=new byte[4];BinaryPrimitives.WriteUInt32BigEndian(b,n);return b;}
        byte[] ftyp=Fixtures.Atom("ftyp",Encoding.ASCII.GetBytes("heic\0\0\0\0mif1"));
        byte[] payload=Enumerable.Range(0,800).SelectMany(_=>new byte[]{0,0,0,3,0x26,1,0x80}).ToArray();
        byte[] grid=[0,0,0,255,1,0,0,1];
        byte[] config=new byte[23];config[0]=1;config[21]=3;config[22]=3;
        foreach(byte type in new byte[]{32,33,34})config=config.Concat(new byte[]{type,0,1,0,3,(byte)(type<<1),1,0x80}).ToArray();
        byte[] ipco=Fixtures.Atom("ipco",Full("ispe",U32(1),U32(1)),Fixtures.Atom("hvcC",config),Full("ispe",U32(count),U32(1)));
        byte[] info=Enumerable.Range(1,primary).SelectMany(id=>Fixtures.Atom("infe",new byte[]{2,0,0,0},U16((ushort)id),U16(0),Encoding.ASCII.GetBytes(id==primary?"gridimage\0":"hvc1image\0"))).ToArray();
        byte[] associations=Enumerable.Range(1,primary).SelectMany(id=>U16((ushort)id).Concat(id==primary?new byte[]{1,3}:new byte[]{2,1,0x82})).ToArray();
        byte[] references=Full("iref",Fixtures.Atom("dimg",U16(primary),U16(count),Enumerable.Range(1,count).SelectMany(id=>U16((ushort)id)).ToArray()));
        byte[] Meta(uint offset)
        {
            byte[] locations=Enumerable.Range(1,primary).SelectMany(id=>U16((ushort)id).Concat(U16(0)).Concat(U16(1))
                .Concat(U32(offset+(id==primary?(uint)payload.Length:0))).Concat(U32(id==primary?(uint)grid.Length:(uint)payload.Length))).ToArray();
            return Full("meta",Full("hdlr",new byte[4],Encoding.ASCII.GetBytes("pict"),new byte[12],new byte[]{0}),Full("pitm",U16(primary)),
                Full("iinf",U16(primary),info),Full("iloc",new byte[]{0x44,0},U16(primary),locations),
                Fixtures.Atom("iprp",ipco,Full("ipma",U32(primary),associations)),references);
        }
        byte[] meta=Meta(0);meta=Meta((uint)(ftyp.Length+meta.Length+8));
        return ftyp.Concat(meta).Concat(Fixtures.Atom("mdat",payload,grid)).ToArray();
    }
}
