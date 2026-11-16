using System.Globalization;
using System.Text.RegularExpressions;
using Porto.Core;

namespace Porto.Apple;

internal static partial class AppleCatalogDecoder
{
    internal static AppleGalleryCatalog Decode(byte[] image,CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        using var db=new AppleMemorySqlite(image,cancel);
        var schema=db.Query("SELECT name,type,sql FROM sqlite_schema",10000);
        var tables=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var row in schema)
        {
            if(row[1]!="table"||row[0]==null||row[2]==null)continue;
            if(!tables.TryAdd(row[0]!,row[2]!))throw BadSchema();
        }
        RequireTable(db,tables,"ZASSET",["Z_PK","ZUUID","ZDIRECTORY","ZFILENAME","ZKIND","ZKINDSUBTYPE","ZHIDDEN","ZTRASHEDSTATE"]);
        RequireTable(db,tables,"ZGENERICALBUM",["Z_PK","ZUUID","ZTITLE","ZKIND","ZTRASHEDSTATE"]);
        var joins=tables.Keys.Where(n=>JoinTable().IsMatch(n)).Select(name=>(Name:name,Columns:Columns(db,name))).Where(t=>
            t.Columns.Count(c=>AlbumColumn().IsMatch(c))==1&&t.Columns.Count(c=>AssetColumn().IsMatch(c))==1).ToArray();
        if(joins.Length!=1)throw BadSchema();
        var join=joins[0];
        RequireTable(db,tables,join.Name,[]);
        string albumColumn=join.Columns.Single(c=>AlbumColumn().IsMatch(c));
        string assetColumn=join.Columns.Single(c=>AssetColumn().IsMatch(c));

