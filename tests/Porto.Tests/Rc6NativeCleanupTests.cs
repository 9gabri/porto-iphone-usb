using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Porto.Core;
using Porto.Usb;

// Entirely synthetic vtables: no COM activation, device access or personal media.
internal static unsafe class Rc6NativeCleanupTests
{
    private static nint streamPointer,resourcesPointer;
    private static int cancels,releases,opens,position,cancelResult,openResult,readResult;
    private static bool dirty,readOnly,resourcesReleased,cancelAfterResourceRelease,returnStreamOnError;
    private static readonly byte[] A=[1,2,3,4,5,6],B=[91,92,93];
    private static byte[] payload=[];
    private static readonly List<string> events=[];

    public static void Add(List<(string,Action)> tests)
    {
        tests.Add(("rc6 native incomplete A is cancelled before B receives its own bytes",()=>
        {
            using var fake=new Fixture();
            using(var input=fake.Source.OpenRead(Item("A"),default))
                Fixtures.Throws<InvalidDataException>(()=>VerifiedCopy.Copy(input,Stream.Null,3,null,null,default));
            using(var input=fake.Source.OpenRead(Item("B"),default))
            using(var output=new MemoryStream())
            {
                input.CopyTo(output);
                Fixtures.Check(output.ToArray().SequenceEqual(B),"B received A's bytes after the aborted read.");
            }
            Fixtures.Check(cancels==1&&releases==2&&readOnly,"Cancel exactly the unfinished stream; release both read-only streams.");
        }));
        tests.Add(("rc6 native clean S_FALSE tail is released without Cancel",()=>
        {
            using var fake=new Fixture();
            using(var input=fake.Source.OpenRead(Item("B"),default))
            {
                byte[] data=new byte[32];
                Fixtures.Check(input.Read(data,0,data.Length)==B.Length&&input.Read(data,0,data.Length)==0);
            }
            Fixtures.Check(cancels==0&&releases==1,"A completed transfer must not cancel device operations.");
        }));
        tests.Add(("rc6 native failed cleanup preserves read error and blocks subsequent Open and Inventory",()=>
        {
            using var fake=new Fixture();readResult=unchecked((int)0x8007001E);cancelResult=unchecked((int)0x80004005);
            Exception? primary=null;
            try
            {
                using var input=fake.Source.OpenRead(Item("A"),default);
                _=input.Read(new byte[4],0,4);
            }
            catch(IOException error){primary=error;}
            Fixtures.Check(primary?.Message.Contains("8007001E",StringComparison.Ordinal)==true,"Cleanup replaced the original read failure.");
            Fixtures.Check(cancels==1&&releases==1,"Cancel failure must still release the stream.");
            int previousOpens=opens;
            Fixtures.Throws<SourceUnavailableException>(()=>fake.Source.OpenRead(Item("B"),default));
            Fixtures.Throws<SourceUnavailableException>(()=>fake.Source.Inventory(default));
            Fixtures.Check(opens==previousOpens,"A failed cleanup allowed another native open.");
        }));
        tests.Add(("rc6 native failed GetStream cancels and releases an allocated result",()=>
        {
            using var fake=new Fixture();openResult=unchecked((int)0x80042007);
            Fixtures.Throws<IOException>(()=>fake.Source.OpenRead(Item("A"),default));
            Fixtures.Check(cancels==1&&releases==1,"Failed GetStream left its operation or allocated interface behind.");
            openResult=0;
            using var input=fake.Source.OpenRead(Item("B"),default);using var output=new MemoryStream();input.CopyTo(output);
            Fixtures.Check(output.ToArray().SequenceEqual(B),"The failed open contaminated the next stream.");
        }));
        tests.Add(("rc6 native failed GetStream cleanup retains original HRESULT and invalidates session",()=>
        {
            using var fake=new Fixture();openResult=unchecked((int)0x80042007);cancelResult=unchecked((int)0x80004005);
            Exception? primary=null;
            try{fake.Source.OpenRead(Item("A"),default);}catch(IOException error){primary=error;}
            Fixtures.Check(primary?.Message.Contains("80042007",StringComparison.Ordinal)==true,"Cancel failure hid the GetStream error.");
            Fixtures.Check(cancels==1&&releases==1);
            Fixtures.Throws<SourceUnavailableException>(()=>fake.Source.OpenRead(Item("B"),default));
        }));
        tests.Add(("rc6 native failed GetStream without pointer still cancels its unfinished operation",()=>
        {
            using var fake=new Fixture();openResult=unchecked((int)0x80042007);returnStreamOnError=false;
            Fixtures.Throws<IOException>(()=>fake.Source.OpenRead(Item("A"),default));
            Fixtures.Check(cancels==1&&releases==0,"An open failure without a returned stream still needs cancellation, without releasing a null pointer.");
            openResult=0;
            using var input=fake.Source.OpenRead(Item("B"),default);using var output=new MemoryStream();input.CopyTo(output);
            Fixtures.Check(output.ToArray().SequenceEqual(B));
        }));
        tests.Add(("rc6 native opening B closes A and stale A cannot consume or cancel B",()=>
        {
            using var fake=new Fixture();using var old=fake.Source.OpenRead(Item("A"),default);
            Fixtures.Check(old.ReadByte()==A[0]);
            using var current=fake.Source.OpenRead(Item("B"),default);
            Fixtures.Throws<ObjectDisposedException>(()=>old.ReadByte());
            old.Dispose();
            using var output=new MemoryStream();current.CopyTo(output);
            Fixtures.Check(output.ToArray().SequenceEqual(B)&&cancels==1&&releases==1,"The old wrapper affected the new native stream.");
        }));
        tests.Add(("rc6 native source closes active stream before releasing resources and disposal is idempotent",()=>
        {
            using var fake=new Fixture();using var input=fake.Source.OpenRead(Item("A"),default);
            Fixtures.Check(input.Read(new byte[1],0,1)==1);
            fake.Source.Dispose();input.Dispose();fake.Source.Dispose();
            Fixtures.Check(cancels==1&&releases==1&&!cancelAfterResourceRelease,"Source/stream disposal must clean up once before resources are released.");
            Fixtures.Check(events.IndexOf("stream-release")<events.IndexOf("resources-release"),"The native resource owner was released before its active stream.");
            Fixtures.Throws<ObjectDisposedException>(()=>{_=input.Read(new byte[1],0,1);});
        }));
    }

