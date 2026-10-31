using System.Buffers.Binary;
using System.Text;
using Porto.Core;

internal static class Fixtures
{
    public static string Root()
    {
        string root=Path.Combine(Environment.CurrentDirectory,"test-output",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);return root;
    }
    public static byte[] Atom(string type,params byte[][] contents)
    {
        int length=8+contents.Sum(x=>x.Length);byte[] output=new byte[length];BinaryPrimitives.WriteUInt32BigEndian(output,(uint)length);Encoding.ASCII.GetBytes(type).CopyTo(output,4);
        int offset=8;foreach(var bytes in contents){bytes.CopyTo(output,offset);offset+=bytes.Length;}return output;
    }
    public static byte[] Video(int seed=1,string? marker=null)
    {
        byte[] handler=new byte[24];Encoding.ASCII.GetBytes("vide").CopyTo(handler,8);
        byte[] payload=new byte[65539];new Random(seed).NextBytes(payload);
        return Atom("ftyp",Encoding.ASCII.GetBytes("qt  \0\0\0\0qt  "))
            .Concat(Atom("moov",Atom("trak",Atom("mdia",Atom("hdlr",handler))),Atom("free",Encoding.UTF8.GetBytes(marker??"synthetic test only"))))
            .Concat(Atom("mdat",payload)).ToArray();
    }
    public static void Check(bool condition,string message="Assertion failed"){if(!condition)throw new Exception(message);}
    public static void Throws<T>(Action action)where T:Exception{try{action();}catch(T){return;}throw new Exception("Expected "+typeof(T).Name);}
}
internal sealed class FakeSource(List<SourceItem> items,Dictionary<string,byte[]> data):IVideoSource
{
    public Func<SourceItem,int,Stream>? Reader {get;set;}
    public int Reads {get;private set;}
    public List<string> Errors {get;set;}=[];
    public Inventory Inventory(CancellationToken cancel)=>new(items,Errors,Errors.Count==0);
    public Stream OpenRead(SourceItem item,CancellationToken cancel)
    {Reads++;return Reader?.Invoke(item,Reads)??new MemoryStream(data[item.Id],false);}
    public void Dispose(){}
    public static FakeSource One(byte[]? video=null,string id="1",string name="IMG_0001.MOV",long? size=null)
    {video??=Fixtures.Video();return new([new(id,"dcim",name,MediaKind.Video,size??video.LongLength) ],new(){{id,video}});}
}
internal sealed class FakeSpace(long available):IDiskSpace
{
    public long Bytes=available;public long Available(string directory)=>Bytes;
}
internal sealed class VirtualStream(long length):Stream
{
    private long position;
    public override int Read(byte[] buffer,int offset,int count){int n=(int)Math.Min(count,length-position);Array.Clear(buffer,offset,n);position+=n;return n;}
    public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;
    public override long Length=>length;public override long Position{get=>position;set=>throw new NotSupportedException();}
    public override void Flush(){}public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long v)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
}
