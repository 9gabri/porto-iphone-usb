namespace Porto.Core;

// This is an ordering hint, never an identity or evidence that any copy is complete.
public sealed record RetryHint(string Name,long? Size,int NextOccurrence);

internal static class RetryOrder
{
    internal static IEnumerable<SourceItem> Arrange(IReadOnlyList<SourceItem> items,IReadOnlyList<RetryHint> hints)
    {
        if(hints.Count==0)return items;
        var history=hints.GroupBy(hint=>(hint.Name,hint.Size)).ToDictionary(group=>group.Key,group=>group.ToArray());
        var preferred=items.Where(item=>!history.ContainsKey((item.Name,item.Size))).ToList();
        var failed=new List<SourceItem>();
        foreach(var group in items.Where(item=>history.ContainsKey((item.Name,item.Size))).GroupBy(item=>(item.Name,item.Size)))
        {
            var siblings=group.ToArray();var prior=history[group.Key];
            int start=prior[^1].NextOccurrence%siblings.Length;
            var failedOrdinals=prior.Select(hint=>(hint.NextOccurrence-1)%siblings.Length).ToHashSet();
            for(int offset=0;offset<siblings.Length;offset++)
            {
                int index=(start+offset)%siblings.Length;
                (failedOrdinals.Contains(index)?failed:preferred).Add(siblings[index]);
            }
        }
        // Prefer unfailed occurrences across every group before any known failure.
        // Otherwise a singleton failure can still block a different group's healthy sibling.
        return preferred.Concat(failed);
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
