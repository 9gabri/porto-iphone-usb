using System.Diagnostics;

namespace Porto.Core;

public sealed class ImportEngine(IVideoSource source,Destination destination,IDiskSpace? disk=null)
{
    private readonly IDiskSpace space=disk??new LocalDiskSpace();
    public Action<string>? FaultPoint { get; set; }
    public Action<ImportProgress>? Progress { get; set; }
    public string? ReportPath { get; private set; }
    private long? totalBytes;
    private long copied;
    private int done,total;
    private readonly Stopwatch timer=new();
    private long lastNotify;

    public ImportReport Run(bool deep,CancellationToken cancel)
    {
        using var catalog=new Catalog(destination);
        string runId=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..8];
        var report=new ImportReport{SecondSourceRead=deep};
        timer.Restart();
        try
        {
            Notify("Inventario USB", "Lettura dell'elenco, prima di copiare",true);
            var inventory=source.Inventory(cancel);
            report.UsbEnumerationComplete=inventory.EnumerationComplete;
            report.InventoryErrors=inventory.Errors;
            // Save the inventory result before any transfer. Each item has an explicit pending category.
            catalog.SaveReport(runId+"-inventory",new ImportReport{
                Status=$"Inventario: {inventory.Items.Count} elementi; candidati {inventory.Items.Count(MediaInspector.Candidate)}",
                UsbEnumerationComplete=inventory.EnumerationComplete,InventoryErrors=inventory.Errors});
            string invPath=destination.Resolve(Path.Combine(".porto",$"inventory-{runId}.json"));
            using(var inv=new FileStream(invPath,FileMode.CreateNew,FileAccess.Write,FileShare.Read))
            {System.Text.Json.JsonSerializer.Serialize(inv,inventory);inv.Flush(true);}
            Recover(catalog,cancel);
            var entries=catalog.Entries();
            for(int index=0;index<entries.Count;index++)
            {
                var prior=entries[index];
                if(prior.State=="Complete"&&!File.Exists(destination.Resolve(prior.Final)))
                {
                    entries[index]=prior with{State="Missing"};catalog.Save(entries[index]);
                    report.Items.Add(new(prior.Source.Id,prior.Source.Name,Outcome.Missing,"Una copia registrata non è più presente nella destinazione. Se la sorgente è disponibile verrà ricopiata.",prior.Final));
                }
                else if(prior.State=="Damaged")report.Items.Add(new(prior.Source.Id,prior.Source.Name,Outcome.Error,"Copia precedente danneggiata o collisionante; conservata. Recupero tramite nuova copia se disponibile.",prior.Final));
            }
            var known=entries.Where(e=>e.State is "Complete" or "Quarantined").ToList();
            var byHash=known.GroupBy(e=>e.Hash!).ToDictionary(g=>g.Key,g=>g.ToList(),StringComparer.Ordinal);
            var knownLengths=known.Select(e=>e.Length).ToHashSet();
            var claimed=new HashSet<string>(StringComparer.Ordinal);
            var companions=MediaInspector.SuspectedCompanions(inventory.Items);
            var candidates=inventory.Items.Where(MediaInspector.Candidate).ToList();
            total=candidates.Count;
            totalBytes=candidates.All(i=>i.Size.HasValue)?candidates.Aggregate(0L,(n,i)=>checked(n+i.Size!.Value)):null;
            // Reserve only a lower-bound estimate for genuinely unknown lengths; repeated content is hashed before writing.
            long required=candidates.Where(i=>i.Size.HasValue&&!knownLengths.Contains(i.Size.Value)&&!companions.ContainsKey(i.Id))
                .Aggregate(VerifiedCopy.ReserveBytes,(n,i)=>checked(n+i.Size!.Value));
            Notify("Inventario pronto",$"{total:N0} video candidati; spazio prudenziale {required:N0} byte",true);
            EnsureSpace(required);
            foreach(var item in inventory.Items)
            {
                cancel.ThrowIfCancellationRequested();
                if(!MediaInspector.Candidate(item))
                {
                    if(item.Kind!=MediaKind.Container)
                    {
                        bool uncertain=item.Kind==MediaKind.Unknown;
                        report.Items.Add(new(item.Id,item.Name,uncertain?Outcome.Ambiguous:Outcome.Excluded,
                            uncertain?"Metadati insufficienti: tipo non riconosciuto.":"Fotografia o contenuto non video."));
                    }
                    continue;
                }
                if(item.Offline){report.Items.Add(new(item.Id,item.Name,Outcome.Missing,"Originale segnalato non disponibile offline."));done++;continue;}
                if(companions.ContainsKey(item.Id))
                {
                    report.Items.Add(new(item.Id,item.Name,Outcome.Ambiguous,"Possibile Live Photo o versione modificata: non importato automaticamente."));done++;continue;
                }
                Notify("Importazione",item.Name,true);
                try
                {
                    CopyResult? fingerprint=null;
                    if(!item.Size.HasValue||knownLengths.Contains(item.Size.Value))
                    {
                        using var input=source.OpenRead(item,cancel);
                        fingerprint=VerifiedCopy.Hash(input,cancel,n=>Notify("Confronto contenuto USB",item.Name,false,n));
                        if(item.Size.HasValue&&fingerprint.Length!=item.Size.Value)throw new InvalidDataException("Dimensione USB incoerente.");
                        CatalogEntry? match=null;
                        if(byHash.TryGetValue(fingerprint.Hash,out var matching))
                        {
                            foreach(var old in matching.Where(e=>!claimed.Contains(e.Id)&&e.Length==fingerprint.Length))
                            {
                                string oldPath=destination.Resolve(old.State=="Quarantined"?old.Temp:old.Final);
                                if(!File.Exists(oldPath)){catalog.Save(old with{State="Missing"});continue;}
                                var saved=VerifiedCopy.HashFile(oldPath,cancel,n=>Notify("Verifica file già presente",item.Name,false,n));
                                if(saved==fingerprint){match=old;break;}
                                catalog.Save(old with{State="Damaged"});
                                report.Items.Add(new(old.Source.Id,old.Source.Name,Outcome.Error,"Copia precedente alterata; conservata senza sovrascrittura.",old.Final));
                            }
                        }
                        if(match!=null)
                        {
                            if(deep){using var second=source.OpenRead(item,cancel);VerifiedCopy.Verify(fingerprint,VerifiedCopy.Hash(second,cancel,n=>Notify("Seconda lettura USB",item.Name,false,n)));}
                            claimed.Add(match.Id);
                            report.Items.Add(match.State=="Quarantined"
                                ?new(item.Id,item.Name,Outcome.Ambiguous,"Elemento ambiguo già conservato in .porto, verificato senza creare altre copie.",match.Temp,fingerprint.Length,fingerprint.Hash)
                                :new(item.Id,item.Name,Outcome.AlreadyPresent,"Sorgente e copia locale rilette: SHA-256 identico.",match.Final,fingerprint.Length,fingerprint.Hash));
                            done++;SaveProgress();continue;
                        }
                    }
                    EnsureSpace(checked((item.Size??0)+VerifiedCopy.ReserveBytes));
                    string id=Guid.NewGuid().ToString("N");
                    var entry=new CatalogEntry(id,"Copying",Path.Combine(".porto",id+".part"),"",null,0,item);
                    catalog.Save(entry);FaultPoint?.Invoke("after-intent");
                    string temp=destination.Resolve(entry.Temp);
                    CopyResult received;
                    using(var input=source.OpenRead(item,cancel))
                    using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,VerifiedCopy.BufferSize,FileOptions.SequentialScan))
                    {
                        long start=copied;
                        received=VerifiedCopy.Copy(input,output,item.Size,n=>{copied=start+n;Notify("Copia",item.Name,false);},
                            ()=>EnsureSpace(VerifiedCopy.ReserveBytes+VerifiedCopy.BufferSize),cancel);
                        output.Flush(true);FaultPoint?.Invoke("after-flush");
                    }
                    VerifiedCopy.Verify(received,VerifiedCopy.HashFile(temp,cancel,n=>Notify("Verifica locale SHA-256",item.Name,false,n)));
                    if(fingerprint!=null)VerifiedCopy.Verify(fingerprint,received);
                    if(deep)
                    {
                        using var second=source.OpenRead(item,cancel);
                        VerifiedCopy.Verify(received,VerifiedCopy.Hash(second,cancel,n=>Notify("Seconda lettura USB",item.Name,false,n)));
                    }
                    FaultPoint?.Invoke("after-verification");
                    var media=MediaInspector.Inspect(temp,cancel);
                    if(media.LivePhoto||media.Ambiguous||!media.IsVideo)
                    {
                        entry=entry with{State="Quarantined",Hash=received.Hash,Length=received.Length};catalog.Save(entry);
                        knownLengths.Add(entry.Length);
                        if(!byHash.TryGetValue(entry.Hash!,out var quarantined))byHash[entry.Hash!]=quarantined=[];
                        quarantined.Add(entry);claimed.Add(entry.Id);
                        report.Items.Add(new(item.Id,item.Name,media.LivePhoto?Outcome.Excluded:Outcome.Ambiguous,
                            media.Detail+" Temporaneo conservato in .porto.",entry.Temp,received.Length,received.Hash));
                        done++;SaveProgress();continue;
                    }
                    string ext=Path.GetExtension(item.Name);
                    if(!MediaInspector.VideoExtensions.Contains(ext))ext=media.Extension;
                    entry=entry with{State="Ready",Hash=received.Hash,Length=received.Length,
                        Final=Path.Combine("Video Porto",Destination.SafeStem(item.Name)+"_"+id+ext.ToLowerInvariant())};
                    catalog.Save(entry);FaultPoint?.Invoke("after-ready");
                    Destination.Publish(temp,destination.Resolve(entry.Final));FaultPoint?.Invoke("after-rename");
                    entry=entry with{State="Complete"};catalog.Save(entry);FaultPoint?.Invoke("after-complete");
                    knownLengths.Add(entry.Length);
                    if(!byHash.TryGetValue(entry.Hash!,out var bucket))byHash[entry.Hash!]=bucket=[];
                    bucket.Add(entry);claimed.Add(entry.Id);
                    report.Items.Add(new(item.Id,item.Name,Outcome.Imported,media.Detail,entry.Final,entry.Length,entry.Hash));
                    done++;SaveProgress();
                }
                catch(OperationCanceledException){throw;}
                catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidDataException)
                {
                    report.Items.Add(new(item.Id,item.Name,Outcome.Error,ex.Message));
                    report.Status="Importazione in pausa per errore. Correggi la causa e premi Riprendi.";
                    return report; // bounded attempts: one attempt per user resume, no unattended retry storm.
                }
            }
            report.Status=report.UsbEnumerationComplete && report.InventoryErrors.Count==0 &&
                !report.Items.Any(i=>i.Outcome is Outcome.Error or Outcome.Ambiguous or Outcome.Missing)
                ?"Completati tutti i video individuati tramite USB. Intera libreria non verificabile."
                :"Importazione incompleta o con elementi non verificabili. Consulta il report.";
            return report;
            void SaveProgress()=>catalog.AppendResult(runId,report.Items[^1]);
        }
        catch(OperationCanceledException){report.Status="In pausa. I file incompleti saranno ricopiati dall'inizio.";return report;}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidDataException or OverflowException)
        {report.InventoryErrors.Add(ex.Message);report.Status="Importazione in pausa: "+ex.Message;return report;}
        finally
        {
            report.EndedUtc=DateTimeOffset.UtcNow;
            // If persistence fails (e.g. disk completely full), surface it instead of claiming success.
            catalog.SaveReport(runId,report);
            string reports=destination.Resolve("Report Porto");ReportWriter.Write(reports,runId,report);
            ReportPath=Path.Combine(reports,$"report-{runId}.txt");
            Notify(report.Status,"Report salvato",true);
        }
    }

    public void Recover(Catalog catalog,CancellationToken cancel)
    {
        foreach(var entry in catalog.Entries().Where(e=>e.State=="Ready"))
        {
            cancel.ThrowIfCancellationRequested();
            string final=destination.Resolve(entry.Final),temp=destination.Resolve(entry.Temp);
            var expected=new CopyResult(entry.Length,entry.Hash!);
            if(File.Exists(final))
            {
                if(expected==VerifiedCopy.HashFile(final,cancel))catalog.Save(entry with{State="Complete"});
                else if(File.Exists(temp)&&expected==VerifiedCopy.HashFile(temp,cancel))
                {
                    var alternate=entry with{Final=Path.Combine("Video Porto",Destination.SafeStem(entry.Source.Name)+"_"+Guid.NewGuid().ToString("N")+Path.GetExtension(entry.Final))};
                    catalog.Save(alternate);
                    Destination.Publish(temp,destination.Resolve(alternate.Final));catalog.Save(alternate with{State="Complete"});
                }
                else catalog.Save(entry with{State="Damaged"});
            }
            else if(File.Exists(temp))
            {
                if(expected==VerifiedCopy.HashFile(temp,cancel))
                {Destination.Publish(temp,final);catalog.Save(entry with{State="Complete"});}
                else catalog.Save(entry with{State="Damaged"});
            }
            else catalog.Save(entry with{State="Missing"});
        }
    }
    private void EnsureSpace(long required)
    {
        if(space.Available(destination.Root)<required)throw new SpaceException("Spazio insufficiente. Libera spazio sul PC o scegli un'altra destinazione; nessun video viene eliminato.");
    }
    private void Notify(string phase,string name,bool force,long? activityBytes=null)
    {
        if(!force&&timer.ElapsedMilliseconds-lastNotify<200)return;
        lastNotify=timer.ElapsedMilliseconds;
        Progress?.Invoke(new(phase,name,done,total,copied,totalBytes,space.Available(destination.Root),
            timer.Elapsed.TotalSeconds>0?copied/timer.Elapsed.TotalSeconds:0));
    }
}
