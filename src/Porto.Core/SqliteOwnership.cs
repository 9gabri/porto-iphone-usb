using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Porto.Core;

internal static class SqliteOwnership
{
    private sealed record Column(string Name,string Type,bool Required=false,int Key=0);
    private sealed record Index(string Table,params string[] Columns);
    private static readonly byte[] JournalMagic=[0xd9,0xd5,0x05,0xf9,0x20,0xa1,0x63,0xd7];

    internal static int ApplicationId(SqliteDatabaseKind kind)=>kind==SqliteDatabaseKind.Catalog?0x50525443:0x50525457;

    // Existing databases are inspected without writing them. A hot rollback
    // journal is recovered only in a private local snapshot until ownership is
    // established; opening the original read/write first would be too late.
    internal static bool Validate(string path,SqliteDatabaseKind kind)
    {
        foreach(string suffix in new[]{"","-journal","-wal","-shm"})Destination.CheckAncestors(path+suffix);
        if(!File.Exists(path))
        {
            if(new[]{"-journal","-wal","-shm"}.Any(s=>File.Exists(path+s)))throw Foreign();
            return false;
        }
        int databasePageSize;
        long databaseLength;
        using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
        {
            byte[] header=new byte[100];
            if(input.Length<header.Length)throw Foreign();
            input.ReadExactly(header);
            if(!header.AsSpan(0,16).SequenceEqual(Encoding.ASCII.GetBytes("SQLite format 3\0")))throw Foreign();
            // Porto has always used rollback journals. Do not adopt WAL files,
            // whose read-only connections can themselves create shared memory.
            if(header[18]!=1||header[19]!=1)throw Foreign();
            databasePageSize=BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(16,2));
            if(databasePageSize==1)databasePageSize=65536;
            databaseLength=input.Length;
            if(!PowerOfTwo((uint)databasePageSize,512,65536)||databaseLength%databasePageSize!=0)throw Foreign();
        }
        if(File.Exists(path+"-wal")||File.Exists(path+"-shm"))throw Foreign();
        string inspect=path;
        string? snapshot=null;
        bool journal=File.Exists(path+"-journal");
        try
        {
            if(journal)
            {
                ValidateJournal(path,databasePageSize,databaseLength);
                string directory=Path.Combine(Path.GetDirectoryName(path)!,".porto-validation-"+Guid.NewGuid().ToString("N"));
                Destination.CheckAncestors(directory);
                if(File.Exists(directory)||Directory.Exists(directory))throw Foreign();
                Directory.CreateDirectory(directory);
                snapshot=directory;
                inspect=Path.Combine(snapshot,Path.GetFileName(path));
                File.Copy(path,inspect,false);File.Copy(path+"-journal",inspect+"-journal",false);
            }
            // Payloads remain under the original destination even when the
            // database itself is inspected in a deeper recovery snapshot.
            string destinationRoot=Path.GetDirectoryName(Path.GetDirectoryName(path)!)!;
            using(var connection=new Sqlite(inspect,readOnly:!journal))ValidateSchema(connection,kind,destinationRoot);
            return true;
        }
        finally
        {
            if(snapshot!=null)RemoveSnapshot(snapshot,inspect);
        }
    }

    private static void RemoveSnapshot(string directory,string database)
    {
        // Only this invocation's two copy targets may be removed, after SQLite
        // closes. Unknown entries and every pre-existing directory are retained.
        if(Path.GetDirectoryName(Path.GetFullPath(database))!=Path.GetFullPath(directory))throw Foreign();
        Destination.CheckAncestors(directory);
        foreach(string file in new[]{database,database+"-journal"})
            if(File.Exists(file)){Destination.CheckAncestors(file);File.Delete(file);}
        if(Directory.Exists(directory)&&!Directory.EnumerateFileSystemEntries(directory).Any())Directory.Delete(directory);
    }

    private static bool PowerOfTwo(uint value,uint minimum,uint maximum)=>value>=minimum&&value<=maximum&&(value&(value-1))==0;

    private static void ValidateJournal(string databasePath,int databasePageSize,long databaseLength)
    {
        using var journal=new FileStream(databasePath+"-journal",FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
        if(journal.Length<28)throw Foreign();
        byte[] header=new byte[28];journal.ReadExactly(header);
        bool hot=header.AsSpan(0,8).SequenceEqual(JournalMagic);
        if(!hot&&header.AsSpan(0,8).ContainsAnyExcept((byte)0))throw Foreign();

        // SQLite readSuperJournal/readMasterJournal trusts the magic in the last
        // eight bytes before following an external pathname. Porto never ATTACHes
        // another database: refuse that footer before even recovering our copy.
        byte[] tail=new byte[8];journal.Position=journal.Length-tail.Length;journal.ReadExactly(tail);
        if(tail.AsSpan().SequenceEqual(JournalMagic))throw Foreign();

        // https://sqlite.org/fileformat2.html#the_rollback_journal
        uint count=BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(8,4));
        uint seed=BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(12,4));
        uint originalPages=BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(16,4));
        uint sector=BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(20,4));
        uint pageSize=BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(24,4));
        if(!PowerOfTwo(sector,32,65536)||!PowerOfTwo(pageSize,512,65536)||pageSize!=databasePageSize||
            originalPages==0||originalPages>0xfffffffe||journal.Length<sector)throw Foreign();
        if(!hot)
        {
            // Before its first sync SQLite leaves a zero magic and record count,
            // but writes the complete padded header. No database page can have
            // changed yet; an arbitrary zero prefix does not establish this state.
            if(count!=0||databaseLength!=(long)originalPages*pageSize)throw Foreign();
            byte[] padding=new byte[(int)sector-header.Length];journal.Position=header.Length;journal.ReadExactly(padding);
            if(padding.AsSpan().ContainsAnyExcept((byte)0))throw Foreign();
            if(journal.Length==sector)return;
        }
        // A magic header with no complete record is not sufficient evidence to
        // let SQLite consume the original file. Bound the declared first segment;
        // open-ended counts still need one complete, checksummed record.
        long requiredRecords=hot&&count>0&&count!=uint.MaxValue?count:1;
        if(journal.Length-sector<requiredRecords*(pageSize+8L))throw Foreign();
        journal.Position=sector;
        byte[] number=new byte[4],page=new byte[databasePageSize],checksum=new byte[4];
        journal.ReadExactly(number);journal.ReadExactly(page);journal.ReadExactly(checksum);
        uint pageNumber=BinaryPrimitives.ReadUInt32BigEndian(number);
        if(pageNumber==0||pageNumber>originalPages)throw Foreign();
        uint calculated=seed;
        for(int index=page.Length-200;index>0;index-=200)calculated=unchecked(calculated+page[index]);
        if(calculated!=BinaryPrimitives.ReadUInt32BigEndian(checksum))throw Foreign();
        if(hot)return; // Native SQLite performs the remaining recovery in the isolated copy.
        using var database=new FileStream(databasePath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
        database.Position=(pageNumber-1L)*databasePageSize;
        byte[] current=new byte[databasePageSize];database.ReadExactly(current);
        if(!current.AsSpan().SequenceEqual(page))throw Foreign();
    }

    private static void ValidateSchema(Sqlite db,SqliteDatabaseKind kind,string destinationRoot)
    {
        int appId=int.Parse(db.Query("PRAGMA application_id").Single()[0]!,CultureInfo.InvariantCulture);
        int version=int.Parse(db.Query("PRAGMA user_version").Single()[0]!,CultureInfo.InvariantCulture);
        bool legacy=appId==0&&version==0;
        if(!legacy&&(appId!=ApplicationId(kind)||version is <0 or >1))throw Foreign();
        var tables=Tables(kind);
        var indices=Indices(kind);
        var present=new HashSet<string>(StringComparer.Ordinal);
        bool hasReplacement=false;
        foreach(var entry in db.Query("SELECT type,name,tbl_name,sql FROM sqlite_master"))
        {
            string type=entry[0]!,name=entry[1]!,table=entry[2]!;
            if(type=="table")
            {
                if(!tables.TryGetValue(name,out var expected))throw Foreign();
                var actual=db.Query($"PRAGMA table_xinfo({name})");
                bool oldCatalog=name=="files"&&actual.Count==7;
                var columns=oldCatalog?expected[..7]:expected;
                if(!MatchColumns(actual,columns)||!MatchesSql(entry[3],TableSql(name,columns)))throw Foreign();
                if(db.Query($"PRAGMA foreign_key_list({name})").Count!=0)throw Foreign();
                if(name=="files")hasReplacement=!oldCatalog;
                present.Add(name);
            }
            else if(type=="index")
            {
                if(!tables.TryGetValue(table,out var columns))throw Foreign();
                string[] expected;
                if(name=="sqlite_autoindex_"+table+"_1"&&entry[3]==null)
                    expected=columns.Where(c=>c.Key>0).OrderBy(c=>c.Key).Select(c=>c.Name).ToArray();
                else if(indices.TryGetValue(name,out var index)&&index.Table==table&&
                    MatchesSql(entry[3],$"CREATE INDEX {name} ON {table}({string.Join(",",index.Columns)})"))expected=index.Columns;
                else throw Foreign();
                string quotedName="\""+name.Replace("\"","\"\"")+"\"";
                if(expected.Length==0||!db.Query($"PRAGMA index_info({quotedName})").Select(r=>r[2]).SequenceEqual(expected))throw Foreign();
            }
            else throw Foreign(); // No Porto version creates views or triggers.
        }
        if(legacy)
        {
            string[] required=kind==SqliteDatabaseKind.Catalog?["files","runs","run_items"]:["work"];
            if(required.Any(t=>!present.Contains(t)))throw Foreign();
        }
        if(kind==SqliteDatabaseKind.Catalog&&present.Contains("files"))CatalogValidation.Read(db,hasReplacement,destinationRoot);
    }

    private static string TableSql(string name,Column[] columns)
    {
        string[] keys=columns.Where(column=>column.Key>0).OrderBy(column=>column.Key).Select(column=>column.Name).ToArray();
        string definitions=string.Join(",",columns.Select(column=>column.Name+" "+column.Type+(column.Required?" NOT NULL":"")+(column.Key>0&&keys.Length==1?" PRIMARY KEY":"")));
        if(keys.Length>1)definitions+=",PRIMARY KEY("+string.Join(",",keys)+")";
        return $"CREATE TABLE {name}({definitions})";
    }

    private static bool MatchesSql(string? actual,string expected)=>actual!=null&&NormalizeSql(actual)==NormalizeSql(expected);
    private static string NormalizeSql(string sql)=>new string(sql.Where(c=>!char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    private static bool MatchColumns(List<string?[]> actual,Column[] expected)=>actual.Count==expected.Length&&
        actual.Zip(expected).All(pair=>pair.First[1]==pair.Second.Name&&pair.First[2]==pair.Second.Type&&
            pair.First[3]==(pair.Second.Required?"1":"0")&&pair.First[4]==null&&pair.First[5]==pair.Second.Key.ToString(CultureInfo.InvariantCulture)&&
            pair.First.Length==7&&pair.First[6]=="0");

    private static Dictionary<string,Column[]> Tables(SqliteDatabaseKind kind)=>kind==SqliteDatabaseKind.Catalog?new(StringComparer.Ordinal)
    {
        ["files"]=[new("id","TEXT",Key:1),new("state","TEXT",true),new("temp","TEXT",true),new("final","TEXT",true),new("hash","TEXT"),new("length","INTEGER",true),new("source","TEXT",true),new("replacement","TEXT")],
        ["runs"]=[new("id","TEXT",Key:1),new("report","TEXT",true)],
        ["run_items"]=[new("run","TEXT",true),new("item","TEXT",true)]
    }:new(StringComparer.Ordinal)
    {
        ["work"]=[new("id","TEXT",Key:1),new("source","TEXT",true),new("outcome","TEXT",true),new("detail","TEXT",true)],
        ["unread_work"]=[new("id","TEXT",Key:1)],
        ["snapshot_members"]=[new("snapshot","TEXT",true,1),new("source_id","TEXT",true,2)],
        ["fingerprints"]=[new("source_id","TEXT",Key:1),new("hash","TEXT",true),new("length","INTEGER",true)],
        ["snapshot_counts"]=[new("snapshot","TEXT",true,1),new("hash","TEXT",true,2),new("length","INTEGER",true,3),new("count","INTEGER",true)],
        ["fingerprint_conflicts"]=[new("source_id","TEXT",Key:1),new("source","TEXT",true),new("detail","TEXT",true)]
    };

    private static Dictionary<string,Index> Indices(SqliteDatabaseKind kind)=>kind==SqliteDatabaseKind.Catalog?new(StringComparer.Ordinal)
    {
        ["ix_hash"]=new("files","hash","length")
    }:new(StringComparer.Ordinal)
    {
        ["ix_snapshot_source"]=new("snapshot_members","source_id","snapshot"),
        ["ix_fingerprint_content"]=new("fingerprints","hash","length"),
        ["ix_snapshot_content"]=new("snapshot_counts","hash","length","count")
    };

    private static IOException Foreign()=>new("Il catalogo o i suoi file ausiliari non appartengono a una versione Porto riconosciuta. Sono stati conservati senza modifiche: scegli un'altra destinazione.");
}
