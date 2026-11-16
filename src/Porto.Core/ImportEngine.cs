using System.Diagnostics;

namespace Porto.Core;

public sealed class ImportEngine(IVideoSource source,Destination destination,IDiskSpace? disk=null)
{
    private readonly IDiskSpace space=disk??new LocalDiskSpace();
    public Action<string>? FaultPoint { get; set; }
    public Action<ImportProgress>? Progress { get; set; }
    public string? ReportPath { get; private set; }
    public int Attempt { get; set; } = 1;
    public ImportMedia Media { get; init; } = ImportMedia.Videos;
    public IReadOnlyList<RetryHint> RetryLast { get; init; } = [];
    public IReadOnlyList<RetryHint> RetryVisited { get; init; } = [];
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
        bool folderSelection=source is ISelectedVideoSource;
        var gallery=source as IGallerySource;
        bool selected=folderSelection||gallery!=null;
        bool appleHints=!selected&&((source as ISourceContext)?.AppleResourceHints??true);
        var report=new ImportReport{SecondSourceRead=deep,Attempt=Attempt,SelectedFolder=folderSelection,SelectedGallery=gallery!=null,Media=Media,
            SourceDescription=(source as ISelectedVideoSource)?.SelectionDescription??(source as ISourceContext)?.SourceDescription??"Dispositivo tramite USB"};
        var previousCopies=new List<CatalogEntry>();
        var reusedPrevious=new HashSet<string>(StringComparer.Ordinal);
        var externalIds=new HashSet<string>(StringComparer.Ordinal);
        var processed=new HashSet<string>(StringComparer.Ordinal);
        var resolvedMissingFiles=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var verifiedCopies=new Dictionary<CopyResult,HashSet<string>>();
        var reconciledContents=new HashSet<CopyResult>();
        var reportedDeficits=new HashSet<CopyResult>();
        var reportedConflicts=new HashSet<string>(StringComparer.Ordinal);
        bool mayRetryDeficits=true;
        List<SourceItem> observed=[];
        var currentKeys=new HashSet<string>(StringComparer.Ordinal);
        copied=0;done=0;total=0;totalBytes=null;lastNotify=0;
        timer.Restart();
        ledger.Migrate(catalog.Entries());
        try
        {
            Notify(folderSelection?"Inventario cartella":gallery!=null?"Catalogo galleria":"Inventario USB", "Lettura dell'elenco, prima di copiare",true);
            var inventory=source.Inventory(cancel);
            report.GalleryCoverage=gallery?.Coverage;
            if(gallery!=null)report.SourceDescription=gallery.SourceDescription;
            observed=inventory.Items;
            var observedKeys=observed.Where(NeedsTracking).Select(WorkLedger.Key).ToHashSet(StringComparer.Ordinal);
            currentKeys=observedKeys;
            report.CandidateCount=observed.Count(IsCandidate);
            report.UsbItemCount=observed.Count;
            if(gallery!=null)report.ExcludedByMediaFilter=observed.Count(item=>item.Kind is MediaKind.Image or MediaKind.Video&&!IsCandidate(item));
            ledger.Observe(observed.Where(NeedsTracking),runId+"-initial");
            report.UsbEnumerationComplete=inventory.EnumerationComplete;
            report.InventoryErrors=inventory.Errors;
            // Save the inventory result before any transfer. Each item has an explicit pending category.
            catalog.SaveReport(runId+"-inventory",new ImportReport{
                SelectedFolder=folderSelection,SelectedGallery=gallery!=null,GalleryCoverage=report.GalleryCoverage,SourceDescription=report.SourceDescription,Media=Media,
                Status=$"Inventario: {inventory.Items.Count} elementi; candidati {inventory.Items.Count(IsCandidate)}",
                UsbEnumerationComplete=inventory.EnumerationComplete,InventoryErrors=inventory.Errors});
            string invPath=destination.Resolve(Path.Combine(".porto",$"inventory-{runId}.json"));
            var persistedInventory=gallery==null?inventory:new Inventory(inventory.Items.Where(NeedsTracking).ToList(),inventory.Errors,inventory.EnumerationComplete);
            AtomicFile.WriteNew(invPath,inv=>System.Text.Json.JsonSerializer.Serialize(inv,persistedInventory));
            Recover(catalog,cancel);
            var entries=catalog.Entries();
            if(selected)
            {
                previousCopies.AddRange(entries.Where(e=>!string.IsNullOrEmpty(e.Final)));
                foreach(var external in FolderArchiveComparison.FindUnregistered(destination,entries,cancel,
                    (name,n)=>Notify("Confronto archivio precedente",name,false,n),Media))
                {entries.Add(external);previousCopies.Add(external);externalIds.Add(external.Id);}
            }
            for(int index=0;index<entries.Count;index++)
            {
                var prior=entries[index];
                if(externalIds.Contains(prior.Id))continue; // Foreign paths are read-only, never catalog mutations.
                if(prior.State=="Missing"&&!string.IsNullOrEmpty(prior.Final)&&File.Exists(destination.Resolve(prior.Final))&&
                    new CopyResult(prior.Length,prior.Hash!)==VerifiedCopy.HashFile(destination.Resolve(prior.Final),cancel))
                {prior=prior with{State="Complete"};entries[index]=prior;catalog.Save(prior);}
                if(prior.State=="Missing" || prior.State=="Complete"&&!File.Exists(destination.Resolve(prior.Final)))
                {
                    entries[index]=prior with{State="Missing"};catalog.Save(entries[index]);
                    report.Items.Add(new(prior.Source.Id,prior.Source.Name,Outcome.Missing,"Una copia registrata non è più presente nella destinazione. Se la sorgente è disponibile verrà ricopiata.",prior.Final,Historical:selected||!currentKeys.Contains(WorkLedger.Key(prior.Source))));
                }
                else if(prior.State=="Damaged")report.Items.Add(new(prior.Source.Id,prior.Source.Name,Outcome.Error,"Copia precedente danneggiata o collisionante; conservata. Recupero tramite nuova copia se disponibile.",prior.Final,Historical:selected||!currentKeys.Contains(WorkLedger.Key(prior.Source))));
            }
            var known=entries.Where(e=>e.State is "Complete" or "Quarantined").ToList();
            var missing=entries.Where(e=>e.State is "Missing" or "Damaged").GroupBy(e=>(e.Hash,e.Length)).ToDictionary(g=>g.Key,g=>new Queue<CatalogEntry>(g));
            var replacementIds=missing.Values.SelectMany(queue=>queue).Select(entry=>entry.Id).ToHashSet(StringComparer.Ordinal);
            CatalogEntry? MissingMatch(string? hash,long length)=>missing.TryGetValue((hash,length),out var queue)&&queue.TryPeek(out var old)?old:null;
            var groups=known.GroupBy(e=>new CopyResult(e.Length,e.Hash!)).ToDictionary(g=>g.Key,g=>g.ToList());
            var byHash=groups.ToDictionary(pair=>pair.Key,pair=>new Queue<CatalogEntry>(pair.Value));
            var knownLengths=known.Select(e=>e.Length).ToHashSet();

            int availableCount=known.Count;
            bool VerifyLocal(CatalogEntry old,CopyResult content)
            {
                string oldRelative=old.State=="Quarantined"?old.Temp:old.Final;
                string oldPath=destination.Resolve(oldRelative);
                bool exists=File.Exists(oldPath);
                if(exists&&VerifiedCopy.HashFile(oldPath,cancel,n=>Notify("Verifica file già presente",old.Source.Name,false,n))==content)return true;
                var broken=old with{State=exists?"Damaged":"Missing"};
                if(!externalIds.Contains(old.Id))catalog.Save(broken);
                if(verifiedCopies.TryGetValue(content,out var verified))verified.Remove(old.Id);
                if(!externalIds.Contains(old.Id)&&replacementIds.Add(old.Id))
                {
                    if(!missing.TryGetValue((old.Hash,old.Length),out var replacements))missing[(old.Hash,old.Length)]=replacements=new();
                    replacements.Enqueue(broken);
                    report.Items.Add(new(old.Source.Id,old.Source.Name,exists?Outcome.Error:Outcome.Missing,
                        exists?"Copia precedente alterata; conservata senza sovrascrittura.":"Una copia registrata non è più presente nella destinazione.",oldRelative,Historical:selected||!currentKeys.Contains(WorkLedger.Key(old.Source))));
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
            // An absent source does not prove that its historical local copies remain intact.
            // Cache each group once; the individual file selected for reuse is still rechecked.
            foreach(var content in groups.Keys)VerifyGroup(content);
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
            var companions=appleHints?MediaInspector.SuspectedCompanions(inventory.Items):new Dictionary<string,string>();
            var candidates=inventory.Items.Where(IsCandidate).ToList();
            total=candidates.Count;
            report.CandidateCount=total;
            report.UsbItemCount=inventory.Items.Count;
            report.ImageCount=inventory.Items.Count(i=>i.Kind==MediaKind.Image);
            report.SidecarCount=inventory.Items.Count(MediaInspector.IsAdjustmentSidecar);
            report.SuspectedCompanionCount=companions.Count;
            report.TransferCandidateCount=total;
            report.SelectedPhotoCount=Media==ImportMedia.Photos?total:
                candidates.Count(item=>PhotoInspector.Candidate(item)&&!MediaInspector.Candidate(item));
            report.SelectedVideoCount=total-report.SelectedPhotoCount;
            totalBytes=candidates.All(i=>i.Size.HasValue)?candidates.Aggregate(0L,(n,i)=>checked(n+i.Size!.Value)):null;
            // Reserve only a lower-bound estimate for genuinely unknown lengths; repeated content is hashed before writing.
            long required=candidates.Where(i=>!i.Offline&&i.Size.HasValue&&!knownLengths.Contains(i.Size.Value))
                .Aggregate(VerifiedCopy.ReserveBytes,(n,i)=>checked(n+Remaining(i)));
            Notify("Inventario pronto",$"{total:N0} file candidati ({report.SelectedPhotoCount:N0} foto, {report.SelectedVideoCount:N0} video); spazio prudenziale {required:N0} byte",true);
            EnsureSpace(required);
            foreach(var item in RetryOrder.Arrange(inventory.Items,RetryLast,RetryVisited))
            {
                cancel.ThrowIfCancellationRequested();
                if(!IsCandidate(item))
                {
                    // Keep aggregate exclusions without persisting the names of unselected gallery media.
                    if(gallery!=null&&item.Kind is MediaKind.Image or MediaKind.Video)continue;
                    if(item.Kind is not(MediaKind.Container or MediaKind.Collection))
                    {
                        bool sidecar=gallery==null&&MediaInspector.IsAdjustmentSidecar(item);
                        bool uncertain=NeedsTracking(item)&&!sidecar;
                        report.Items.Add(new(item.Id,item.Name,uncertain?Outcome.Ambiguous:Outcome.Excluded,
                            sidecar?"File accessorio .AAE: escluso dalla copia.":uncertain?"Formato non supportato o metadati insufficienti: da verificare; nessuna copia verificata.":
                            MediaSelectionRules.Candidate(item,ImportMedia.PhotosAndVideos)||UnsupportedVideoExtension(item)?"Tipo di contenuto escluso dalla selezione corrente (foto/video).":"Contenuto escluso: non riconosciuto come foto o video supportato."));
                        if(uncertain)SaveProgress(item);
                    }
                    continue;
                }
                if(item.Offline){report.Items.Add(new(item.Id,item.Name,Outcome.Missing,"Originale segnalato non disponibile offline."));done++;SaveProgress(item);continue;}
                Notify("Importazione",item.Name,true);
                string sizeNote="";
                try
                {
                    bool verifySourceTwice=deep;
                    long? Expected(Stream input)
                    {
                        long? resource=(input as ISourceStreamInfo)?.ResourceLength;
                        if(resource is <0)throw new InvalidDataException("Dimensione risorsa USB non valida.");
                        if(resource.HasValue&&resource!=item.Size)
                        {
                            verifySourceTwice=true;
                            sizeNote=$" Dimensione inventario: {item.Size?.ToString()??"sconosciuta"}; risorsa USB: {resource} byte. Richieste due letture concordanti.";
                        }
                        return resource??item.Size;
                    }
                    CopyResult ReadSource(Stream input,string phase)=>VerifiedCopy.Copy(input,Stream.Null,Expected(input),n=>Notify(phase,item.Name,false,n),null,cancel);
                    CopyResult? fingerprint=null;
                    if(availableCount>0)
                    {
                        using var input=source.OpenRead(item,cancel);
                        fingerprint=ReadSource(input,"Confronto contenuto USB");

                        ledger.Remember(item,fingerprint);
                        var verified=VerifyGroup(fingerprint);
                        CatalogEntry? match=null;
                        // One copy cannot discharge two videos that were ever observed together.
                        // Leave the existing queue untouched when another physical copy is needed.
                        if((selected||verified.Count>=ledger.RequiredCopies(fingerprint))&&byHash.TryGetValue(fingerprint,out var matching))
                        {
                            while(matching.TryDequeue(out var old))
                            {
                                availableCount--;
                                if(!verified.Contains(old.Id))continue;
                                // A group count can be cached, but the file actually being reused
                                // must still match now, after possibly hours of other transfers.
                                if(VerifyLocal(old,fingerprint)){match=old;break;}
                                if(!selected&&verified.Count<ledger.RequiredCopies(fingerprint))break;
                            }
                        }
                        if(match!=null)
                        {
                            if(verifySourceTwice){using var second=source.OpenRead(item,cancel);VerifySourceReads(item,fingerprint,ReadSource(second,"Seconda lettura USB"));}
                            ledger.ReadVerified(item,fingerprint);
                            if(match.State=="Quarantined")
                            {
                                string preserved=destination.Resolve(match.Temp);
                                var inspected=InspectSelected(preserved,item,cancel);
                                if(inspected.IsSupportedMedia&&!inspected.LivePhoto)
                                {
                                    // Reclassify into a NEW file. Never rename or overwrite the old trial's .part.
                                    EnsureSpace(checked(fingerprint.Length+VerifiedCopy.ReserveBytes));
                                    string promotedId=Guid.NewGuid().ToString("N");
                                    var promoted=new CatalogEntry(promotedId,"Copying",Path.Combine(".porto",promotedId+".part"),"",null,0,item);
                                    catalog.Save(promoted);FaultPoint?.Invoke("promotion-intent");
                                    string promotedTemp=destination.Resolve(promoted.Temp);
                                    using(var local=File.OpenRead(preserved))
                                    using(var output=new FileStream(promotedTemp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                                    {
                                        var localCopy=VerifiedCopy.Copy(local,output,fingerprint.Length,null,
                                            ()=>EnsureSpace(VerifiedCopy.ReserveBytes+VerifiedCopy.BufferSize),cancel);
                                        output.Flush(true);VerifiedCopy.Verify(fingerprint,localCopy);
                                    }
                                    FaultPoint?.Invoke("promotion-flush");
                                    VerifiedCopy.Verify(fingerprint,VerifiedCopy.HashFile(promotedTemp,cancel));
                                    bool additional=appleHints&&inspected.IsVideo&&(companions.ContainsKey(item.Id)||inspected.AdditionalResource);
                                    promoted=promoted with{State="Ready",Hash=fingerprint.Hash,Length=fingerprint.Length,Replaces=match.Id,
                                        Final=FinalName(item,inspected,promotedId,additional)};
                                    catalog.Save(promoted);FaultPoint?.Invoke("promotion-ready");
                                    Destination.Publish(promotedTemp,destination.Resolve(promoted.Final));FaultPoint?.Invoke("promotion-rename");
                                    promoted=promoted with{State="Complete"};
                                    catalog.SaveAll(promoted,match with{State="Replaced"});
                                    verified.Remove(match.Id);verified.Add(promoted.Id);
                                    FaultPoint?.Invoke("promotion-complete");
                                    report.Items.Add(new(item.Id,item.Name,additional?Outcome.Additional:Outcome.Imported,
                                        "Recuperato da un temporaneo precedente, conservato invariato. "+ClassificationDetail(item,inspected)+sizeNote,
                                        promoted.Final,fingerprint.Length,fingerprint.Hash));
                                }
                                else report.Items.Add(new(item.Id,item.Name,Outcome.Ambiguous,inspected.Detail+" Temporaneo precedente conservato e verificato.",match.Temp,fingerprint.Length,fingerprint.Hash));
                            }
                            else
                            {
                                var inspected=InspectSelected(destination.Resolve(match.Final),item,cancel);
                                bool additional=appleHints&&inspected.IsVideo&&(companions.ContainsKey(item.Id)||Path.GetDirectoryName(match.Final)==Destination.AdditionalDirectory||inspected.AdditionalResource);
                                bool valid=inspected.IsSupportedMedia&&!inspected.LivePhoto;
                                if(valid&&!externalIds.Contains(match.Id))catalog.Save(match with{Source=item});
                                if(valid)reusedPrevious.Add(match.Id);
                                report.Items.Add(new(item.Id,item.Name,!valid?Outcome.Ambiguous:additional?Outcome.AdditionalAlreadyPresent:Outcome.AlreadyPresent,
                                    "Sorgente e copia locale rilette: SHA-256 identico. File precedente conservato nella sua cartella."+
                                    (!valid?" Il contenitore richiede verifica: "+inspected.Detail:additional?" Risorsa aggiuntiva: corrispondenza con un video autonomo di Foto non certificabile.":"")+sizeNote,match.Final,fingerprint.Length,fingerprint.Hash));
                            }
                            ReconcileContent(fingerprint);
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
                            received=resuming?VerifiedCopy.Resume(input,output,Expected(input),n=>{copied=start+n;Notify("Ripresa verificata",item.Name,false);},
                                ()=>EnsureSpace(VerifiedCopy.ReserveBytes+VerifiedCopy.BufferSize),cancel)
                                :VerifiedCopy.Copy(input,output,Expected(input),n=>{copied=start+n;Notify("Copia",item.Name,false);},
                                ()=>EnsureSpace(VerifiedCopy.ReserveBytes+VerifiedCopy.BufferSize),cancel);
                        }
                        catch(PartialMismatchException){catalog.Save(entry with{State="Abandoned"});throw;}
                        output.Flush(true);FaultPoint?.Invoke("after-flush");
                    }
                    VerifiedCopy.Verify(received,VerifiedCopy.HashFile(temp,cancel,n=>Notify("Verifica locale SHA-256",item.Name,false,n)));
                    if(fingerprint!=null)VerifySourceReads(item,fingerprint,received);
                    if(verifySourceTwice)
                    {
                        using var second=source.OpenRead(item,cancel);
                        VerifySourceReads(item,received,ReadSource(second,"Seconda lettura USB"));
                    }
                    ledger.ReadVerified(item,received);
                    VerifyGroup(received);
                    FaultPoint?.Invoke("after-verification");
                    var media=InspectSelected(temp,item,cancel);
                    if(media.LivePhoto||!media.IsSupportedMedia)
                    {
                        entry=entry with{State="Quarantined",Hash=received.Hash,Length=received.Length};SaveFinalEntry(entry);ReconcileContent(received);
                        report.Items.Add(new(item.Id,item.Name,media.LivePhoto?Outcome.Excluded:Outcome.Ambiguous,
                            media.Detail+" Temporaneo conservato in .porto.",entry.Temp,received.Length,received.Hash));
                        done++;SaveProgress(item);continue;
                    }
                    bool isAdditional=appleHints&&media.IsVideo&&(companions.ContainsKey(item.Id)||media.AdditionalResource);
                    entry=entry with{State="Ready",Hash=received.Hash,Length=received.Length,
                        Replaces=MissingMatch(received.Hash,received.Length)?.Id,
                        Final=FinalName(item,media,id,isAdditional)};
                    catalog.Save(entry);FaultPoint?.Invoke("after-ready");
                    Destination.Publish(temp,destination.Resolve(entry.Final));FaultPoint?.Invoke("after-rename");
                    entry=entry with{State="Complete"};SaveFinalEntry(entry);ReconcileContent(received);FaultPoint?.Invoke("after-complete");
                    report.Items.Add(new(item.Id,item.Name,isAdditional?Outcome.Additional:Outcome.Imported,ClassificationDetail(item,media)+sizeNote,entry.Final,entry.Length,entry.Hash));
                    done++;SaveProgress(item);
                }
                catch(OperationCanceledException){throw;}
                catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidDataException)
                {
                    bool previousCleanup=ex is PreviousReadCleanupException;
                    if(!previousCleanup && source is IReadFailureRecovery recovery)recovery.InvalidateRead();
                    report.Items.Add(new(item.Id,item.Name,previousCleanup?Outcome.Pending:Outcome.Error,ex.Message+sizeNote));
                    // Remember the actual failed reads, including those preceding a
                    // cleanup failure. A resource not opened yet must not be penalized.
                    if(!previousCleanup)report.RetryLast.Add(RetryOrder.AfterFailure(inventory.Items,item));
                    done++;SaveProgress(item);report.RetryRecommended=true;
                    if(ex is SpaceException or UnauthorizedAccessException or PreviousReadCleanupException ||
                        ex is SourceUnavailableException && source is not IReadFailureRecovery)
                    {
                        report.ReadRecoveryInterrupted=ex is SourceUnavailableException;
                        report.StoppedForCapacity=ex is SpaceException;
                        if(report.StoppedForCapacity||ex is UnauthorizedAccessException){report.RetryRecommended=false;mayRetryDeficits=false;}
                        report.Status="Importazione in pausa: collegamento o destinazione da verificare.";
                        return report;
                    }
                }
            }
            Notify("Controllo finale", folderSelection?"Verifico che l’elenco della cartella non sia cambiato":gallery!=null?"Verifico di nuovo il catalogo e la selezione della galleria":"Verifico che l’elenco dei file USB non sia cambiato",true);
            var finalInventory=source.Inventory(cancel);
            currentKeys.UnionWith(finalInventory.Items.Where(NeedsTracking).Select(WorkLedger.Key));
            report.FinalInventoryChecked=finalInventory.EnumerationComplete&&finalInventory.Errors.Count==0&&
                inventory.Items.Select(WorkLedger.Key).Order(StringComparer.Ordinal).SequenceEqual(finalInventory.Items.Select(WorkLedger.Key).Order(StringComparer.Ordinal));
            if(!report.FinalInventoryChecked)
            {
                report.InventoryErrors.Add("L'inventario finale è cambiato o incompleto. È necessario un altro passaggio.");
                report.InventoryErrors.AddRange(finalInventory.Errors);report.RetryRecommended=true;
                var initialKeys=inventory.Items.Select(WorkLedger.Key).ToHashSet(StringComparer.Ordinal);
                var newKeys=finalInventory.Items.Select(WorkLedger.Key).Where(key=>!initialKeys.Contains(key)).ToHashSet(StringComparer.Ordinal);
                ledger.Observe(finalInventory.Items.Where(NeedsTracking),runId+"-final",newKeys);
            }
            if(!report.UsbEnumerationComplete||report.InventoryErrors.Count>0)report.RetryRecommended=true;
            AddOutstanding();
            report.Status=total>0 && report.FinalInventoryChecked && report.UsbEnumerationComplete && report.InventoryErrors.Count==0 &&
                !report.Items.Any(i=>i.Outcome is Outcome.Error or Outcome.Ambiguous or Outcome.Missing or Outcome.Pending)
                ?report.Items.Any(i=>i.Outcome is Outcome.Additional or Outcome.AdditionalAlreadyPresent)
                    ?"Risorse USB copiate e verificate. Distinzione dei video Foto non verificabile: consulta le risorse aggiuntive."
                    :"Completati copia e controllo dei file selezionati e individuati tramite USB. Intera libreria del telefono non verificabile."
                :"Importazione incompleta o con elementi non verificabili. Consulta il report.";
            if(selected)
            {
                bool selectionVerified=total>0&&report.FinalInventoryChecked&&report.UsbEnumerationComplete&&report.InventoryErrors.Count==0&&
                    report.CurrentCount(Outcome.Imported)+report.CurrentCount(Outcome.AlreadyPresent)==total&&
                    !report.Items.Any(i=>!i.Historical&&i.Outcome is Outcome.Error or Outcome.Ambiguous or Outcome.Missing or Outcome.Pending);
                report.SelectedFolderVerified=folderSelection&&selectionVerified;
                report.GallerySelectionCopiesVerified=gallery!=null&&selectionVerified;
                report.Status=gallery!=null
                    ?selectionVerified?$"Verificate le copie dei {total:N0} elementi selezionati nel catalogo Foto osservato. Completezza dell’intera libreria non certificata.":"Selezione della galleria incompleta o non verificabile. Consulta il report."
                    :report.SelectedFolderVerified
                    ?$"Verificati i {total:N0} file della cartella selezionata. "+(report.HistoricalCount>0?"Restano osservazioni precedenti da verificare nel report.":"Copertura della libreria del telefono non verificata.")
                    :"Importazione da cartella incompleta o non verificabile. Consulta il report.";
                if(folderSelection)report.RetryRecommended=false;
            }
            if(total==0&&report.FinalInventoryChecked&&report.UsbEnumerationComplete&&report.InventoryErrors.Count==0&&
                !report.Items.Any(item=>(!selected||!item.Historical)&&item.Outcome is Outcome.Error or Outcome.Ambiguous or Outcome.Missing or Outcome.Pending))
                report.Status=gallery!=null?"Nessun elemento del tipo scelto nel catalogo della selezione.":folderSelection?"Nessun file del tipo scelto trovato nella cartella selezionata.":"Nessun file del tipo scelto individuato via USB. Verifica telefono e contenuti disponibili localmente.";
            return report;
            void SaveProgress(SourceItem item)
            {
                var result=report.Items[^1];
                catalog.AppendResult(runId,result);ledger.Record(item,result);processed.Add(WorkLedger.Key(item));
                if(result.Sha256!=null&&result.Outcome is Outcome.Imported or Outcome.AlreadyPresent or Outcome.Additional or Outcome.AdditionalAlreadyPresent)
                    report.RetryVisited.Add(RetryOrder.AfterFailure(inventory.Items,item));
            }
            string ClassificationDetail(SourceItem item,MediaInspection media)=>gallery!=null?"Elemento del catalogo Foto osservato: struttura e byte verificati. "+report.GalleryCoverage?.Note:folderSelection?"File della cartella scelta: struttura e byte verificati. Selezione nell’app fotografica non verificata indipendentemente.":media.Detail+
                (companions.TryGetValue(item.Id,out string? hint)?" "+hint:"")+
                (appleHints&&media.IsVideo&&(companions.ContainsKey(item.Id)||media.AdditionalResource)?" Copia verificata in Risorse video aggiuntive: corrispondenza con un video autonomo di Foto non certificabile via USB.":"");
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
            report.UsbSessionRestarts=(source as IReadFailureRecovery)?.SessionRestarts??0;
            report.EndedUtc=DateTimeOffset.UtcNow;
            AddOutstanding();
            if(selected)
            {
                if(folderSelection)report.RetryRecommended=false;
                foreach(var prior in previousCopies)
                {
                    bool intact=verifiedCopies.TryGetValue(new(prior.Length,prior.Hash!),out var verified)&&verified.Contains(prior.Id);
                    bool reused=reusedPrevious.Contains(prior.Id);
                    report.PreviousCopies.Add(new(prior.Final,prior.Length,prior.Hash,reused,intact,
                        reused?"Riutilizzata per un file selezionato: confronto completo SHA-256 riuscito; file conservato al suo posto.":
                        intact?(externalIds.Contains(prior.Id)?"File precedente non registrato riletto senza variazioni, conservato e non riutilizzato; nessuna conferma dell’integrità rispetto all’originale. Non classificato automaticamente come Live Photo.":"Copia precedente integra rispetto al catalogo, non riutilizzata in questo passaggio e conservata. Può essere una copia ulteriore, un file diverso, una versione o una risorsa Live Photo; non classificata automaticamente."):
                        "Copia precedente non verificata, assente o alterata: nessuna cancellazione o sovrascrittura."));
                }
            }
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
                    "Da completare o verificare: "+work.Detail,Historical:!currentKeys.Contains(WorkLedger.Key(work.Source))));
            foreach(var requirement in ledger.Requirements())
                if(verifiedCopies.TryGetValue(requirement.Content,out var copies)&&copies.Count<requirement.Count&&reportedDeficits.Add(requirement.Content))
                {
                    report.Items.Add(new(requirement.Content.Hash,"File identici da completare",Outcome.Pending,
                        $"Inventari precedenti provano almeno {requirement.Count} file con questo contenuto; copie integre verificate: {copies.Count}.",Historical:true));
                    if(!selected&&mayRetryDeficits&&!cancel.IsCancellationRequested&&!report.StoppedForCapacity)report.RetryRecommended=true;
                }
            foreach(var conflict in ledger.Conflicts())
                if(reportedConflicts.Add(WorkLedger.Key(conflict.Source)))
                    report.Items.Add(new(conflict.Source.Id,conflict.Source.Name,Outcome.Ambiguous,conflict.Detail,Historical:!currentKeys.Contains(WorkLedger.Key(conflict.Source))));
        }
    }

