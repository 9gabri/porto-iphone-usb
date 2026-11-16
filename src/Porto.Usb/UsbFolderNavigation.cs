using Porto.Core;

namespace Porto.Usb;

// The callback is strictly one level. Object IDs are resolved afresh from the
// selected physical path on every operation; no missing path can become DEVICE.
internal sealed class UsbFolderNavigation(Func<string,CancellationToken,Inventory> children)
{
    public IReadOnlyList<UsbFolderChoice> ListFolders(UsbFolderChoice? parent,CancellationToken cancel)
        =>ReadLevel(parent,cancel).Folders;

    public IReadOnlyList<UsbCollectionChoice> ListCollections(UsbFolderChoice? parent,CancellationToken cancel)
        =>ReadLevel(parent,cancel).Collections;

    public UsbNavigationLevel ReadLevel(UsbFolderChoice? parent,CancellationToken cancel)
    {
        var chain=parent==null?[]:Resolve(parent,cancel);
        string id=chain.Count==0?"DEVICE":chain[^1].Id;
        var immediate=CompleteChildren(id,cancel);
        var ancestors=chain.Select(item=>item.Id).ToHashSet(StringComparer.Ordinal);
        if(immediate.Any(item=>ancestors.Contains(item.Id)))throw Unavailable();
        var prefix=chain.Select(item=>new UsbFolderSegment(item.Name,item.PersistentId)).ToArray();
        // Same-name siblings are not an unambiguous physical path, even when the
        // driver supplies different persistent IDs. Do not offer them for selection.
        var folders=Choices(MediaKind.Container);
        var collections=Choices(MediaKind.Collection).Select(choice=>new UsbCollectionChoice(choice.DisplayPath,choice.Segments)).ToArray();
        int ambiguous=immediate.Where(item=>item.Kind is MediaKind.Container or MediaKind.Collection)
            .GroupBy(item=>(item.Kind,item.Name)).Where(group=>group.Count()>1).Sum(group=>group.Count());
        string[] warnings=ambiguous==0?[]:[$"{ambiguous:N0} cartelle o raccolte omonime non sono selezionabili senza ambiguità e non vengono mostrate. Nessuna selezione viene estesa automaticamente."];
        return new(folders,collections,warnings);
        UsbFolderChoice[] Choices(MediaKind kind)=>immediate.Where(item=>item.Kind==kind)
            .GroupBy(item=>item.Name,StringComparer.Ordinal).Where(group=>group.Count()==1)
            .Select(group=>new UsbFolderChoice("",[..prefix,new(group.Single().Name,group.Single().PersistentId)]))
            .Select(choice=>choice with{DisplayPath=string.Join(" / ",choice.Segments.Select(segment=>Display(segment.Name)))})
            .OrderBy(choice=>choice.DisplayPath,StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public Inventory Inventory(UsbFolderChoice folder,CancellationToken cancel)
    {
        List<SourceItem> items=Resolve(folder,cancel);
        List<string> errors=[];var guard=new EnumerationGuard();
        foreach(var ancestor in items)guard.Visit(ancestor.Id);
        Queue<(string Id,int Depth)> pending=new();pending.Enqueue((items[^1].Id,items.Count));
        try
        {
            while(pending.TryDequeue(out var parent))
            {
                cancel.ThrowIfCancellationRequested();
                if(parent.Depth>128){Error("Gerarchia USB troppo profonda.");continue;}
                var immediate=children(parent.Id,cancel);
                foreach(string error in immediate.Errors)Error(error);
                if(!immediate.EnumerationComplete&&immediate.Errors.Count==0)Error("Enumerazione della cartella USB incompleta.");
                foreach(var item in immediate.Items)
                {
                    cancel.ThrowIfCancellationRequested();
                    ValidateItem(item,parent.Id);guard.Visit(item.Id);items.Add(item);
                    if(item.Kind==MediaKind.Container)pending.Enqueue((item.Id,parent.Depth+1));
                }
            }
        }
        catch(EnumerationStoppedException error){errors.Add(error.Message);}
        return new(items,errors,errors.Count==0);
        void Error(string message){guard.Error();errors.Add(message);}
    }

    private List<SourceItem> Resolve(UsbFolderChoice choice,CancellationToken cancel)
        =>Resolve(choice.Segments,MediaKind.Container,cancel);

    public List<SourceItem> ResolveCollection(UsbCollectionChoice choice,CancellationToken cancel)
        =>Resolve(choice.Segments,MediaKind.Collection,cancel);

    private List<SourceItem> Resolve(IReadOnlyList<UsbFolderSegment> segments,MediaKind leafKind,CancellationToken cancel)
    {
        if(segments is not {Count:>0 and <=128}||segments.Any(segment=>
            segment==null||!ValidText(segment.Name)||segment.PersistentId!=null&&!ValidText(segment.PersistentId)))throw Unavailable();
        List<SourceItem> chain=[];var visited=new HashSet<string>(StringComparer.Ordinal){"DEVICE"};
        string parent="DEVICE";
        for(int index=0;index<segments.Count;index++)
        {
            var segment=segments[index];var kind=index==segments.Count-1?leafKind:MediaKind.Container;
            var matches=CompleteChildren(parent,cancel).Where(item=>item.Kind==kind&&item.Name==segment.Name).ToArray();
            if(matches.Length!=1||segment.PersistentId!=null&&matches[0].PersistentId!=segment.PersistentId)throw Unavailable();
            var match=matches[0];
            if(!visited.Add(match.Id))throw Unavailable();
            chain.Add(match);parent=match.Id;
        }
        return chain;
    }

    private List<SourceItem> CompleteChildren(string parent,CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();var result=children(parent,cancel);
        if(!result.EnumerationComplete||result.Errors.Count!=0)throw new IOException(
            "Elenco cartelle USB incompleto. Riprova senza estendere la selezione a tutto il telefono. "+string.Join(" ",result.Errors.Take(3)));
        var ids=new HashSet<string>(StringComparer.Ordinal){"DEVICE",parent};
        foreach(var item in result.Items)
        {
            cancel.ThrowIfCancellationRequested();ValidateItem(item,parent);
            if(!ids.Add(item.Id))throw Unavailable();
        }
        return result.Items;
    }
    private static void ValidateItem(SourceItem item,string parent)
    {
        if(!ValidText(item.Id)||!ValidText(item.Name)||item.ParentId!=parent||
            item.PersistentId!=null&&!ValidText(item.PersistentId))throw Unavailable();
    }
    private static bool ValidText(string? value)=>!string.IsNullOrEmpty(value)&&value.Length<=32768&&!value.Contains('\0');
    private static string Display(string name)=>string.Concat(name.Select(character=>
        character is '%' or '/' or '\\'||char.IsControl(character)?"%"+((int)character).ToString("X2",System.Globalization.CultureInfo.InvariantCulture):character.ToString()));
    private static IOException Unavailable()=>new("La cartella USB scelta è assente, cambiata o non identificabile senza ambiguità. Ripeti la selezione; nessuna estensione automatica a tutto il telefono.");
}