        var assets=new List<AppleAsset>();var knownAssets=new HashSet<long>();var visible=new Dictionary<long,AppleAsset>();
        var ids=new HashSet<string>(StringComparer.Ordinal);int hidden=0,trashed=0;
        foreach(var row in db.Query("SELECT Z_PK,ZUUID,ZDIRECTORY,ZFILENAME,ZKIND,ZKINDSUBTYPE,ZHIDDEN,ZTRASHEDSTATE FROM ZASSET",1_000_000))
        {
            cancel.ThrowIfCancellationRequested();
            long key=Number(row[0]);if(key<=0||!knownAssets.Add(key))throw BadSchema();
            string id=Identifier(row[1]);if(!ids.Add(id))throw BadSchema();
            long hiddenValue=Number(row[6]),trashValue=Number(row[7]);
            if(hiddenValue is not(0 or 1)||trashValue<0)throw BadSchema();
            if(trashValue!=0){trashed++;continue;}
            if(hiddenValue!=0){hidden++;continue;}
            long kind=Number(row[4]),subtype=Number(row[5]);
            MediaKind media=kind switch{0=>MediaKind.Image,1=>MediaKind.Video,_=>MediaKind.Unknown};
            string? path=OriginalPath(row[2],row[3]);
            string name=SafeFilename(row[3])?row[3]!:(media==MediaKind.Video?"Video senza originale":media==MediaKind.Image?"Foto senza originale":"Elemento Foto sconosciuto");
            var asset=new AppleAsset(key,id,name,path,media,kind==0&&subtype==2);
            assets.Add(asset);visible.Add(key,asset);
        }
        var knownAlbums=new HashSet<long>();var userAlbums=new Dictionary<long,(string Id,string Name)>();var albumIds=new HashSet<string>(StringComparer.Ordinal);
        var parents=new Dictionary<long,(long Kind,long Parent,string? Title,long Trashed)>();
        bool hasParent=Columns(db,"ZGENERICALBUM").Contains("ZPARENTFOLDER",StringComparer.Ordinal);
        foreach(var row in db.Query("SELECT Z_PK,ZUUID,ZTITLE,ZKIND,ZTRASHEDSTATE,"+(hasParent?"ZPARENTFOLDER":"NULL")+" FROM ZGENERICALBUM",100000))
        {
            long key=Number(row[0]);if(key<=0||!knownAlbums.Add(key))throw BadSchema();
            long kind=Number(row[3]),trash=Number(row[4]);if(trash<0)throw BadSchema();
            long parent=row[5]==null?0:Number(row[5]);if(parent<0)throw BadSchema();
            parents.Add(key,(kind,parent,row[2],trash));
            if(kind!=2||trash!=0)continue;
            string id=Identifier(row[1]);if(!albumIds.Add(id))throw BadSchema();
            string title=row[2]??"Album senza nome";
            if(title.Length>1024||title.Any(char.IsControl))throw BadSchema();
            userAlbums.Add(key,(id,title));
        }
        var memberKeys=userAlbums.Keys.ToDictionary(key=>key,_=>new HashSet<long>());
        foreach(var row in db.Query($"SELECT \"{albumColumn}\",\"{assetColumn}\" FROM \"{join.Name}\"",2_000_000))
        {
            long album=Number(row[0]),asset=Number(row[1]);
            if(!knownAlbums.Contains(album)||!knownAssets.Contains(asset))throw new IOException("Il catalogo Foto contiene associazioni album non risolte; nessun album viene ampliato automaticamente.");
            if(memberKeys.TryGetValue(album,out var keys)&&!keys.Add(asset))throw BadSchema();
        }
        var albums=new List<AppleAlbum>();var members=new Dictionary<string,IReadOnlyList<AppleAsset>>(StringComparer.Ordinal);
        foreach(var (key,album) in userAlbums)
        {
            var selection=memberKeys[key].Where(visible.ContainsKey).Select(k=>visible[k]).OrderBy(a=>a.Key).ToList();
            members.Add(album.Id,selection.AsReadOnly());
            string displayName=album.Name;
            if(hasParent)
            {
                var path=new List<string>{album.Name};var visited=new HashSet<long>{key};long parent=parents[key].Parent;
                while(parent!=0)
                {
                    if(!visited.Add(parent)||visited.Count>128||!parents.TryGetValue(parent,out var folder))throw BadSchema();
                    if(folder.Kind==3999)break;
                    if(folder.Kind!=4000||folder.Trashed!=0||string.IsNullOrWhiteSpace(folder.Title)||folder.Title.Length>1024||folder.Title.Any(char.IsControl))throw BadSchema();
                    path.Add(folder.Title);parent=folder.Parent;
                }
                path.Reverse();displayName=string.Join(" / ",path);if(displayName.Length>4096)throw BadSchema();
            }
            albums.Add(new(album.Id,displayName,selection.Count,selection.Count(a=>a.Kind==MediaKind.Image),selection.Count(a=>a.Kind==MediaKind.Video)));
        }
        return new(assets,albums.OrderBy(a=>a.Name,StringComparer.CurrentCulture).ToList(),members,hidden,trashed);
    }
    private static void RequireTable(AppleMemorySqlite db,Dictionary<string,string> tables,string name,string[] required)
    {
        if(!tables.TryGetValue(name,out var sql)||!Regex.IsMatch(sql,@"\ACREATE\s+TABLE\s",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)||
           Regex.IsMatch(sql,@"\b(GENERATED|AS\s*\(|WITHOUT\s+ROWID)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))throw BadSchema();
        var columns=Columns(db,name);if(required.Any(r=>!columns.Contains(r,StringComparer.Ordinal)))throw BadSchema();
    }
    private static string[] Columns(AppleMemorySqlite db,string table)
    {
        if(!Regex.IsMatch(table,@"\A[A-Za-z0-9_]+\z",RegexOptions.CultureInvariant))throw BadSchema();
        var rows=db.Query($"PRAGMA table_xinfo(\"{table}\")",10000);
        if(rows.Any(r=>r.Length!=7||r[6]!="0"))throw BadSchema();
        return rows.Select(r=>r[1]??throw BadSchema()).ToArray();
    }
    internal static string? OriginalPath(string? directory,string? filename)
    {
        if(directory==null||!SafeFilename(filename)||directory.Length>2048||directory.Contains('\\')||directory.Contains('\0'))return null;
        string clean=directory.TrimStart('/');
        if(!clean.StartsWith("DCIM/",StringComparison.Ordinal)||clean.Split('/').Any(p=>p is "" or "." or ".."||p.Any(char.IsControl)||p.Contains(':')))return null;
        return "/"+clean+"/"+filename;
    }
    private static bool SafeFilename(string? name)=>!string.IsNullOrWhiteSpace(name)&&name.Length<=255&&name is not("." or "..")&&!name.Any(c=>char.IsControl(c)||c is '/' or '\\' or ':');
    private static long Number(string? value)=>long.TryParse(value,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var result)?result:throw BadSchema();
    private static string Identifier(string? value)=>!string.IsNullOrWhiteSpace(value)&&value.Length<=128&&!value.Any(char.IsControl)?value:throw BadSchema();
    private static IOException BadSchema()=>new("Struttura del catalogo Foto non riconosciuta o incoerente. Importazione arrestata senza passare alla lista dei file USB.");
    [GeneratedRegex(@"\AZ_\d+ASSETS\z",RegexOptions.CultureInvariant)] private static partial Regex JoinTable();
    [GeneratedRegex(@"\AZ_\d+ALBUMS\z",RegexOptions.CultureInvariant)] private static partial Regex AlbumColumn();
    [GeneratedRegex(@"\AZ_\d+ASSETS\z",RegexOptions.CultureInvariant)] private static partial Regex AssetColumn();
}
