using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace Porto.Apple;

// One owned RAM image shared by capture, WAL application and readonly SQLite.
// No file backing. Padding protects SQLite's documented malformed-input overreads.
internal sealed unsafe class AppleCatalogBuffer : SafeHandleZeroOrMinusOneIsInvalid
{
    internal const int Padding=32;
    internal int Length {get;}
    internal int Capacity=>checked(Length+Padding);
    internal Span<byte> Data
    {
        get {ObjectDisposedException.ThrowIf(IsClosed,this);return new Span<byte>((void*)handle,Length);}
    }
    internal AppleCatalogBuffer(int length):base(true)
    {
        if(length<=0||length>AppleSqliteImage.MaximumBytes)throw new IOException("Dimensione del catalogo non valida.");
        Length=length;
        SetHandle(Marshal.AllocHGlobal(Capacity));
        new Span<byte>((void*)handle,Capacity).Clear();
    }
    internal static AppleCatalogBuffer Copy(byte[] image)
    {
        AppleCatalogMemory.EnsureAvailable(checked(image.LongLength+Padding));
        var result=new AppleCatalogBuffer(image.Length);
        image.CopyTo(result.Data);return result;
    }
    protected override bool ReleaseHandle()
    {
        CryptographicOperations.ZeroMemory(new Span<byte>((void*)handle,Capacity));
        Marshal.FreeHGlobal(handle);return true;
    }
}
