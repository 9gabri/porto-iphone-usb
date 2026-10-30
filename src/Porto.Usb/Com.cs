using System.Runtime.InteropServices;

namespace Porto.Usb;

[StructLayout(LayoutKind.Sequential)]
internal struct PropertyKey(Guid format,uint id) { public Guid Format=format;public uint Id=id; }

// Only the read-side COM ABI is exposed. Slots verified against Microsoft WinSDK headers.
internal sealed unsafe class Com(nint pointer) : IDisposable
{
    public nint Pointer {get;private set;}=pointer;
    public nint Slot(int index)=>((nint*)*(nint*)Pointer)[index];
    public static Com Create(string clsid,string iid)
    {Guid c=new(clsid),i=new(iid);Check(CoCreateInstance(ref c,0,1,ref i,out var p));return new(p);}
    public static void Check(int hr){if(hr<0)throw new IOException($"USB non disponibile o accesso negato (0x{hr:X8}). Sblocca e autorizza l'iPhone.");}
    public Com Interface(int slot)
    {nint p=0;Check(((delegate* unmanaged[Stdcall]<nint,nint*,int>)Slot(slot))(Pointer,&p));return new(p);}
    public string? String(PropertyKey key)
    {
        nint p=0;int hr=((delegate* unmanaged[Stdcall]<nint,PropertyKey*,nint*,int>)Slot(8))(Pointer,&key,&p);
        try{return hr<0?null:Marshal.PtrToStringUni(p);}finally{if(p!=0)Marshal.FreeCoTaskMem(p);}
    }
    public Guid? GuidValue(PropertyKey key)
    {Guid result=default;return ((delegate* unmanaged[Stdcall]<nint,PropertyKey*,Guid*,int>)Slot(28))(Pointer,&key,&result)<0?null:result;}
    public long? Length(PropertyKey key)
    {ulong result=0;return ((delegate* unmanaged[Stdcall]<nint,PropertyKey*,ulong*,int>)Slot(14))(Pointer,&key,&result)<0||result>long.MaxValue?null:(long)result;}
    public void SetUInt(PropertyKey key,uint value)=>Check(((delegate* unmanaged[Stdcall]<nint,PropertyKey*,uint,int>)Slot(9))(Pointer,&key,value));
    public void SetString(PropertyKey key,string value)
    {fixed(char* v=value)Check(((delegate* unmanaged[Stdcall]<nint,PropertyKey*,char*,int>)Slot(7))(Pointer,&key,v));}
    public void Dispose(){if(Pointer!=0){((delegate* unmanaged[Stdcall]<nint,uint>)Slot(2))(Pointer);Pointer=0;}}
    [DllImport("ole32.dll")][DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int CoCreateInstance(ref Guid clsid,nint outer,uint context,ref Guid iid,out nint result);
    [DllImport("ole32.dll")][DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int CoInitializeEx(nint reserved,uint mode);
    [DllImport("ole32.dll")][DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern void CoUninitialize();
}
