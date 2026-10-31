namespace Porto.Core;

public sealed class ImportSession(Func<IVideoSource> createSource, Destination destination, IDiskSpace? disk = null)
{
    public const int MaximumAttempts = 3;
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(15);
    public Action<ImportProgress>? Progress { get; init; }
    public string? ReportPath { get; private set; }
    public int Attempts { get; private set; }

    public ImportReport Run(bool deep, CancellationToken cancel)
    {
        ImportReport? latest=null;
        for(int attempt=1;attempt<=MaximumAttempts;attempt++)
        {
            if(cancel.IsCancellationRequested)break;
            Attempts=attempt;
            using var source=createSource();
            var engine=new ImportEngine(source,destination,disk){Progress=Progress,Attempt=attempt};
            latest=engine.Run(deep,cancel);ReportPath=engine.ReportPath;
            if(!latest.RetryRecommended||cancel.IsCancellationRequested||attempt==MaximumAttempts)break;
            Progress?.Invoke(new("Recupero automatico",$"Tentativo {attempt+1}/{MaximumAttempts} tra {RetryDelay.TotalSeconds:0} secondi. Sblocca o ricollega lo stesso iPhone. Pausa interrompe l'attesa.",0,latest.CandidateCount,0,null,null));
            if(cancel.WaitHandle.WaitOne(RetryDelay))break;
        }
        return latest??new ImportReport{Status="In pausa prima dell'avvio."};
    }
}