    private static string FinalName(SourceItem item,MediaInspection media,string id,bool additional)
    {
        string ext=Path.GetExtension(item.Name);
        if(media.IsImage||!MediaInspector.VideoExtensions.Contains(ext))ext=media.Extension;
        return Path.Combine(media.IsImage?Destination.PhotoDirectory:additional?Destination.AdditionalDirectory:"Video Porto",Destination.SafeStem(item.Name)+"_"+id+ext.ToLowerInvariant());
    }
    private bool IsCandidate(SourceItem item)=>(source is not IGallerySource||item.Kind is MediaKind.Image or MediaKind.Video)&&MediaSelectionRules.Candidate(item,Media);
    private MediaInspection InspectSelected(string path,SourceItem item,CancellationToken cancel)
    {
        var result=MediaSelectionRules.Inspect(path,item,cancel);
        return result.IsSupportedMedia&&(Media==ImportMedia.Photos&&!result.IsImage||Media==ImportMedia.Videos&&!result.IsVideo)
            ?result with{Ambiguous=true,Detail="Il contenuto verificato contraddice il tipo selezionato (foto/video) o i metadati: nessuna copia verificata per questa selezione."}
            :result;
    }
    private bool NeedsTracking(SourceItem item)=>IsCandidate(item)||source is IGallerySource&&item.Kind==MediaKind.Unknown||
        Media!=ImportMedia.Photos&&(item.Kind is MediaKind.Other or MediaKind.Unknown)&&UnsupportedVideoExtension(item)||
        item.Kind==MediaKind.Unknown&&!MediaInspector.IsAdjustmentSidecar(item)&&
            !MediaSelectionRules.Candidate(item,ImportMedia.PhotosAndVideos)&&
            (Media!=ImportMedia.Photos||!UnsupportedVideoExtension(item));
    private static bool UnsupportedVideoExtension(SourceItem item)=>Path.GetExtension(item.Name).ToLowerInvariant() is
        ".avi" or ".mkv" or ".webm" or ".3gp" or ".3g2" or ".mts" or ".m2ts" or ".ts" or ".wmv" or ".mpg" or ".mpeg";

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
                    var alternate=entry with{Final=Path.Combine(Path.GetDirectoryName(entry.Final)!,Destination.SafeStem(entry.Source.Name)+"_"+Guid.NewGuid().ToString("N")+Path.GetExtension(entry.Final))};
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
        if(space.Available(destination.Root)<required)throw new SpaceException("Spazio insufficiente. Libera spazio sul PC o scegli un'altra destinazione; nessun file viene eliminato.");
    }
    private void Notify(string phase,string name,bool force,long? activityBytes=null)
    {
        if(!force&&timer.ElapsedMilliseconds-lastNotify<200)return;
        lastNotify=timer.ElapsedMilliseconds;
        if(activityBytes.HasValue)name+=$" · {activityBytes.Value:N0} byte letti per verifica";
        if(source is ISelectedVideoSource){phase=phase.Replace("USB","cartella",StringComparison.Ordinal);name=name.Replace("USB","della cartella",StringComparison.Ordinal);}
        Progress?.Invoke(new(phase,name,done,total,copied,totalBytes,space.Available(destination.Root),
            timer.Elapsed.TotalSeconds>0?copied/timer.Elapsed.TotalSeconds:0));
    }
}
