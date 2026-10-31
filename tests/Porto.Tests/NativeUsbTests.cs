using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Porto.Usb;

internal static unsafe class NativeUsbTests
{
    private static int result,releases,nextCalls;
    private static nint enumerator;
    private static readonly List<string> events=[];
    public static void Add(List<(string,Action)> tests)
    {
        foreach(int hr in new[]{0,1,unchecked((int)0x80004005)})tests.Add(($"native discovery ABI propagates {hr:X8} and releases allocated strings",()=>{
            result=hr;releases=0;int freed=0;
            using(var native=new Native())
            {
                native.Set(3,(nint)(delegate* unmanaged[Stdcall]<nint,nint*,uint*,int>)&Devices);
                native.Set(5,(nint)(delegate* unmanaged[Stdcall]<nint,char*,char*,uint*,int>)&Name);
                native.Set(7,(nint)(delegate* unmanaged[Stdcall]<nint,char*,char*,uint*,int>)&Manufacturer);
                using var manager=new Com(native.Pointer);
                void Free(nint p){freed++;Marshal.FreeCoTaskMem(p);}
                if(hr<0)Fixtures.Throws<IOException>(()=>WpdSource.ReadDeviceBatch(manager,Free));
                else
                {
                    var batch=WpdSource.ReadDeviceBatch(manager,Free);Fixtures.Check(batch.HResult==hr);
                    if(hr==0)Fixtures.Check(batch.Devices.Single().Name=="iPhone sintetico"&&batch.Devices.Single().Manufacturer=="Apple");
                }
                Fixtures.Check(freed==1);
            }
            Fixtures.Check(releases==1);
        }));
        tests.Add(("native inventory emits progress between metadata calls and ends on S_FALSE",()=>{
            Com.Check(Com.CoInitializeEx(0,0));
            try
            {
                releases=0;nextCalls=0;events.Clear();
                using var device=new Native();using var content=new Native();using var properties=new Native();using var resources=new Native();using var iterator=new Native();
                enumerator=iterator.Pointer;
                content.Set(3,(nint)(delegate* unmanaged[Stdcall]<nint,uint,char*,nint,nint*,int>)&Enumerate);
                properties.Set(5,(nint)(delegate* unmanaged[Stdcall]<nint,char*,nint,nint*,int>)&Values);
                iterator.Set(3,(nint)(delegate* unmanaged[Stdcall]<nint,uint,nint*,uint*,int>)&Next);
                using(var source=new WpdSource(new(device.Pointer),new(content.Pointer),new(properties.Pointer),new(resources.Pointer)))
                {
                    source.InventoryProgress=_=>events.Add("progress");var inventory=source.Inventory(default);
                    Fixtures.Check(inventory.EnumerationComplete&&inventory.Items.Count==3&&nextCalls==1);
                    for(int i=1;i<=3;i++){int index=events.IndexOf("metadata"+i);Fixtures.Check(index>=0&&events[index+1]=="progress");}
                }
                Fixtures.Check(releases==5);
            }
            finally{Com.CoUninitialize();}
        }));
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static uint Release(nint self){releases++;return 0;}
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int Devices(nint self,nint* ids,uint* count)
    {if(ids==null){*count=1;return 0;}ids[0]=Marshal.StringToCoTaskMemUni("usb#vid_05ac#synthetic");*count=1;return result;}
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int Name(nint self,char* id,char* output,uint* size)=>Text("iPhone sintetico",output,size);
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int Manufacturer(nint self,char* id,char* output,uint* size)=>Text("Apple",output,size);
    private static int Text(string value,char* output,uint* size)
    {
        if(output!=null){for(int i=0;i<value.Length;i++)output[i]=value[i];output[value.Length]='\0';}
        *size=(uint)value.Length+1;return 0;
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int Enumerate(nint self,uint flags,char* parent,nint filter,nint* output){*output=enumerator;return 0;}
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int Next(nint self,uint count,nint* ids,uint* fetched)
    {nextCalls++;for(int i=0;i<3;i++)ids[i]=Marshal.StringToCoTaskMemUni((i+1).ToString());*fetched=3;return 1;}
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])]
    private static int Values(nint self,char* id,nint keys,nint* output)
    {
        try
        {
            events.Add("metadata"+new string(id));
            var values=Com.Create("0c15d503-d017-47ce-9016-7b3f978721cc","6848f6f2-3155-4f86-b6f5-263eeeab3143");
            Guid group=new("ef6b490d-5cd8-437a-affc-da8b60ee4a3c"),kind=new("9261b03c-3d78-4519-85e3-02c5e1f50bb9");
            values.SetString(new(group,12),"synthetic.mov");var key=new PropertyKey(group,7);
            Com.Check(((delegate* unmanaged[Stdcall]<nint,PropertyKey*,Guid*,int>)values.Slot(27))(values.Pointer,&key,&kind));
            *output=values.Pointer;return 0; // Reference is transferred to WpdSource's using scope.
        }
        catch{return unchecked((int)0x80004005);}
    }
    private sealed class Native:IDisposable
    {
        private readonly nint table=(nint)NativeMemory.AllocZeroed(40,(nuint)IntPtr.Size);
        public nint Pointer {get;}=(nint)NativeMemory.AllocZeroed((nuint)IntPtr.Size);
        public Native(){*(nint*)Pointer=table;Set(2,(nint)(delegate* unmanaged[Stdcall]<nint,uint>)&Release);}
        public void Set(int index,nint method)=>((nint*)table)[index]=method;
        public void Dispose(){NativeMemory.Free((void*)Pointer);NativeMemory.Free((void*)table);}
    }
}
