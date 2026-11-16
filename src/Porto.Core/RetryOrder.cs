namespace Porto.Core;

// This is an ordering hint, never an identity or evidence that any copy is complete.
public sealed record RetryHint(string Name,long? Size,int NextOccurrence);

internal static class RetryOrder
{
    internal static IEnumerable<SourceItem> Arrange(IReadOnlyList<SourceItem> items,IReadOnlyList<RetryHint> hints,
        IReadOnlyList<RetryHint>? visited=null)
    {
        if(hints.Count==0&&(visited==null||visited.Count==0))return items;
        var history=hints.GroupBy(hint=>(hint.Name,hint.Size)).ToDictionary(group=>group.Key,group=>group.ToArray());
        var visits=(visited??[]).GroupBy(hint=>(hint.Name,hint.Size)).ToDictionary(group=>group.Key,group=>group.ToArray());
        var preferred=new List<SourceItem>();
        var read=new List<SourceItem>();
        var failed=new List<SourceItem>();
        foreach(var group in items.GroupBy(item=>(item.Name,item.Size)))
        {
            var siblings=group.ToArray();var prior=history.GetValueOrDefault(group.Key)??[];
            int start=prior.Length==0?0:prior[^1].NextOccurrence%siblings.Length;
            var failedOrdinals=prior.Select(hint=>(hint.NextOccurrence-1)%siblings.Length).ToHashSet();
            var readOrdinals=(visits.GetValueOrDefault(group.Key)??[])
                .Select(hint=>(hint.NextOccurrence-1)%siblings.Length).ToHashSet();
            for(int offset=0;offset<siblings.Length;offset++)
            {
                int index=(start+offset)%siblings.Length;
                (failedOrdinals.Contains(index)?failed:readOrdinals.Contains(index)?read:preferred).Add(siblings[index]);
            }
        }
        // Try unreached positions before rereading earlier successes and failures. All three
        // queues remain in the pass: positional hints never skip or reconcile an observation.
        return preferred.Concat(read).Concat(failed);
    }

    internal static RetryHint AfterFailure(IReadOnlyList<SourceItem> original,SourceItem failed)
    {
        int next=0;
        foreach(var item in original)
        {
            if(item.Name==failed.Name&&item.Size==failed.Size)next++;
            if(ReferenceEquals(item,failed))return new(failed.Name,failed.Size,next);
        }
        throw new InvalidOperationException("Elemento interrotto assente dall'inventario corrente.");
    }
}
