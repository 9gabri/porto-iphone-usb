using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Porto.Core;
using static Fixtures;

internal static class Rc8MediaTests
{
    private const string StillImageTime="com.apple.quicktime.still-image-time";
    private const string ContentIdentifier="com.apple.quicktime.content.identifier";

    public static void Add(List<(string,Action)> tests)
    {
        foreach(string marker in new[]{StillImageTime,ContentIdentifier})
            tests.Add(($"rc8 media arbitrary free text is not metadata: {marker}",()=>
            {
                var result=InspectBytes(Video(marker:marker));
                Fixtures.Check(result is {IsVideo:true,Ambiguous:false,AdditionalResource:false,LivePhoto:false});
            }));
        tests.Add(("rc8 media structured still-image-time is only an additional-resource hint",()=>
        {
            var result=InspectBytes(TimedVideo());
            Fixtures.Check(result is {IsVideo:true,Ambiguous:false,AdditionalResource:true,LivePhoto:false});
            Fixtures.Check(result.Detail.Contains("non prova",StringComparison.Ordinal)&&result.Detail.Contains("campione",StringComparison.Ordinal));
        }));
        foreach(string localKey in new[]{"free","moov","\0\0\0\x02"})
            tests.Add(($"rc8 media local timed key ID is not an atom semantic: {Convert.ToHexString(Encoding.ASCII.GetBytes(localKey))}",()=>
                Fixtures.Check(InspectBytes(TimedVideo(localKey:localKey)) is {IsVideo:true,AdditionalResource:true})));
        foreach(var sample in new[]{
            (Label:"content identifier",Key:ContentIdentifier,Namespace:"mdta",Handler:"meta",LocalKey:"\0\0\0\x01"),
            (Label:"wrong namespace",Key:StillImageTime,Namespace:"udta",Handler:"meta",LocalKey:"\0\0\0\x01"),
            (Label:"unrelated key prefix",Key:StillImageTime+".unrelated",Namespace:"mdta",Handler:"meta",LocalKey:"\0\0\0\x01"),
            (Label:"audio track",Key:StillImageTime,Namespace:"mdta",Handler:"soun",LocalKey:"\0\0\0\x01"),
            (Label:"video track",Key:StillImageTime,Namespace:"mdta",Handler:"vide",LocalKey:"\0\0\0\x01"),
            (Label:"unused local key",Key:StillImageTime,Namespace:"mdta",Handler:"meta",LocalKey:"\0\0\0\0")})
            tests.Add(($"rc8 media no extra classification for {sample.Label}",()=>
                Fixtures.Check(InspectBytes(TimedVideo(sample.Key,sample.Namespace,sample.Handler,sample.LocalKey)) is {IsVideo:true,Ambiguous:false,AdditionalResource:false})));
        tests.Add(("rc8 media static content identifier alone stays eligible",()=>
        {
            // File-level keys use a full atom and key count, unlike a mebx keys table.
            byte[] keys=Atom("keys",new byte[4],UInt32(1),Atom("mdta",Encoding.UTF8.GetBytes(ContentIdentifier)));
            byte[] values=Atom("ilst",Atom("\0\0\0\x01",Atom("data",UInt32(1),new byte[4],Encoding.UTF8.GetBytes("synthetic-id"))));
            var result=InspectBytes(Container(Atom("meta",Atom("hdlr",Handler("mdta")),keys,values)));
            Fixtures.Check(result is {IsVideo:true,Ambiguous:false,AdditionalResource:false,LivePhoto:false});
        }));
        tests.Add(("rc8 media nested free payload cannot impersonate a metadata track",()=>
            Fixtures.Check(InspectBytes(Container(Atom("free",TimedTrack()))) is {IsVideo:true,Ambiguous:false,AdditionalResource:false})));
        tests.Add(("rc8 media misplaced key declaration is not timed metadata",()=>
            Fixtures.Check(InspectBytes(Container(Atom("keyd",Encoding.UTF8.GetBytes("mdta"+StillImageTime)))) is {IsVideo:true,Ambiguous:false,AdditionalResource:false})));
        tests.Add(("rc8 media handler order does not change structured hint",()=>
            Fixtures.Check(InspectBytes(Container(Atom("trak",Atom("mdia",TimedMinf(),Atom("hdlr",Handler("meta")))))) is {IsVideo:true,AdditionalResource:true})));
        tests.Add(("rc8 media documented sample description zero padding stays valid",()=>
        {
            byte[] mebx=Atom("mebx",new byte[]{0,0,0,0,0,0,0,1},
                Atom("keys",Atom("\0\0\0\x01",Atom("keyd",Encoding.UTF8.GetBytes("mdta"+StillImageTime)))),new byte[4]);
            byte[] track=Atom("trak",Atom("mdia",Atom("hdlr",Handler("meta")),Atom("minf",Atom("stbl",Atom("stsd",new byte[4],UInt32(1),mebx)))));
            Fixtures.Check(InspectBytes(Container(track)) is {IsVideo:true,Ambiguous:false,AdditionalResource:true});
        }));
        tests.Add(("rc8 media inspection leaves synthetic bytes unchanged",()=>
        {
            string path=Path.Combine(Root(),"synthetic.mov");byte[] bytes=TimedVideo();File.WriteAllBytes(path,bytes);
            byte[] before=SHA256.HashData(bytes);MediaInspector.Inspect(path,default);
            Fixtures.Check(new FileInfo(path).Length==bytes.Length&&SHA256.HashData(File.ReadAllBytes(path)).SequenceEqual(before));
        }));
        tests.Add(("rc8 media name and photo evidence is a copy hint, never a pre-read rejection",()=>
        {
            List<SourceItem> items=[
                new("paired","dcim","IMG_1234.MOV",MediaKind.Video,100),
                new("photo","dcim","IMG_1234.HEIC",MediaKind.Image,50),
                new("edited","dcim","IMG_E1234.MOV",MediaKind.Video,100),
                new("prefix-only","dcim","IMG_E1234_holiday.MOV",MediaKind.Video,100),
                new("elsewhere","other","IMG_1234.MOV",MediaKind.Video,100),
                new("video-image-name","dcim","IMG_5678.HEIC",MediaKind.Video,100),
                new("conflicting","dcim","IMG_5678.MOV",MediaKind.Video,100),
                new("format-video-image-name","dcim","IMG_6789.HEIC",MediaKind.Unknown,100,Format:"b9820000"),
                new("format-conflicting","dcim","IMG_6789.MOV",MediaKind.Video,100)];
            var hints=MediaInspector.SuspectedCompanions(items);
            Fixtures.Check(hints.Count==2&&hints.ContainsKey("paired")&&hints.ContainsKey("edited"));
            Fixtures.Check(hints.Values.All(v=>v.Contains("dopo la verifica",StringComparison.Ordinal)&&!v.Contains("non importato",StringComparison.Ordinal)));
            Fixtures.Check(InspectBytes(Video(),"IMG_1234.MOV") is {IsVideo:true,Ambiguous:false});
            Fixtures.Check(InspectBytes(Video(),"IMG_E1234.MOV") is {IsVideo:true,Ambiguous:false});
        }));
        tests.Add(("rc8 media malformed and truncated metadata never publishes as verified video",()=>
        {
            byte[] truncatedHeader=new byte[8];UInt32(1).CopyTo(truncatedHeader,0);Encoding.ASCII.GetBytes("mebx").CopyTo(truncatedHeader,4);
            byte[] oversizedAtom=new byte[16];UInt32(1).CopyTo(oversizedAtom,0);Encoding.ASCII.GetBytes("mebx").CopyTo(oversizedAtom,4);
            BinaryPrimitives.WriteUInt64BigEndian(oversizedAtom.AsSpan(8),ulong.MaxValue);
            foreach(byte[] description in new[]{
                Atom("stsd",new byte[7]),
                Atom("stsd",new byte[4],UInt32(1),truncatedHeader),
                Atom("stsd",new byte[4],UInt32(1),oversizedAtom),
                Atom("stsd",new byte[4],UInt32(uint.MaxValue),Atom("mebx",new byte[8])),
                Atom("stsd",new byte[4],UInt32(1),Atom("mebx",new byte[8],Atom("keys",Atom("\0\0\0\x01",new byte[5]))))})
            {
                var result=InspectBytes(Container(Atom("trak",Atom("mdia",Atom("hdlr",Handler("meta")),Atom("minf",Atom("stbl",description))))));
                Fixtures.Check(result is {IsVideo:false,Ambiguous:true,AdditionalResource:false},"Invalid metadata boundaries must not be treated as a valid video.");
            }
        }));
        tests.Add(("rc8 media large free payload does not provide metadata evidence",()=>
        {
            byte[] payload=new byte[2*1024*1024];Encoding.UTF8.GetBytes(StillImageTime).CopyTo(payload,payload.Length/2);
            Fixtures.Check(InspectBytes(Container(Atom("free",payload))) is {IsVideo:true,Ambiguous:false,AdditionalResource:false});
        }));
        tests.Add(("rc8 media cancellation remains observable",()=>
        {
            string path=Path.Combine(Root(),"synthetic.mov");File.WriteAllBytes(path,TimedVideo());
            Fixtures.Throws<OperationCanceledException>(()=>MediaInspector.Inspect(path,new CancellationToken(true)));
        }));
    }

