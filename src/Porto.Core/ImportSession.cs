namespace Porto.Core;

public sealed class ImportSession(Func<IVideoSource> createSource, Destination destination, IDiskSpace? disk = null)
{
    public const int MaximumAttempts = 3;
    public const int MaximumRecoverySegments = 128;
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(15);
    public Action<ImportProgress>? Progress { get; init; }
    public string? ReportPath { get; private set; }
    public int Attempts { get; private set; }
    public ImportMedia Media { get; init; } = ImportMedia.Videos;

    public ImportReport Run(bool deep, CancellationToken cancel)
    {
        ImportReport? latest=null;
        var retryLast=new List<RetryHint>();
        var retryVisited=new List<RetryHint>();
        var seenFailures=new HashSet<RetryHint>();
        var seenVisits=new HashSet<RetryHint>();
        int? recoveryBudget=null;
        for(int attempt=1;;attempt++)
        {
            if(cancel.IsCancellationRequested)break;
            Attempts=attempt;
            using(var source=createSource())
            {
                var engine=new ImportEngine(source,destination,disk)
                    {Progress=Progress,Attempt=attempt,RetryLast=retryLast,RetryVisited=retryVisited,Media=Media};
                latest=engine.Run(deep,cancel);ReportPath=engine.ReportPath;
            }
            if(recoveryBudget==null&&latest.UsbEnumerationComplete)
                recoveryBudget=Math.Clamp(latest.CandidateCount,0,MaximumRecoverySegments);
            // A failed read must not monopolize every fresh session. The hints only reorder
            // the next inventory; every item still needs its own complete content verification.
            bool newObstacle=false;
            foreach(var hint in latest.RetryLast)newObstacle|=seenFailures.Add(hint);
            retryLast.AddRange(latest.RetryLast);
            foreach(var hint in latest.RetryVisited)
                if(seenVisits.Add(hint))retryVisited.Add(hint);
            if(!latest.RetryRecommended||cancel.IsCancellationRequested)break;
            bool extra=attempt>=MaximumAttempts;
            // Continue past the ordinary retry budget only while interrupted passes reach
            // new ordering positions. Metadata grants neither identity nor copied status.
            // The first complete inventory fixes the cap; changing IDs/names cannot grow it.
            if(extra&&(!newObstacle||!latest.ReadRecoveryInterrupted||attempt-MaximumAttempts>=recoveryBudget.GetValueOrDefault()))break;
            string next=extra?$"Continuazione USB {attempt-MaximumAttempts+1}/{recoveryBudget}"
                :$"Tentativo {attempt+1}/{MaximumAttempts}";
            Progress?.Invoke(new("Recupero automatico",$"{next} tra {RetryDelay.TotalSeconds:0} secondi. Sblocca o ricollega lo stesso telefono. Pausa interrompe l'attesa.",0,latest.CandidateCount,0,null,null));
            if(cancel.WaitHandle.WaitOne(RetryDelay))break;
        }
        return latest??new ImportReport{Media=Media,Status="In pausa prima dell'avvio."};
    }
}
