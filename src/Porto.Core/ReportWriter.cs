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
            .AppendLine($"Versione Porto: {report.ApplicationVersion}")
            .AppendLine($"Sorgente: {report.SourceDescription}")
            .AppendLine($"Contenuti scelti: {MediaLabel(report.Media)}; candidati foto: {report.SelectedPhotoCount}; candidati video: {report.SelectedVideoCount}")
            .AppendLine($"Inventario terminato: {(report.UsbEnumerationComplete ? "sì" : "no")}")
            .AppendLine($"Controllo finale inventario: {(report.FinalInventoryChecked ? "stabile" : "non verificato o cambiato")}; tentativo: {report.Attempt}");
        if(report.SelectedGallery)
        {
            text.AppendLine($"Elementi candidati nella selezione del catalogo: {report.CandidateCount}; copie della selezione verificate: {(report.GallerySelectionCopiesVerified?"sì":"no")}");
            if(report.GalleryCoverage is {} coverage)
                text.AppendLine(coverage.Note)
                    .AppendLine($"Catalogo: foto {coverage.Photos}; video {coverage.Videos}; non risolti {coverage.Unresolved}; Nascosti esclusi {coverage.HiddenExcluded}; Eliminati di recente esclusi {coverage.DeletedExcluded}.")
                    .AppendLine($"Snapshot atomico del catalogo verificato: {(coverage.AtomicSnapshotVerified?"sì":"no")}. Una lettura ripetuta non certifica l’intera libreria.");
            text.AppendLine("Live Photo: l’immagine è una foto; la componente animata non viene inclusa nella selezione Solo video. Gli originali non disponibili localmente restano pendenti/errori.")
                .AppendLine($"Elementi esclusi dal filtro foto/video: {report.ExcludedByMediaFilter}. Nomi e identificatori degli elementi esclusi non sono salvati nell’inventario della galleria.")
                .AppendLine("Le copie precedenti non riutilizzate restano al loro posto; questo non le identifica come Live Photo né ne autorizza la cancellazione.");
        }
        else if(report.SelectedFolder)
        {
            text.AppendLine($"File candidati nella cartella selezionata: {report.CandidateCount}; cartella verificata: {(report.SelectedFolderVerified?"sì":"no")}")
                .AppendLine("La selezione viene dalla cartella indicata dall’utente: Porto non può confermare che contenga tutti e soltanto gli elementi dell’album o della raccolta desiderata.")
                .AppendLine("Se desideri un album preciso, usa una sua esportazione di originali in una cartella separata. I tipi non scelti e i file accessori sono esclusi con motivazione; formati foto/video non supportati restano da verificare.")
                .AppendLine("Gli indizi USB di Live Photo o nomi modificati non escludono video da questa selezione. Gli altri file precedenti restano al loro posto.")
                .AppendLine("Il confronto usa byte e SHA-256, con una copia distinta per ogni file selezionato. Versioni con byte diversi non sono accorpate: possono rappresentare lo stesso video modificato o convertito.")
                .AppendLine($"Copie precedenti trovate: {report.PreviousCopies.Count}; riutilizzate: {report.PreviousCopies.Count(c=>c.ReusedForSelection)}; non riutilizzate: {report.PreviousCopies.Count(c=>!c.ReusedForSelection)}")
                .AppendLine("Non riutilizzata NON significa Live Photo né assente dalla selezione: può anche essere una copia ulteriore, una lettura ancora pendente o una versione diversa. Nessuna cancellazione o sovrascrittura.");
        }
        else
        {
            text.AppendLine($"Riavvii USB tentati: {report.UsbSessionRestarts}")
                .AppendLine($"Risorse USB totali (incluse cartelle): {report.UsbItemCount}; immagini: {report.ImageCount}; file accessori AAE: {report.SidecarCount}")
                .AppendLine($"Risorse foto/video selezionate USB: {report.CandidateCount}; indizi di foto abbinate/versioni modificate: {report.SuspectedCompanionCount}; candidati alla copia: {report.TransferCandidateCount}")
                .AppendLine("Questi conteggi riguardano risorse USB, non il numero degli elementi in un album dell’app Foto o Google Foto.")
                .AppendLine("Le risorse aggiuntive sono copiate e verificate separatamente: non vanno sommate come video autonomi di Foto.")
                .AppendLine("Copertura dell'intera libreria: NON VERIFICABILE via USB; elementi solo iCloud o altri cloud potrebbero non essere enumerati. Le cartelle USB non attestano gli album delle app.");
        }
        text.AppendLine("Originalità: byte ricevuti conservati senza conversioni; originale Foto non verificato indipendentemente.")
            .AppendLine($"Seconda lettura sorgente richiesta: {(report.SecondSourceRead?"sì":"no")}")
            .AppendLine("Integrità: SHA-256 dei byte ricevuti confrontato con rilettura del file salvato.");
        text.AppendLine("CONTEGGI DEL PASSAGGIO ATTUALE (lo storico è distinto, non cancellato)");
        foreach(var category in Enum.GetValues<Outcome>())text.AppendLine($"{Label(category)}: {report.CurrentCount(category)}");
        text.AppendLine($"Osservazioni storiche ancora da verificare: {report.HistoricalCount}")
            .AppendLine("Lo storico può contenere osservazioni dello stesso file con ID USB cambiati. Senza prove dei byte non può essere cancellato né contato come nuovi file distinti.")
            .AppendLine("Risorse video aggiuntive: copie integre con indizi di foto abbinate, modifiche o metadati temporizzati; nessuna esclusione basata solo sul nome.");
        if(report.SelectedFolder||report.SelectedGallery)
        {
            text.AppendLine("CONFRONTO DELLE COPIE PRECEDENTI (tutti i percorsi conservati)");
            foreach(var copy in report.PreviousCopies)text.AppendLine($"[{(copy.ReusedForSelection?"RIUTILIZZATA":copy.IntegrityVerified?"CONSERVATA, NON RIUTILIZZATA":"DA VERIFICARE")}] {copy.File} — {copy.Detail}");
        }
        foreach(string error in report.InventoryErrors)text.AppendLine($"INVENTARIO: {error}");
        foreach(var item in report.Items)text.AppendLine($"[{(item.Historical?"STORICO · ":"")}{Label(item.Outcome)}] {item.Name} — {item.Detail} {item.File}");
        WriteNew(Path.Combine(directory,$"report-{id}.txt"),text.ToString());
    }
    public static string MediaLabel(ImportMedia media)=>media switch
    {
        ImportMedia.Videos=>"Solo video",ImportMedia.Photos=>"Solo foto",ImportMedia.PhotosAndVideos=>"Foto e video",_=>"Scelta non valida"
    };
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
