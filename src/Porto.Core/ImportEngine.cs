using System.Diagnostics;

namespace Porto.Core;

public sealed class ImportEngine(IVideoSource source,Destination destination,IDiskSpace? disk=null)
{
    private readonly IDiskSpace space=disk??new LocalDiskSpace();
    public Action<string>? FaultPoint { get; set; }
    public Action<ImportProgress>? Progress { get; set; }
    public string? ReportPath { get; private set; }
    public int Attempt { get; set; } = 1;
    private long? totalBytes;
    private long copied;
    private int done,total;
    private readonly Stopwatch timer=new();
    private long lastNotify;

    public ImportReport Run(bool deep,CancellationToken cancel)
    {
        using var catalog=new Catalog(destination);
        using var ledger=new WorkLedger(destination);
        string runId=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..8];
        var report=new ImportReport{SecondSourceRead=deep,Attempt=Attempt};
        var processed=new HashSet<string>(StringComparer.Ordinal);
        var resolvedMissingFiles=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var verifiedCopies=new Dictionary<CopyResult,HashSet<string>>();
        var reconciledContents=new HashSet<CopyResult>();
        var reportedDeficits=new HashSet<CopyResult>();
        var reportedConflicts=new HashSet<string>(StringComparer.Ordinal);
        bool mayRetryDeficits=true;
        List<SourceItem> observed=[];
        copied=0;done=0;total=0;totalBytes=null;lastNotify=0;
        timer.Restart();
        ledger.Migrate(catalog.Entries());
        try
        {
            Notify("Inventario USB", "Lettura dell'elenco, prima di copiare",true);
            var inventory=source.Inventory(cancel);
            observed=inventory.Items;
            var observedKeys=observed.Select(WorkLedger.Key).ToHashSet(StringComparer.Ordinal);
            ledger.Observe(observed.Where(MediaInspector.Candidate),runId+"-initial");
            report.UsbEnumerationComplete=inventory.EnumerationComplete;
            report.InventoryErrors=inventory.Errors;
            // Save the inventory result before any transfer. Each item has an explicit pending category.
            catalog.SaveReport(runId+"-inventory",new ImportReport{
                Status=$"Inventario: {inventory.Items.Count} elementi; candidati {inventory.Items.Count(MediaInspector.Candidate)}",
                UsbEnumerationComplete=inventory.EnumerationComplete,InventoryErrors=inventory.Errors});
            string invPath=destination.Resolve(Path.Combine(".porto",$"inventory-{runId}.json"));
            AtomicFile.WriteNew(invPath,inv=>System.Text.Json.JsonSerializer.Serialize(inv,inventory));
            Recover(catalog,cancel);
            var entries=catalog.Entries();
            for(int index=0;index<entries.Count;index++)
            {
                var prior=entries[index];
                if(prior.State=="Missing"&&!string.IsNullOrEmpty(prior.Final)&&File.Exists(destination.Resolve(prior.Final))&&
                    new CopyResult(prior.Length,prior.Hash!)==VerifiedCopy.HashFile(destination.Resolve(prior.Final),cancel))
                {prior=prior with{State="Complete"};entries[index]=prior;catalog.Save(prior);}
                if(prior.State=="Missing" || prior.State=="Complete"&&!File.Exists(destination.Resolve(prior.Final)))
                {
                    entries[index]=prior with{State="Missing"};catalog.Save(entries[index]);
                    report.Items.Add(new(prior.Source.Id,prior.Source.Name,Outcome.Missing,"Una copia registrata non è più presente nella destinazione. Se la sorgente è disponibile verrà ricopiata.",prior.Final));
                }
                else if(prior.State=="Damaged")report.Items.Add(new(prior.Source.Id,prior.Source.Name,Outcome.Error,"Copia precedente danneggiata o collisionante; conservata. Recupero tramite nuova copia se disponibile.",prior.Final));
            }
            var known=entries.Where(e=>e.State is "Complete" or "Quarantined").ToList();
            var missing=entries.Where(e=>e.State is "Missing" or "Damaged").GroupBy(e=>(e.Hash,e.Length)).ToDictionary(g=>g.Key,g=>new Queue<CatalogEntry>(g));
            var replacementIds=missing.Values.SelectMany(queue=>queue).Select(entry=>entry.Id).ToHashSet(StringComparer.Ordinal);
            CatalogEntry? MissingMatch(string? hash,long length)=>missing.TryGetValue((hash,length),out var queue)&&queue.TryPeek(out var old)?old:null;
            var groups=known.GroupBy(e=>new CopyResult(e.Length,e.Hash!)).ToDictionary(g=>g.Key,g=>g.ToList());
            var byHash=groups.ToDictionary(pair=>pair.Key,pair=>new Queue<CatalogEntry>(pair.Value));
            var knownLengths=known.Select(e=>e.Length).ToHashSet();
            var availableLengths=known.GroupBy(e=>e.Length).ToDictionary(g=>g.Key,g=>g.Count());
            int availableCount=known.Count;
            bool VerifyLocal(CatalogEntry old,CopyResult content)
            {
                string oldRelative=old.State=="Quarantined"?old.Temp:old.Final;
                string oldPath=destination.Resolve(oldRelative);
                bool exists=File.Exists(oldPath);
                if(exists&&VerifiedCopy.HashFile(oldPath,cancel,n=>Notify("Verifica file già presente",old.Source.Name,false,n))==content)return true;
                var broken=old with{State=exists?"Damaged":"Missing"};
                catalog.Save(broken);
                if(verifiedCopies.TryGetValue(content,out var verified))verified.Remove(old.Id);
                if(replacementIds.Add(old.Id))
                {
                    if(!missing.TryGetValue((old.Hash,old.Length),out var replacements))missing[(old.Hash,old.Length)]=replacements=new();
                    replacements.Enqueue(broken);
                    report.Items.Add(new(old.Source.Id,old.Source.Name,exists?Outcome.Error:Outcome.Missing,
                        exists?"Copia precedente alterata; conservata senza sovrascrittura.":"Una copia registrata non è più presente nella destinazione.",oldRelative));
                }
                return false;
            }
            HashSet<string> VerifyGroup(CopyResult content)
            {
                if(verifiedCopies.TryGetValue(content,out var verified))return verified;
                verified=new(StringComparer.Ordinal);
                foreach(var old in groups.GetValueOrDefault(content)??[])
                    if(VerifyLocal(old,content))verified.Add(old.Id);
                verifiedCopies.Add(content,verified);
                return verified;
            }
            void VerifySourceReads(SourceItem item,CopyResult first,CopyResult second)
            {
                // Preserve both full-read observations before throwing on disagreement.
                ledger.Remember(item,first);ledger.Remember(item,second);
                VerifiedCopy.Verify(first,second);
            }
            void ReconcileContent(CopyResult content)
            {
                if(VerifyGroup(content).Count>=ledger.RequiredCopies(content)&&reconciledContents.Add(content))
                    ledger.ReconcileKnown(content,observedKeys);
            }
            var partials=entries.Where(e=>e.State=="Copying"&&e.Temp==Path.Combine(".porto",e.Id+".part")&&File.Exists(destination.Resolve(e.Temp)))
                .GroupBy(e=>WorkLedger.Key(e.Source)).ToDictionary(g=>g.Key,g=>g.OrderByDescending(e=>new FileInfo(destination.Resolve(e.Temp)).Length).First());
            long Remaining(SourceItem item)
            {
                if(!item.Size.HasValue)return 0;
                long prefix=partials.TryGetValue(WorkLedger.Key(item),out var part)?new FileInfo(destination.Resolve(part.Temp)).Length:0;
                return Math.Max(0,item.Size.Value-prefix);
            }
            var companions=MediaInspector.SuspectedCompanions(inventory.Items);
            var candidates=inventory.Items.Where(MediaInspector.Candidate).ToList();
            total=candidates.Count;
            report.CandidateCount=total;
            totalBytes=candidates.All(i=>i.Size.HasValue)?candidates.Aggregate(0L,(n,i)=>checked(n+i.Size!.Value)):null;
            // Reserve only a lower-bound estimate for genuinely unknown lengths; repeated content is hashed before writing.
            long required=candidates.Where(i=>!i.Offline&&i.Size.HasValue&&!knownLengths.Contains(i.Size.Value)&&!companions.ContainsKey(i.Id))
                .Aggregate(VerifiedCopy.ReserveBytes,(n,i)=>checked(n+Remaining(i)));
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
                if(item.Offline){report.Items.Add(new(item.Id,item.Name,Outcome.Missing,"Originale segnalato non disponibile offline."));done++;SaveProgress(item);continue;}
                if(companions.ContainsKey(item.Id))
                {
                    report.Items.Add(new(item.Id,item.Name,Outcome.Ambiguous,"Possibile Live Photo o versione modificata: non importato automaticamente."));done++;SaveProgress(item);continue;
                }
                Notify("Importazione",item.Name,true);
                try
                {
                    CopyResult? fingerprint=null;
                    if(item.Size.HasValue?availableLengths.GetValueOrDefault(item.Size.Value)>0:availableCount>0)
                    {
                        using var input=source.OpenRead(item,cancel);
                        fingerprint=VerifiedCopy.Hash(input,cancel,n=>Notify("Confronto contenuto USB",item.Name,false,n));
                        if(item.Size.HasValue&&fingerprint.Length!=item.Size.Value)throw new InvalidDataException("Dimensione USB incoerente.");
                        ledger.Remember(item,fingerprint);
                        var verified=VerifyGroup(fingerprint);
                        CatalogEntry? match=null;
                        // One copy cannot discharge two videos that were ever observed together.
                        // Leave the existing queue untouched when another physical copy is needed.
                        if(verified.Count>=ledger.RequiredCopies(fingerprint)&&byHash.TryGetValue(fingerprint,out var matching))
                        {
                            while(matching.TryDequeue(out var old))
                            {
                                availableCount--;availableLengths[old.Length]--;
                                if(!verified.Contains(old.Id))continue;
                                // A group count can be cached, but the file actually being reused
                                // must still match now, after possibly hours of other transfers.
                                if(VerifyLocal(old,fingerprint)){match=old;break;}
                                if(verified.Count<ledger.RequiredCopies(fingerprint))break;
                            }
                        }
                        if(match!=null)
                        {
                            if(deep){using var second=source.OpenRead(item,cancel);VerifySourceReads(item,fingerprint,VerifiedCopy.Hash(second,cancel,n=>Notify("Seconda lettura USB",item.Name,false,n)));}
                            ledger.ReadVerified(item,fingerprint);
                            catalog.Save(match with{Source=item});
                            ReconcileContent(fingerprint);
                            report.Items.Add(match.State=="Quarantined"
                                ?new(item.Id,item.Name,Outcome.Ambiguous,"Elemento ambiguo già conservato in .porto, verificato senza creare altre copie.",match.Temp,fingerprint.Length,fingerprint.Hash)
                                :new(item.Id,item.Name,Outcome.AlreadyPresent,"Sorgente e copia locale rilette: SHA-256 identico.",match.Final,fingerprint.Length,fingerprint.Hash));
                            done++;SaveProgress(item);continue;
                        }
                    }
                    EnsureSpace(checked(Remaining(item)+VerifiedCopy.ReserveBytes));
                    string id=Guid.NewGuid().ToString("N");
                    bool resuming=partials.TryGetValue(WorkLedger.Key(item),out var partial);
                    var entry=resuming?partial!:new CatalogEntry(id,"Copying",Path.Combine(".porto",id+".part"),"",null,0,item);
                    id=entry.Id;
                    catalog.Save(entry);FaultPoint?.Invoke("after-intent");
                    string temp=destination.Resolve(entry.Temp);
                    CopyResult received;
                    using(var input=source.OpenRead(item,cancel))
                    using(var output=new FileStream(temp,resuming?FileMode.Open:FileMode.CreateNew,resuming?FileAccess.ReadWrite:FileAccess.Write,FileShare.None,VerifiedCopy.BufferSize,FileOptions.SequentialScan))
                    {
                        long start=copied;
                        try
                        {
                            received=resuming?VerifiedCopy.Resume(input,output,item.Size,n=>{copied=start+n;Notify("Ripresa verificata",item.Name,false);},
                                ()=>EnsureSpace(VerifiedCopy.ReserveBytes+VerifiedCopy.BufferSize),cancel)
                                :VerifiedCopy.Copy(input,output,item.Size,n=>{copied=start+n;Notify("Copia",item.Name,false);},
                                ()=>EnsureSpace(VerifiedCopy.ReserveBytes+VerifiedCopy.BufferSize),cancel);
                        }
                        catch(PartialMismatchException){catalog.Save(entry with{State="Abandoned"});throw;}
                        output.Flush(true);FaultPoint?.Invoke("after-flush");
                    }
                    VerifiedCopy.Verify(received,VerifiedCopy.HashFile(temp,cancel,n=>Notify("Verifica locale SHA-256",item.Name,false,n)));
                    if(fingerprint!=null)VerifySourceReads(item,fingerprint,received);
                    if(deep)
                    {
                        using var second=source.OpenRead(item,cancel);
                        VerifySourceReads(item,received,VerifiedCopy.Hash(second,cancel,n=>Notify("Seconda lettura USB",item.Name,false,n)));
                    }
                    ledger.ReadVerified(item,received);
                    VerifyGroup(received);
                    FaultPoint?.Invoke("after-verification");
                    var media=MediaInspector.Inspect(temp,cancel);
                    if(media.LivePhoto||media.Ambiguous||!media.IsVideo)
                    {
                        entry=entry with{State="Quarantined",Hash=received.Hash,Length=received.Length};SaveFinalEntry(entry);ReconcileContent(received);
                        report.Items.Add(new(item.Id,item.Name,media.LivePhoto?Outcome.Excluded:Outcome.Ambiguous,
                            media.Detail+" Temporaneo conservato in .porto.",entry.Temp,received.Length,received.Hash));
                        done++;SaveProgress(item);continue;
                    }
                    string ext=Path.GetExtension(item.Name);
                    if(!MediaInspector.VideoExtensions.Contains(ext))ext=media.Extension;
                    entry=entry with{State="Ready",Hash=received.Hash,Length=received.Length,
                        Replaces=MissingMatch(received.Hash,received.Length)?.Id,
                        Final=Path.Combine("Video Porto",Destination.SafeStem(item.Name)+"_"+id+ext.ToLowerInvariant())};
                    catalog.Save(entry);FaultPoint?.Invoke("after-ready");
                    Destination.Publish(temp,destination.Resolve(entry.Final));FaultPoint?.Invoke("after-rename");
                    entry=entry with{State="Complete"};SaveFinalEntry(entry);ReconcileContent(received);FaultPoint?.Invoke("after-complete");
                    report.Items.Add(new(item.Id,item.Name,Outcome.Imported,media.Detail,entry.Final,entry.Length,entry.Hash));
                    done++;SaveProgress(item);
                }
                catch(OperationCanceledException){throw;}
                catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidDataException)
                {
                    report.Items.Add(new(item.Id,item.Name,Outcome.Error,ex.Message));
                    done++;SaveProgress(item);report.RetryRecommended=true;
                    if(ex is SpaceException or UnauthorizedAccessException or SourceUnavailableException)
                    {
                        report.StoppedForCapacity=ex is SpaceException;
                        if(report.StoppedForCapacity||ex is UnauthorizedAccessException){report.RetryRecommended=false;mayRetryDeficits=false;}
                        report.Status="Importazione in pausa: collegamento o destinazione da verificare.";
                        return report;
                    }
                }
            }
            Notify("Controllo finale", "Verifico che l'elenco dei video USB non sia cambiato",true);
            var finalInventory=source.Inventory(cancel);
            report.FinalInventoryChecked=finalInventory.EnumerationComplete&&finalInventory.Errors.Count==0&&
                inventory.Items.Select(WorkLedger.Key).Order(StringComparer.Ordinal).SequenceEqual(finalInventory.Items.Select(WorkLedger.Key).Order(StringComparer.Ordinal));
            if(!report.FinalInventoryChecked)
            {
                report.InventoryErrors.Add("L'inventario finale è cambiato o incompleto. È necessario un altro passaggio.");
                report.InventoryErrors.AddRange(finalInventory.Errors);report.RetryRecommended=true;
                var initialKeys=inventory.Items.Select(WorkLedger.Key).ToHashSet(StringComparer.Ordinal);
                var newKeys=finalInventory.Items.Select(WorkLedger.Key).Where(key=>!initialKeys.Contains(key)).ToHashSet(StringComparer.Ordinal);
                ledger.Observe(finalInventory.Items.Where(MediaInspector.Candidate),runId+"-final",newKeys);
            }
            if(!report.UsbEnumerationComplete||report.InventoryErrors.Count>0)report.RetryRecommended=true;
            AddOutstanding();
            report.Status=total>0 && report.FinalInventoryChecked && report.UsbEnumerationComplete && report.InventoryErrors.Count==0 &&
                !report.Items.Any(i=>i.Outcome is Outcome.Error or Outcome.Ambiguous or Outcome.Missing or Outcome.Pending)
                ?"Completati tutti i video individuati tramite USB. Intera libreria non verificabile."
                :"Importazione incompleta o con elementi non verificabili. Consulta il report.";
            if(total==0&&report.Items.Count==0&&report.InventoryErrors.Count==0)report.Status="Nessun video individuato via USB. Verifica telefono e contenuti disponibili localmente.";
            return report;
            void SaveProgress(SourceItem item)
            {catalog.AppendResult(runId,report.Items[^1]);ledger.Record(item,report.Items[^1]);processed.Add(WorkLedger.Key(item));}
            void SaveFinalEntry(CatalogEntry replacement)
            {
                var old=MissingMatch(replacement.Hash,replacement.Length);
                if(old==null)catalog.Save(replacement);
                else
                {
                    catalog.SaveAll(replacement with{Replaces=old.Id},old with{State="Replaced"});missing[(old.Hash,old.Length)].Dequeue();
                    resolvedMissingFiles.Add(old.Final);
                }
                // New copies count toward historical multiplicity but are never queued for reuse
                // by another item in this same inventory.
                VerifyGroup(new(replacement.Length,replacement.Hash!)).Add(replacement.Id);
            }
        }
        catch(OperationCanceledException){mayRetryDeficits=false;report.RetryRecommended=false;report.Status="In pausa. I temporanei saranno confrontati con la sorgente prima di riprendere.";return report;}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidDataException or OverflowException)
        {report.InventoryErrors.Add(ex.Message);report.StoppedForCapacity=ex is SpaceException;
            report.RetryRecommended=ex is IOException&&!report.StoppedForCapacity;mayRetryDeficits=report.RetryRecommended;report.Status="Importazione in pausa: "+ex.Message;return report;}
        finally
        {
            report.EndedUtc=DateTimeOffset.UtcNow;
            AddOutstanding();
            // If persistence fails (e.g. disk completely full), surface it instead of claiming success.
            catalog.SaveReport(runId,report);
            string reports=destination.Resolve("Report Porto");ReportWriter.Write(reports,runId,report);
            ReportPath=Path.Combine(reports,$"report-{runId}.txt");
            Notify(report.Status,"Report salvato",true);
        }
        void AddOutstanding()
        {
            report.Items.RemoveAll(i=>i.Outcome==Outcome.Missing&&i.File!=null&&resolvedMissingFiles.Contains(i.File));
            foreach(var work in ledger.Unresolved())
                if(processed.Add(WorkLedger.Key(work.Source)))report.Items.Add(new(work.Source.Id,work.Source.Name,work.Outcome,
                    "Da completare o verificare (anche da un inventario precedente): "+work.Detail));
            foreach(var requirement in ledger.Requirements())
                if(verifiedCopies.TryGetValue(requirement.Content,out var copies)&&copies.Count<requirement.Count&&reportedDeficits.Add(requirement.Content))
                {
                    report.Items.Add(new(requirement.Content.Hash,"Video identici da completare",Outcome.Pending,
                        $"Inventari precedenti provano almeno {requirement.Count} video con questo contenuto; copie integre verificate: {copies.Count}."));
                    if(mayRetryDeficits&&!cancel.IsCancellationRequested&&!report.StoppedForCapacity)report.RetryRecommended=true;
                }
            foreach(var conflict in ledger.Conflicts())
                if(reportedConflicts.Add(WorkLedger.Key(conflict.Source)))
                    report.Items.Add(new(conflict.Source.Id,conflict.Source.Name,Outcome.Ambiguous,conflict.Detail));
        }
    }

    public void Recover(Catalog catalog,CancellationToken cancel)
    {
        var entries=catalog.Entries().ToDictionary(e=>e.Id);
        void Complete(CatalogEntry entry)
        {
            if(entry.Replaces!=null)
            {
                if(!entries.TryGetValue(entry.Replaces,out var old)||old.Hash!=entry.Hash||old.Length!=entry.Length)
                    throw new InvalidDataException("Relazione di recupero nel catalogo non valida.");
                catalog.SaveAll(entry with{State="Complete"},old with{State="Replaced"});
            }
            else catalog.Save(entry with{State="Complete"});
        }
        foreach(var entry in entries.Values.Where(e=>e.State=="Ready"))
        {
            cancel.ThrowIfCancellationRequested();
            string final=destination.Resolve(entry.Final),temp=destination.Resolve(entry.Temp);
            var expected=new CopyResult(entry.Length,entry.Hash!);
            if(File.Exists(final))
            {
                if(expected==VerifiedCopy.HashFile(final,cancel))Complete(entry);
                else if(File.Exists(temp)&&expected==VerifiedCopy.HashFile(temp,cancel))
                {
                    var alternate=entry with{Final=Path.Combine("Video Porto",Destination.SafeStem(entry.Source.Name)+"_"+Guid.NewGuid().ToString("N")+Path.GetExtension(entry.Final))};
                    catalog.Save(alternate);
                    Destination.Publish(temp,destination.Resolve(alternate.Final));Complete(alternate);
                }
                else catalog.Save(entry with{State=entry.Replaces==null?"Damaged":"Abandoned"});
            }
            else if(File.Exists(temp))
            {
                if(expected==VerifiedCopy.HashFile(temp,cancel))
                {Destination.Publish(temp,final);Complete(entry);}
                else catalog.Save(entry with{State=entry.Replaces==null?"Damaged":"Abandoned"});
            }
            else catalog.Save(entry with{State=entry.Replaces==null?"Missing":"Abandoned"});
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