    private static SourceItem Item(string id)=>new(id,"synthetic-parent",id+".mov",MediaKind.Video);
    private sealed class Fixture:IDisposable
    {
        private readonly Native device=new(),content=new(),properties=new(),resources=new(),stream=new();
        public WpdSource Source {get;}
        public Fixture()
        {
            cancels=releases=opens=position=cancelResult=openResult=readResult=0;
            dirty=resourcesReleased=cancelAfterResourceRelease=false;readOnly=returnStreamOnError=true;payload=[];events.Clear();
            streamPointer=stream.Pointer;resourcesPointer=resources.Pointer;
            resources.Set(4,(nint)(delegate* unmanaged[Stdcall]<nint,char*,PropertyKey*,nint*,int>)&Attributes);
            resources.Set(5,(nint)(delegate* unmanaged[Stdcall]<nint,char*,PropertyKey*,uint,uint*,nint*,int>)&Open);
            // WinSDK PortableDeviceApi.h: IUnknown 0..2, GetSupportedResources 3,
            // GetResourceAttributes 4, GetStream 5, Delete 6, Cancel 7.
            resources.Set(7,(nint)(delegate* unmanaged[Stdcall]<nint,int>)&Cancel);
            content.Set(3,(nint)(delegate* unmanaged[Stdcall]<nint,uint,char*,nint,nint*,int>)&Enumerate);
            stream.Set(3,(nint)(delegate* unmanaged[Stdcall]<nint,byte*,uint,uint*,int>)&Read);
            Source=new(new(device.Pointer),new(content.Pointer),new(properties.Pointer),new(resources.Pointer));
        }
        public void Dispose(){Source.Dispose();stream.Dispose();resources.Dispose();properties.Dispose();content.Dispose();device.Dispose();}
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int Attributes(nint self,char* id,PropertyKey* key,nint* value)
    {*value=0;return unchecked((int)0x80004001);}
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int Enumerate(nint self,uint flags,char* id,nint filter,nint* value)
    {*value=0;return unchecked((int)0x80004005);}
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int Open(nint self,char* id,PropertyKey* key,uint mode,uint* optimal,nint* value)
    {
        opens++;readOnly&=mode==0;position=0;
        // The synthetic driver retains its previous transaction until Cancel or EOF.
        payload=dirty?A:new string(id)=="A"?A:B;dirty=true;
        *optimal=4;*value=openResult>=0||returnStreamOnError?streamPointer:0;return openResult;
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int Read(nint self,byte* buffer,uint requested,uint* received)
    {
        *received=0;if(readResult<0)return readResult;
        int count=Math.Min((int)requested,payload.Length-position);
        for(int i=0;i<count;i++)buffer[i]=payload[position+i];
        position+=count;*received=(uint)count;
        if(position==payload.Length){dirty=false;return 1;}
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int Cancel(nint self)
    {
        cancels++;cancelAfterResourceRelease|=resourcesReleased;events.Add("cancel");
        if(cancelResult>=0)dirty=false;
        return cancelResult;
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static uint Release(nint self)
    {
        if(self==streamPointer){releases++;events.Add("stream-release");}
        if(self==resourcesPointer){resourcesReleased=true;events.Add("resources-release");}
        return 0;
    }
    private sealed class Native:IDisposable
    {
        private readonly nint table=(nint)NativeMemory.AllocZeroed(40,(nuint)IntPtr.Size);
        public nint Pointer {get;}=(nint)NativeMemory.AllocZeroed((nuint)IntPtr.Size);
        public Native(){*(nint*)Pointer=table;Set(2,(nint)(delegate* unmanaged[Stdcall]<nint,uint>)&Release);}
        public void Set(int slot,nint method)=>((nint*)table)[slot]=method;
        public void Dispose(){NativeMemory.Free((void*)Pointer);NativeMemory.Free((void*)table);}
    }
}
