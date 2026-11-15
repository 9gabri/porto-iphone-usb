using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Porto.Core;
using Porto.Usb;

// Native COM vtables are synthetic: no COM discovery, phone, driver or media access.
internal static unsafe class Rc5NativeResourceTests
{
    private static readonly Guid ResourceDefault=new("e81e79be-34f0-41bf-b53f-f1a06ae87842");
    private static readonly Guid ResourceAttributes=new("1eb6f604-9278-429f-93cc-5bb8c06656b6");
    private static nint attributes,streamPointer;
    private static int attributesResult,lengthResult,openResult,attributeReleases,streamReleases,reads,position;
    private static uint optimal,lastRequested;
    private static ulong length;
    private static bool keysCorrect,readOnly,returnPointerOnFailure,tailSent,shortSuccess;
    private static byte[] payload=[];

    public static void Add(List<(string,Action)> tests)
    {
        foreach(ulong expected in new ulong[]{0,12345,4294967313})
            tests.Add(($"rc5 native resource length {expected} uses the exact resource property and read-only stream",()=>
            {
                using var fake=new Fixture();length=expected;
                using(var input=fake.Source.OpenRead(Item(),default))
                    Fixtures.Check(ResourceLength(input)==(long)expected,"The stream must expose the default resource length without truncating UInt64.");
                Fixtures.Check(keysCorrect&&readOnly&&attributeReleases==1&&streamReleases==1,"Wrong ABI, resource key, access mode or reference ownership.");
            }));

        foreach(int hr in new[]{unchecked((int)0x80070032),unchecked((int)0x80070490)})
            tests.Add(($"rc5 native missing resource attributes {hr:X8} fall back without inventing a length",()=>
            {
                using var fake=new Fixture();attributesResult=hr;
                using var input=fake.Source.OpenRead(Item(),default);
                Fixtures.Check(ResourceLength(input)==null&&readOnly,"Unavailable metadata must not invent a zero resource length.");
            }));
        tests.Add(("rc5 native missing resource size property remains unknown",()=>
        {
            using var fake=new Fixture();lengthResult=unchecked((int)0x80070490);
            using var input=fake.Source.OpenRead(Item(),default);
            Fixtures.Check(ResourceLength(input)==null&&attributeReleases==1);
        }));
        tests.Add(("rc5 native lost session from resource attributes propagates and releases an allocated result",()=>
        {
            using var fake=new Fixture();attributesResult=unchecked((int)0x8007048F);returnPointerOnFailure=true;
            Fixtures.Throws<SourceUnavailableException>(()=>fake.Source.OpenRead(Item(),default));
            Fixtures.Check(attributeReleases==1&&!readOnly,"The failed attributes result must be released and GetStream must not be called.");
        }));
        tests.Add(("rc5 native lost session reading resource length propagates and releases attributes",()=>
        {
            using var fake=new Fixture();lengthResult=unchecked((int)0x8007048F);
            Fixtures.Throws<SourceUnavailableException>(()=>fake.Source.OpenRead(Item(),default));
            Fixtures.Check(attributeReleases==1&&!readOnly);
        }));
        tests.Add(("rc5 native GetStream failure releases an allocated stream",()=>
        {
            using var fake=new Fixture();openResult=unchecked((int)0x80042007);returnPointerOnFailure=true;
            Fixtures.Throws<IOException>(()=>fake.Source.OpenRead(Item(),default));
            Fixtures.Check(streamReleases==1&&attributeReleases==1);
        }));
        tests.Add(("rc5 native final S_FALSE bytes are retained and EOF is not requested from the driver again",()=>
        {
            using var fake=new Fixture();payload=[11,22,33];length=3;
            using var input=fake.Source.OpenRead(Item(),default);
            byte[] buffer=new byte[4096];
            Fixtures.Check(input.Read(buffer,0,buffer.Length)==3&&buffer.AsSpan(0,3).SequenceEqual(payload));
            Fixtures.Check(input.Read(buffer,0,buffer.Length)==0&&input.Read(buffer,0,1)==0&&reads==1,
                "S_FALSE is terminal even when the final read returned bytes; a repeated driver Read can fail.");
        }));
        tests.Add(("rc5 native optimal buffer below 4096 is respected",()=>
        {
            using var fake=new Fixture();optimal=512;payload=new byte[900];length=900;
            using var input=fake.Source.OpenRead(Item(),default);
            Fixtures.Check(input.Read(new byte[8192],0,8192)==512&&lastRequested==512,"Do not enlarge the driver's optimal read request.");
        }));
        tests.Add(("rc5 native short successful read does not imply EOF",()=>
        {
            using var fake=new Fixture();payload=Enumerable.Range(0,31).Select(i=>(byte)i).ToArray();length=31;shortSuccess=true;
            using var input=fake.Source.OpenRead(Item(),default);using var output=new MemoryStream();
            input.CopyTo(output);
            Fixtures.Check(output.ToArray().SequenceEqual(payload)&&reads>1,"Only terminal status, not a short S_OK read, proves EOF.");
        }));
        tests.Add(("rc5 native zero-byte read does not call the driver or consume EOF",()=>
        {
            using var fake=new Fixture();payload=[9];length=1;
            using var input=fake.Source.OpenRead(Item(),default);byte[] buffer=new byte[8];
            Fixtures.Check(input.Read(buffer,0,0)==0&&reads==0);
            Fixtures.Check(input.Read(buffer,0,8)==1&&buffer[0]==9);
        }));
        tests.Add(("rc5 native terminal S_FALSE passes engine copy and independent second read",()=>
        {
            using var fake=new Fixture();payload=Fixtures.Video();length=(ulong)payload.Length;
            using var destination=new Destination(Fixtures.Root());
            var source=FakeSource.One(payload);
            source.Reader=(_,_)=>fake.Source.OpenRead(Item(),default);
            var report=new ImportEngine(source,destination).Run(true,default);
            Fixtures.Check(report.Count(Outcome.Imported)==1&&report.Count(Outcome.Error)==0,report.Status);
            Fixtures.Check(source.Reads==2&&streamReleases==2,"Each source read must own and close one native stream.");
            Fixtures.Check(File.ReadAllBytes(Directory.GetFiles(destination.Videos).Single()).SequenceEqual(payload));
        }));
    }

