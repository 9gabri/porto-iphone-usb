using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Porto.Core;

public static class ReportWriter
{
    public static readonly JsonSerializerOptions JsonOptions=new(){WriteIndented=true,Converters={new JsonStringEnumConverter()}};
    public static void Write(string directory,string id,ImportReport report)
    {
        Destination.CheckAncestors(directory);
        Directory.CreateDirectory(directory);
        WriteNew(Path.Combine(directory,$"report-{id}.json"),JsonSerializer.Serialize(report,JsonOptions));
        StringBuilder text=new();text.AppendLine("PORTO — REPORT DI IMPORTAZIONE").AppendLine(report.Status)
            .AppendLine($"Versione Porto: {report.ApplicationVersion}; riavvii USB tentati in questo passaggio: {report.UsbSessionRestarts}")
            .AppendLine($"Inventario USB terminato: {(report.UsbEnumerationComplete ? "sì" : "no")}")
            .AppendLine($"Controllo finale inventario: {(report.FinalInventoryChecked ? "stabile" : "non verificato o cambiato")}; tentativo: {report.Attempt}")
            .AppendLine($"Risorse USB totali (incluse cartelle): {report.UsbItemCount}; immagini: {report.ImageCount}; file accessori AAE: {report.SidecarCount}")
            .AppendLine($"Risorse video USB: {report.CandidateCount}; indizi di foto abbinate/versioni modificate: {report.SuspectedCompanionCount}; candidati alla copia: {report.TransferCandidateCount}")
            .AppendLine("Questi conteggi riguardano risorse USB, non il numero dei video autonomi nell'app Foto.")
            .AppendLine("Importati e già presenti verificati si riferiscono a questo passaggio. Le risorse aggiuntive sono copiate e verificate separatamente: non vanno sommate come video autonomi di Foto.")
            .AppendLine("Copertura dell'intera libreria: NON VERIFICABILE via USB; elementi solo iCloud potrebbero non essere enumerati.")
            .AppendLine("Originalità: byte ricevuti conservati senza conversioni; originale Foto non verificato indipendentemente.")
            .AppendLine($"Seconda lettura sorgente richiesta: {(report.SecondSourceRead?"sì":"no")}")
            .AppendLine("Integrità: SHA-256 dei byte ricevuti confrontato con rilettura del file salvato.");
        text.AppendLine("CONTEGGI DEL PASSAGGIO ATTUALE (lo storico è distinto, non cancellato)");
        foreach(var category in Enum.GetValues<Outcome>())text.AppendLine($"{Label(category)}: {report.CurrentCount(category)}");
        text.AppendLine($"Osservazioni storiche ancora da verificare: {report.HistoricalCount}")
            .AppendLine("Lo storico può contenere osservazioni dello stesso file con ID USB cambiati. Senza prove dei byte non può essere cancellato né contato come nuovi video distinti.")
            .AppendLine("Risorse video aggiuntive: copie integre con indizi di foto abbinate, modifiche o metadati temporizzati; nessuna esclusione basata solo sul nome.");
        foreach(string error in report.InventoryErrors)text.AppendLine($"INVENTARIO: {error}");
        foreach(var item in report.Items)text.AppendLine($"[{(item.Historical?"STORICO · ":"")}{Label(item.Outcome)}] {item.Name} — {item.Detail} {item.File}");
        WriteNew(Path.Combine(directory,$"report-{id}.txt"),text.ToString());
    }
    private static void WriteNew(string path,string contents)
    {
        AtomicFile.WriteNew(path,stream=>
        {
            using var writer=new StreamWriter(stream,new UTF8Encoding(false),leaveOpen:true);
            writer.Write(contents);writer.Flush();
        });
    }
    public static string Label(Outcome outcome)=>outcome switch{
        Outcome.Imported=>"Importati",Outcome.AlreadyPresent=>"Già presenti verificati",Outcome.Excluded=>"Esclusi",
        Outcome.Additional=>"Risorse aggiuntive copiate",Outcome.AdditionalAlreadyPresent=>"Risorse aggiuntive già verificate",
        Outcome.Ambiguous=>"Ambigui",Outcome.Missing=>"Mancanti noti",Outcome.Pending=>"Pendenti aggiuntivi",_=>"Errori"};
}
