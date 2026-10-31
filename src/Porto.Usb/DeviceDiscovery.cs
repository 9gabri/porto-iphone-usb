using Porto.Core;

namespace Porto.Usb;

public sealed record DeviceDescriptor(string Id, string Name, string Manufacturer, bool MetadataIncomplete = false);
public sealed record DeviceBatch(int HResult, List<DeviceDescriptor> Devices);
public sealed record DiscoveryResult(List<DeviceInfo> Devices, int WindowsDeviceCount, int MetadataFailures)
{
    public string Detail => Devices.Count > 0 ? $"{Devices.Count} dispositivo/i Apple disponibile/i."
        : WindowsDeviceCount == 0 ? "Windows non espone dispositivi portatili. Verifica prima che Dispositivi Apple riconosca l'iPhone."
        : MetadataFailures > 0 ? "Windows vede dispositivi portatili, ma alcuni dati non sono leggibili. Sblocca l'iPhone e verifica Dispositivi Apple."
        : "Windows vede dispositivi portatili, ma nessuno identificato come Apple. Verifica cavo dati, autorizzazione e Dispositivi Apple.";
}

public static class DeviceDiscovery
{
    // S_FALSE means truncated, not an authoritative empty/complete device list.
    public static DiscoveryResult Read(Func<DeviceBatch> read)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var batch = read();
            if (batch.HResult == 1) continue;
            if (batch.HResult != 0) throw new IOException($"Ricerca USB incompleta (0x{batch.HResult:X8}). Riprova.");
            var devices = batch.Devices.Where(d => d.Id.Contains("vid_05ac", StringComparison.OrdinalIgnoreCase)
                || d.Manufacturer.Contains("Apple", StringComparison.OrdinalIgnoreCase))
                .Select(d => new DeviceInfo(d.Id, string.IsNullOrWhiteSpace(d.Name) ? "Dispositivo Apple USB" : d.Name)).ToList();
            return new(devices, batch.Devices.Count, batch.Devices.Count(d => d.MetadataIncomplete));
        }
        throw new IOException("L'elenco USB continua a cambiare. Attendi il collegamento stabile e premi Cerca iPhone.");
    }
}