    private static SourceItem Item()=>new("synthetic-object","p","synthetic.mov",MediaKind.Video,987654321);
    // Allows the pre-fix baseline to compile and fail the contract assertion.
    private static long? ResourceLength(Stream input)
    {
        var contract=input.GetType().GetInterface("ISourceStreamInfo");
        Fixtures.Check(contract!=null,"The opened USB stream has no resource-size contract.");
        return (long?)contract!.GetProperty("ResourceLength")!.GetValue(input);
    }

    private sealed class Fixture:IDisposable
    {
        private readonly Native device=new(),content=new(),properties=new(),resources=new(),values=new(),stream=new();
        public WpdSource Source {get;}
        public Fixture()
        {
            attributesResult=lengthResult=openResult=attributeReleases=streamReleases=reads=position=0;
            optimal=4096;lastRequested=0;length=0;keysCorrect=true;readOnly=returnPointerOnFailure=tailSent=shortSuccess=false;payload=[];
            attributes=values.Pointer;streamPointer=stream.Pointer;
            resources.Set(4,(nint)(delegate* unmanaged[Stdcall]<nint,char*,PropertyKey*,nint*,int>)&Attributes);
            resources.Set(5,(nint)(delegate* unmanaged[Stdcall]<nint,char*,PropertyKey*,uint,uint*,nint*,int>)&Open);
            resources.Set(7,(nint)(delegate* unmanaged[Stdcall]<nint,int>)&Cancel);
            values.Set(14,(nint)(delegate* unmanaged[Stdcall]<nint,PropertyKey*,ulong*,int>)&Length);
            stream.Set(3,(nint)(delegate* unmanaged[Stdcall]<nint,byte*,uint,uint*,int>)&Read);
            Source=new(new(device.Pointer),new(content.Pointer),new(properties.Pointer),new(resources.Pointer));
        }
        public void Dispose(){Source.Dispose();stream.Dispose();values.Dispose();resources.Dispose();properties.Dispose();content.Dispose();device.Dispose();}
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int Attributes(nint self,char* id,PropertyKey* key,nint* result)
    {
        keysCorrect&=new string(id)=="synthetic-object"&&key->Format==ResourceDefault&&key->Id==0;
        *result=attributesResult>=0||returnPointerOnFailure?attributes:0;return attributesResult;
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int Length(nint self,PropertyKey* key,ulong* result)
    {
        keysCorrect&=key->Format==ResourceAttributes&&key->Id==2;
        *result=length;return lengthResult;
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int Open(nint self,char* id,PropertyKey* key,uint mode,uint* buffer,nint* result)
    {
        keysCorrect&=new string(id)=="synthetic-object"&&key->Format==ResourceDefault&&key->Id==0;
        position=0;tailSent=false;
        readOnly=mode==0;*buffer=optimal;*result=openResult>=0||returnPointerOnFailure?streamPointer:0;return openResult;
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int Read(nint self,byte* buffer,uint count,uint* received)
    {
        reads++;lastRequested=count;*received=0;
        if(tailSent)return unchecked((int)0x80042007);
        int remaining=payload.Length-position;
        int n=Math.Min((int)count,remaining);
        if(shortSuccess&&remaining>3)n=Math.Min(n,3);
        for(int i=0;i<n;i++)buffer[i]=payload[position+i];
        position+=n;*received=(uint)n;
        if(position==payload.Length&&n<count){tailSent=true;return 1;}
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int Cancel(nint self)=>0;
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static uint Release(nint self)
    {if(self==attributes)attributeReleases++;if(self==streamPointer)streamReleases++;return 0;}
    private sealed class Native:IDisposable
    {
        private readonly nint table=(nint)NativeMemory.AllocZeroed(40,(nuint)IntPtr.Size);
        public nint Pointer {get;}=(nint)NativeMemory.AllocZeroed((nuint)IntPtr.Size);
        public Native(){*(nint*)Pointer=table;Set(2,(nint)(delegate* unmanaged[Stdcall]<nint,uint>)&Release);}
        public void Set(int slot,nint method)=>((nint*)table)[slot]=method;
        public void Dispose(){NativeMemory.Free((void*)Pointer);NativeMemory.Free((void*)table);}
    }
}