    public static byte[] TimedVideo(string key=StillImageTime,string keyNamespace="mdta",string handler="meta",string localKey="\0\0\0\x01")=>
        Container(TimedTrack(key,keyNamespace,handler,localKey));
    private static byte[] TimedTrack(string key=StillImageTime,string keyNamespace="mdta",string handler="meta",string localKey="\0\0\0\x01")=>
        Atom("trak",Atom("mdia",Atom("hdlr",Handler(handler)),TimedMinf(key,keyNamespace,localKey)));
    private static byte[] TimedMinf(string key=StillImageTime,string keyNamespace="mdta",string localKey="\0\0\0\x01")=>
        Atom("minf",Atom("stbl",Atom("stsd",new byte[4],UInt32(1),
            Atom("mebx",new byte[]{0,0,0,0,0,0,0,1},Atom("keys",Atom(localKey,Atom("keyd",Encoding.UTF8.GetBytes(keyNamespace+key))))))));
    private static byte[] Container(params byte[][] metadata)=>Atom("ftyp",Encoding.ASCII.GetBytes("qt  \0\0\0\0qt  "))
        .Concat(Atom("moov",new[]{Atom("trak",Atom("mdia",Atom("hdlr",Handler("vide"))))}.Concat(metadata).ToArray()))
        .Concat(Atom("mdat",[1,2,3,4])).ToArray();
    private static byte[] Handler(string type){byte[] bytes=new byte[24];Encoding.ASCII.GetBytes(type).CopyTo(bytes,8);return bytes;}
    private static byte[] UInt32(uint value){byte[] bytes=new byte[4];BinaryPrimitives.WriteUInt32BigEndian(bytes,value);return bytes;}
    private static MediaInspection InspectBytes(byte[] bytes,string name="synthetic.mov")
    {string path=Path.Combine(Root(),name);File.WriteAllBytes(path,bytes);return MediaInspector.Inspect(path,default);}
}
