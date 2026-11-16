using Porto.Core;

namespace Porto.Usb;

public sealed record DeviceDescriptor(string Id, string Name, string Manufacturer, bool MetadataIncomplete = false);
public sealed record DeviceBatch(int HResult, List<DeviceDescriptor> Devices);
public sealed record DiscoveryResult(List<DeviceInfo> Devices, int WindowsDeviceCount, int MetadataFailures)
{
    public string Detail => Devices.Count > 0 ? $"{Devices.Count} dispositivo/i portatile/i disponibile/i."
        +(MetadataFailures > 0 ? " Alcuni dati identificativi sono incompleti: controlla il dispositivo selezionato." : "")
        : "Windows non espone dispositivi portatili. Sblocca il telefono, autorizza il collegamento e verifica il cavo dati. Su Android scegli Trasferimento file.";
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
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var d in batch.Devices)
                if(string.IsNullOrWhiteSpace(d.Id)||d.Id.Contains('\0')||!ids.Add(d.Id))
                    throw new IOException("Windows ha restituito identificatori USB mancanti o duplicati. Ricollega e ripeti la ricerca.");
            var devices = batch.Devices.Select(d => new DeviceInfo(d.Id,
                string.IsNullOrWhiteSpace(d.Name) ? "Dispositivo portatile USB" : d.Name,
                d.Id.Contains("vid_05ac", StringComparison.OrdinalIgnoreCase)
                    || d.Manufacturer.Contains("Apple", StringComparison.OrdinalIgnoreCase))).ToList();
            return new(devices, batch.Devices.Count, batch.Devices.Count(d => d.MetadataIncomplete));
        }
        throw new IOException("L'elenco USB continua a cambiare. Attendi il collegamento stabile e premi Cerca dispositivi.");
    }
}
