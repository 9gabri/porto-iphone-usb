using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Porto.Core;

public static class ReportWriter
{
    public static readonly JsonSerializerOptions JsonOptions=new(){WriteIndented=true,Converters={new JsonStringEnumConverter()}};
    public static void Write(string directory,string id,ImportReport report)
    {
        Directory.CreateDirectory(directory);
        WriteNew(Path.Combine(directory,$"report-{id}.json"),JsonSerializer.Serialize(report,JsonOptions));
        StringBuilder text=new();text.AppendLine("PORTO — REPORT DI IMPORTAZIONE").AppendLine(report.Status)
            .AppendLine($"Inventario USB terminato: {(report.UsbEnumerationComplete ? "sì" : "no")}")
            .AppendLine("Copertura dell'intera libreria: NON VERIFICABILE via USB; elementi solo iCloud potrebbero non essere enumerati.")
            .AppendLine("Originalità: byte ricevuti conservati senza conversioni; originale Foto non verificato indipendentemente.")
            .AppendLine($"Seconda lettura sorgente richiesta: {(report.SecondSourceRead?"sì":"no")}")
            .AppendLine("Integrità: SHA-256 dei byte ricevuti confrontato con rilettura del file salvato.");
        foreach(var category in Enum.GetValues<Outcome>())text.AppendLine($"{Label(category)}: {report.Count(category)}");
        foreach(string error in report.InventoryErrors)text.AppendLine($"INVENTARIO: {error}");
        foreach(var item in report.Items)text.AppendLine($"[{Label(item.Outcome)}] {item.Name} — {item.Detail} {item.File}");
        WriteNew(Path.Combine(directory,$"report-{id}.txt"),text.ToString());
    }
    private static void WriteNew(string path,string contents)
    {
        Destination.CheckAncestors(path);
        using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read);
        using var writer=new StreamWriter(stream,new UTF8Encoding(false));writer.Write(contents);writer.Flush();stream.Flush(true);
    }
    public static string Label(Outcome outcome)=>outcome switch{
        Outcome.Imported=>"Importati",Outcome.AlreadyPresent=>"Già presenti verificati",Outcome.Excluded=>"Esclusi",
        Outcome.Ambiguous=>"Ambigui",Outcome.Missing=>"Mancanti noti",_=>"Errori"};
}
