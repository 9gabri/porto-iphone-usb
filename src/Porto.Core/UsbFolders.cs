namespace Porto.Core;

// Paths describe a physical WPD subtree. They are never proof of asset identity.
public sealed record UsbFolderSegment(string Name,string? PersistentId);
public sealed record UsbFolderChoice(string DisplayPath,IReadOnlyList<UsbFolderSegment> Segments)
{
    public override string ToString()=>DisplayPath;
}

public static class UsbFolders
{
    public static List<UsbFolderChoice> List(Inventory inventory)
    {
        var paths=Paths(inventory);
        return paths.Values.GroupBy(path=>string.Join('\0',path.Select(segment=>segment.Name)),StringComparer.Ordinal)
            .Where(group=>group.Count()==1).Select(group=>group.Single())
            .Select(path=>new UsbFolderChoice(string.Join(" / ",path.Select(segment=>DisplaySegment(segment.Name))),path))
            .OrderBy(choice=>choice.DisplayPath,StringComparer.OrdinalIgnoreCase).ToList();
    }

    internal static List<SourceItem> Select(Inventory inventory,UsbFolderChoice choice)
    {
        if(choice.Segments is not {Count:>0 and <=256})throw Unavailable();
        var paths=Paths(inventory);
        // Even distinct persistent IDs cannot resolve an ambiguous human folder path.
        var matches=paths.Where(pair=>SameNames(pair.Value,choice.Segments)).ToList();
        if(matches.Count!=1)throw Unavailable();
        var match=matches[0];
        for(int index=0;index<choice.Segments.Count;index++)
            if(choice.Segments[index].PersistentId is { } persistent&&
                persistent!=match.Value[index].PersistentId)throw Unavailable();
        var descendants=paths.Where(pair=>pair.Value.Count>=match.Value.Count&&
            SameNames(pair.Value.Take(match.Value.Count).ToArray(),match.Value))
            .Select(pair=>pair.Key).ToHashSet(StringComparer.Ordinal);
        return inventory.Items.Where(item=>descendants.Contains(item.Id)||descendants.Contains(item.ParentId)).ToList();
    }

    private static Dictionary<string,IReadOnlyList<UsbFolderSegment>> Paths(Inventory inventory)
    {
        var items=new Dictionary<string,SourceItem>(StringComparer.Ordinal);
        foreach(var item in inventory.Items)
            if(string.IsNullOrEmpty(item.Id)||string.IsNullOrEmpty(item.Name)||item.ParentId==null||
                item.Id.Contains('\0')||item.Name.Contains('\0')||item.ParentId.Contains('\0')||
                !items.TryAdd(item.Id,item))throw Unavailable();
        var paths=new Dictionary<string,IReadOnlyList<UsbFolderSegment>>(StringComparer.Ordinal);
        foreach(var item in items.Values)
        {
            var chain=new List<UsbFolderSegment>();var visited=new HashSet<string>(StringComparer.Ordinal);
            var current=item;
            while(true)
            {
                if(!visited.Add(current.Id)||visited.Count>256)throw Unavailable();
                if(current.Kind==MediaKind.Container)chain.Add(new(current.Name,current.PersistentId));
                if((current.ParentId=="DEVICE"&&!items.ContainsKey("DEVICE"))||current.ParentId.Length==0)break;
                if(!items.TryGetValue(current.ParentId,out var parent)||parent.Kind!=MediaKind.Container)throw Unavailable();
                current=parent;
            }
            if(item.Kind==MediaKind.Container){chain.Reverse();paths.Add(item.Id,chain.ToArray());}
        }
        return paths;
    }
    private static bool SameNames(IReadOnlyList<UsbFolderSegment> left,IReadOnlyList<UsbFolderSegment> right)=>
        left.Count==right.Count&&left.Select(segment=>segment.Name).SequenceEqual(right.Select(segment=>segment.Name),StringComparer.Ordinal);
    private static string DisplaySegment(string name)=>string.Concat(name.Select(character=>
        character is '%' or '/' or '\\'||char.IsControl(character)?"%"+((int)character).ToString("X2",System.Globalization.CultureInfo.InvariantCulture):character.ToString()));
    private static IOException Unavailable()=>new("La cartella USB scelta è assente, cambiata o non identificabile senza ambiguità. Ripeti la selezione; nessuna estensione automatica a tutto il telefono.");
}
