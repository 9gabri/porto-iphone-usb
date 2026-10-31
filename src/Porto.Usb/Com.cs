using System.Runtime.InteropServices;
using Porto.Core;

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
    public static void Check(int hr)
    {
        if(hr>=0)return;
        if(IsSessionLost(hr))
        {
            string message=$"La sessione USB non è più disponibile (0x{hr:X8}). Ricollega lo stesso iPhone per creare un nuovo inventario.";
            throw new SourceUnavailableException(message,new COMException(message,hr));
        }
        string detail=unchecked((uint)hr)==0x80040154
            ?$"Componente Windows per dispositivi portatili (WPD) non registrato (0x{hr:X8}). Verifica o ripristina i componenti multimediali e WPD di Windows, poi riprova."
            :$"Operazione USB non riuscita (0x{hr:X8}). Verifica collegamento, sblocco e autorizzazione dell'iPhone.";
        throw new IOException(detail,hr);
    }
    // Explicit lost/hung/stale-session errors from Microsoft WPD and COM Error Constants.
    // File-not-found and access-denied remain per-file failures: they do not prove session loss.
    internal static bool IsSessionLost(int hr)=>unchecked((uint)hr) is
        0x8007048F or 0x802A0002 or 0x802A0006 or 0x80070015 or 0x80042003 or 0x80042009 or
        0x80010006 or 0x80010007 or 0x80010012 or 0x80010108 or 0x800401FD;
    private static void CheckSession(int hr){if(IsSessionLost(hr))Check(hr);}
    public Com Interface(int slot)
    {nint p=0;Check(((delegate* unmanaged[Stdcall]<nint,nint*,int>)Slot(slot))(Pointer,&p));return new(p);}
    public string? String(PropertyKey key)
    {
        nint p=0;int hr=((delegate* unmanaged[Stdcall]<nint,PropertyKey*,nint*,int>)Slot(8))(Pointer,&key,&p);
        try{CheckSession(hr);return hr<0?null:Marshal.PtrToStringUni(p);}finally{if(p!=0)Marshal.FreeCoTaskMem(p);}
    }
    public Guid? GuidValue(PropertyKey key)
    {
        Guid result=default;int hr=((delegate* unmanaged[Stdcall]<nint,PropertyKey*,Guid*,int>)Slot(28))(Pointer,&key,&result);
        CheckSession(hr);return hr<0?null:result;
    }
    public long? Length(PropertyKey key)
    {
        ulong result=0;int hr=((delegate* unmanaged[Stdcall]<nint,PropertyKey*,ulong*,int>)Slot(14))(Pointer,&key,&result);
        CheckSession(hr);return hr<0||result>long.MaxValue?null:(long)result;
    }
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
