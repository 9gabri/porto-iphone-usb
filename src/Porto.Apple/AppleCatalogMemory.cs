using System.Runtime.InteropServices;

namespace Porto.Apple;

internal static class AppleCatalogMemory
{
    internal const long ReserveBytes=512L*1024*1024;
    // Budget captured bytes and the native SQLite verification copy before reading USB.
    // WAL growth is checked again when its committed size becomes known.
    internal static long CaptureAllocation(long bytes)
    {
        if(bytes<=0||bytes>AppleSqliteImage.MaximumBytes)
            throw new IOException("[APPLE-CATALOG-MEMORY] Dimensione del catalogo non valida.");
        return checked(2*bytes+32);
    }
    internal static void EnsureAvailable(long allocation)
    {
        var status=new MemoryStatus {Length=(uint)Marshal.SizeOf<MemoryStatus>()};
        if(!GlobalMemoryStatusEx(ref status))throw new IOException("[APPLE-CATALOG-MEMORY] Impossibile verificare la memoria disponibile. Catalogo non aperto.");
        ValidateAvailable(allocation,status.AvailablePhysical,status.AvailableCommit);
    }
    internal static void ValidateAvailable(long allocation,ulong physical,ulong commit)
    {
        if(allocation<0||allocation>2L*AppleSqliteImage.MaximumBytes+32)throw new IOException("[APPLE-CATALOG-MEMORY] Dimensione dell'allocazione non valida.");
        ulong required=checked((ulong)(allocation+ReserveBytes));
        if(physical<required||commit<required)
            throw new IOException(FormattableString.Invariant($"[APPLE-CATALOG-MEMORY] Memoria disponibile insufficiente per leggere il catalogo. Richiesti con margine: {required} byte; RAM disponibile: {physical} byte; capacità di allocazione: {commit} byte. Chiudi le altre applicazioni e riprova."));
    }
    // A volatile measurement, not a reservation. Allocation failure is also handled by capture.
    [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus
    {
        internal uint Length,Load;
        internal ulong TotalPhysical,AvailablePhysical,TotalCommit,AvailableCommit,TotalVirtual,AvailableVirtual,AvailableExtendedVirtual;
    }
    [DllImport("kernel32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
}
