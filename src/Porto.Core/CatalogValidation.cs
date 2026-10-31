using System.Globalization;
using System.Text.Json;

namespace Porto.Core;

internal static class CatalogValidation
{
    // These are the relative names emitted by every released Porto version.
    // Recognizing them constrains recovery; it does not authenticate a catalog.
    internal static List<CatalogEntry> Read(Sqlite db,bool hasReplacement,string destinationRoot)
    {
        string[] textColumns=hasReplacement?["id","state","temp","final","hash","source","replacement"]:["id","state","temp","final","hash","source"];
        string invalidText=string.Join(" OR ",textColumns.Select(column=>$"({column} IS NOT NULL AND (typeof({column})<>'text' OR instr({column},char(0))>0))"));
        // sqlite3_column_text exposes a NUL-terminated string. Reject embedded
        // NULs in SQLite before marshaling can hide an invalid path suffix.
        if(db.Query($"SELECT 1 FROM files WHERE typeof(length)<>'integer' OR {invalidText} LIMIT 1").Count!=0)throw Invalid();
        string replacement=hasReplacement?"replacement":"NULL";
        List<CatalogEntry> entries=[];
        var ids=new Dictionary<string,CatalogEntry>(StringComparer.Ordinal);
        var finals=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var row in db.Query($"SELECT id,state,temp,final,hash,length,source,{replacement} FROM files"))
        {
            if(!long.TryParse(row[5],NumberStyles.Integer,CultureInfo.InvariantCulture,out long length))throw Invalid();
            var entry=new CatalogEntry(row[0]!,row[1]!,row[2]!,row[3]!,row[4],length,ReadSource(row[6]),row[7]);
            Validate(entry);
            if(!ids.TryAdd(entry.Id,entry)||entry.Final.Length!=0&&!finals.Add(entry.Final))throw Invalid();
            entries.Add(entry);
        }
        foreach(var entry in entries.Where(entry=>entry.Replaces!=null))
            if(entry.Replaces==entry.Id||!ids.TryGetValue(entry.Replaces!,out var previous)||previous.Hash!=entry.Hash||previous.Length!=entry.Length)throw Invalid();
        RejectReplacementCycles(ids);
        foreach(var entry in entries)
        {
            Destination.CheckAncestors(Path.Combine(destinationRoot,entry.Temp));
            if(entry.Final.Length!=0)Destination.CheckAncestors(Path.Combine(destinationRoot,entry.Final));
        }
        return entries; // No caller can process an earlier row before all rows pass.
    }

    private static void RejectReplacementCycles(Dictionary<string,CatalogEntry> entries)
    {
        var traversals=new Dictionary<string,int>(StringComparer.Ordinal);
        int traversal=0;
        foreach(var entry in entries.Values)
        {
            traversal++;
            var current=entry;
            // Each row is visited once. A previously completed walk is safe;
            // reaching a row again in this same walk proves a cycle.
            while(true)
            {
                if(traversals.TryGetValue(current.Id,out int previous))
                {
                    if(previous==traversal)throw Invalid();
                    break;
                }
                traversals.Add(current.Id,traversal);
                if(current.Replaces==null)break;
                current=entries[current.Replaces];
            }
        }
    }

    internal static void Validate(CatalogEntry entry)
    {
        if(!Uuid(entry.Id)||entry.Temp!=Path.Combine(".porto",entry.Id+".part")||entry.Final==null||entry.Length<0)throw Invalid();
        ValidateSource(entry.Source);
        if(entry.State is not ("Copying" or "Ready" or "Complete" or "Quarantined" or "Missing" or "Damaged" or "Replaced" or "Abandoned"))throw Invalid();
        if(entry.Replaces!=null&&(!Uuid(entry.Replaces)||entry.Replaces==entry.Id))throw Invalid();
        if(entry.Hash==null)
        {
            if(entry.State is not ("Copying" or "Abandoned")||entry.Length!=0||entry.Final.Length!=0||entry.Replaces!=null)throw Invalid();
        }
        else if(entry.Hash.Length!=64||entry.Hash.Any(c=>c is not (>= '0' and <= '9' or >= 'A' and <= 'F'))||entry.State=="Copying")throw Invalid();
        if((entry.State is "Ready" or "Complete")&&entry.Final.Length==0)throw Invalid();
        if(entry.State=="Quarantined"&&entry.Final.Length!=0)throw Invalid();
        if(entry.Final.Length==0)return;
        string extension=Path.GetExtension(entry.Final);
        if(extension is not (".mov" or ".mp4" or ".m4v"))throw Invalid();
        string name=Path.GetFileNameWithoutExtension(entry.Final);
        // Verified reuse can update Source.Name while the published name stays
        // unchanged; collision recovery can also use a fresh UUID suffix.
        if(!GeneratedName(name)||entry.Final!=Path.Combine("Video Porto",name+extension))throw Invalid();
    }

    private static SourceItem ReadSource(string? json)
    {
        if(json==null)throw Invalid();
        try
        {
            using var document=JsonDocument.Parse(json);
            if(document.RootElement.ValueKind!=JsonValueKind.Object)throw Invalid();
            var properties=new HashSet<string>(StringComparer.Ordinal);
            foreach(var property in document.RootElement.EnumerateObject())
                if(property.Name is not ("Id" or "ParentId" or "Name" or "Kind" or "Size" or "PersistentId" or "Format" or "Offline")||!properties.Add(property.Name))throw Invalid();
            if(!new[]{"Id","ParentId","Name","Kind"}.All(properties.Contains))throw Invalid();
            var source=document.RootElement.Deserialize<SourceItem>();
            ValidateSource(source);
            return source!;
        }
        catch(JsonException error){throw Invalid(error);}
    }

    private static void ValidateSource(SourceItem? source)
    {
        if(source==null||string.IsNullOrEmpty(source.Id)||source.ParentId==null||string.IsNullOrEmpty(source.Name)||
            !Enum.IsDefined(source.Kind)||source.Size is <0||
            new[]{source.Id,source.ParentId,source.Name,source.PersistentId,source.Format}.Any(value=>value?.Contains('\0')==true))throw Invalid();
    }

    private static bool Uuid(string? value)=>value is {Length:32}&&value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool GeneratedName(string name)
    {
        if(name.Length<36||name[^33]!='_')return false;
        string stem=name[..^33];
        return stem.Length is >=3 and <=67&&stem.StartsWith("V_",StringComparison.Ordinal)&&
            stem==Destination.SafeStem(stem[2..]+".mov")&&Uuid(name[^32..]);
    }
    private static InvalidDataException Invalid(Exception? inner=null)=>new("Il catalogo contiene dati o percorsi non riconosciuti. I file sono stati conservati: scegli un'altra destinazione o conserva il catalogo per la diagnosi.",inner);
}
