using Porto.Core;

internal static class Rc6RecoveryTests
{
    public static void Add(List<(string,Action)> tests)
    {
        foreach(bool remap in new[]{false,true})
            tests.Add(($"rc6 recovery prioritizes untouched healthy files after delayed cleanup failure; remap={remap}",()=>
            {
                using var destination=new Destination(Fixtures.Root());
                byte[] videoB=Fixtures.Video(72),videoC=Fixtures.Video(73),videoD=Fixtures.Video(74);
                int attempts=0;
                var session=new ImportSession(()=>
                {
                    int attempt=++attempts;bool poisoned=false;
                    string suffix=remap?"-"+attempt:"";
                    var items=new[]{"A","B","C","D"}.Select(name=>new SourceItem(name+suffix,"synthetic",name+".mov",MediaKind.Video,videoB.Length)).ToList();
                    return new FakeSource(items,[]){Reader=(item,_)=>
                    {
                        if(poisoned)throw new PreviousReadCleanupException("Synthetic cleanup of the previous failed resource could not restore the session.");
                        if(item.Name=="A.mov")
                        {poisoned=true;throw new IOException("Synthetic per-file failure; cleanup failure appears at the next open.");}
                        return new MemoryStream(item.Name=="B.mov"?videoB:item.Name=="C.mov"?videoC:videoD,false);
                    }};
                },destination){RetryDelay=TimeSpan.Zero};
                var report=session.Run(true,default);
                var saved=Directory.GetFiles(destination.Videos).Select(File.ReadAllBytes).ToList();
                Fixtures.Check(saved.Count==3&&saved.Any(bytes=>bytes.SequenceEqual(videoB))&&saved.Any(bytes=>bytes.SequenceEqual(videoC))&&saved.Any(bytes=>bytes.SequenceEqual(videoD)),
                    "The file with the original error monopolized retries or blamed the next file; healthy B/C/D were not all copied exactly once.");
                Fixtures.Check(attempts==ImportSession.MaximumAttempts&&!report.Status.StartsWith("Completati",StringComparison.Ordinal),
                    "Retry bounds or truthful incomplete outcome were lost.");
                Fixtures.Check(report.Items.Any(item=>item.Name=="A.mov"&&item.Outcome is Outcome.Error or Outcome.Pending),
                    "The failing file disappeared from unresolved results.");
                Fixtures.Check(report.Items.Any(item=>item.Name=="B.mov"),"A file blocked by delayed cleanup disappeared from results.");
            }));
    }
}
