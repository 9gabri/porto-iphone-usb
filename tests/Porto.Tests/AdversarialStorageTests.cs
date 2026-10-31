using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Porto.Core;
using static Fixtures;

internal static class AdversarialStorageTests
{
    public static void Add(List<(string,Action)> tests)
    {
        tests.Add(("state hard links are rejected without changing unrelated files",()=>
        {
            foreach(string name in new[]{"import.lock","catalog.db","work.db","catalog.db-journal"})
            {
                string root=Root(),external=Path.Combine(Root(),"unrelated.bin");
                byte[] original=Encoding.UTF8.GetBytes("unrelated synthetic data");File.WriteAllBytes(external,original);
                string state=Path.Combine(root,".porto");Directory.CreateDirectory(state);
                HardLink(Path.Combine(state,name),external);
                Fixtures.Throws<IOException>(()=>{using var destination=new Destination(root);});
                Fixtures.Check(File.ReadAllBytes(external).SequenceEqual(original));
            }
        }));
        tests.Add(("a resumed hard-linked partial never appends to the unrelated file",()=>
        {
            using var destination=new Destination(Root());byte[] video=Video(),prefix=video[..10003];
            string external=Path.Combine(Root(),"unrelated-prefix.bin");File.WriteAllBytes(external,prefix);
            string id=Guid.NewGuid().ToString("N"),relative=Path.Combine(".porto",id+".part");
            var item=FakeSource.One(video).Inventory(default).Items.Single();
            using(var catalog=new Catalog(destination))catalog.Save(new(id,"Copying",relative,"",null,0,item));
            HardLink(Path.Combine(destination.Root,relative),external);
            var report=new ImportEngine(FakeSource.One(video),destination).Run(false,default);
            Fixtures.Check(!report.Status.StartsWith("Completati")&&report.Count(Outcome.Imported)==0);
            Fixtures.Check(File.ReadAllBytes(external).SequenceEqual(prefix));
        }));
        tests.Add(("foreign SQLite database is rejected before any mutation",()=>
        {
            using var destination=new Destination(Root());string path=Path.Combine(destination.State,"catalog.db");
            using(var db=new FixtureDatabase(path))db.Execute("CREATE TABLE private_data(value TEXT); INSERT INTO private_data VALUES('preserve me');");
            byte[] before=SHA256.HashData(File.ReadAllBytes(path));
            Fixtures.Throws<IOException>(()=>{using var catalog=new Catalog(destination);});
            Fixtures.Check(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))));
        }));
        tests.Add(("foreign journal alongside a Porto catalog is preserved and rejected",()=>
        {
            using var destination=new Destination(Root());using(var catalog=new Catalog(destination)){}
            string path=Path.Combine(destination.State,"catalog.db"),journal=path+"-journal";
            byte[] original=Encoding.UTF8.GetBytes("unrelated journal-name collision");File.WriteAllBytes(journal,original);
            byte[] before=SHA256.HashData(File.ReadAllBytes(path));
            Fixtures.Throws<IOException>(()=>{using var catalog=new Catalog(destination);});
            Fixtures.Check(File.ReadAllBytes(journal).SequenceEqual(original)&&before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))));
        }));
        foreach(var sample in new (string Name,byte[] Bytes)[]{("empty",[]),("single zero",[0]),("zero prefix and foreign payload",new byte[8].Concat(Encoding.UTF8.GetBytes("foreign data must survive")).ToArray()),("zero sector",new byte[512])})
        tests.Add(("unattributable journal is preserved: "+sample.Name,()=>
        {
            using var destination=new Destination(Root());using(var catalog=new Catalog(destination)){}
            string path=Path.Combine(destination.State,"catalog.db"),journal=path+"-journal";
            File.WriteAllBytes(journal,sample.Bytes);byte[] before=SHA256.HashData(File.ReadAllBytes(path));
            Fixtures.Throws<IOException>(()=>{using var catalog=new Catalog(destination);});
            Fixtures.Check(File.Exists(journal)&&File.ReadAllBytes(journal).SequenceEqual(sample.Bytes));
            Fixtures.Check(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))));
        }));
        tests.Add(("generated columns cannot hide foreign work schema from ownership checks",()=>
        {
            using var destination=new Destination(Root());string path=Path.Combine(destination.State,"work.db");
            using(var db=new FixtureDatabase(path))db.Execute("CREATE TABLE work(id TEXT PRIMARY KEY,source TEXT NOT NULL,outcome TEXT NOT NULL,detail TEXT NOT NULL,secret TEXT GENERATED ALWAYS AS ('foreign') VIRTUAL);");
            byte[] before=SHA256.HashData(File.ReadAllBytes(path));
            Fixtures.Throws<IOException>(()=>{using var ledger=new WorkLedger(destination);});
            Fixtures.Check(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))));
        }));
        foreach(var sample in new (string Name,bool Hot,int TrailingBytes,bool ForeignPadding)[]{("hot header without a record",true,0,false),("hot header with one record byte",true,1,false),("non-hot header with foreign padding",false,0,true)})
        tests.Add(("incomplete or foreign journal structure is preserved: "+sample.Name,()=>
        {
            using var destination=new Destination(Root());using(var catalog=new Catalog(destination)){}
            string path=Path.Combine(destination.State,"catalog.db"),journal=path+"-journal";
            byte[] database=File.ReadAllBytes(path),bytes=JournalHeader(database,sample.Hot,sample.Hot?1u:0u,sample.TrailingBytes);
            if(sample.ForeignPadding)bytes[28]=0x41;
            File.WriteAllBytes(journal,bytes);
            Fixtures.Throws<IOException>(()=>{using var catalog=new Catalog(destination);});
            Fixtures.Check(File.ReadAllBytes(path).SequenceEqual(database)&&File.ReadAllBytes(journal).SequenceEqual(bytes));
            Fixtures.Check(Directory.GetDirectories(destination.State,".porto-validation-*").Length==0);
        }));
        tests.Add(("recognized legacy SQLite schemas migrate without losing records",()=>
        {
            using var destination=new Destination(Root());string catalogPath=Path.Combine(destination.State,"catalog.db");
            using(var db=new FixtureDatabase(catalogPath)){db.Execute(LegacyCatalog);db.Execute("INSERT INTO runs VALUES('old','preserve legacy report');");}
            using(var catalog=new Catalog(destination))Fixtures.Check(catalog.Entries().Count==0);
            byte[] header=File.ReadAllBytes(catalogPath);
            Fixtures.Check(BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(68,4))==0x50525443);
            using(var db=new FixtureDatabase(catalogPath))Fixtures.Check(db.Scalar("SELECT COUNT(*) FROM runs WHERE id='old'")==1);
            string workPath=Path.Combine(destination.State,"work.db");
            using(var db=new FixtureDatabase(workPath))db.Execute("CREATE TABLE work(id TEXT PRIMARY KEY,source TEXT NOT NULL,outcome TEXT NOT NULL,detail TEXT NOT NULL);");
            using(var ledger=new WorkLedger(destination))Fixtures.Check(ledger.Unresolved().Count==0);
            Fixtures.Check(BinaryPrimitives.ReadInt32BigEndian(File.ReadAllBytes(workPath).AsSpan(68,4))==0x50525457);
        }));
        foreach(string mutation in new[]{"PRAGMA application_id=1234","PRAGMA user_version=99","CREATE TABLE unexpected(value TEXT)","CREATE TRIGGER unexpected AFTER INSERT ON runs BEGIN DELETE FROM files; END"})
        tests.Add(("unrecognized catalog ownership or schema is rejected: "+mutation,()=>
        {
            using var destination=new Destination(Root());using(var catalog=new Catalog(destination)){}
            string path=Path.Combine(destination.State,"catalog.db");using(var db=new FixtureDatabase(path))db.Execute(mutation);
            byte[] before=SHA256.HashData(File.ReadAllBytes(path));
            Fixtures.Throws<IOException>(()=>{using var catalog=new Catalog(destination);});
            Fixtures.Check(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))));
        }));
        tests.Add(("legacy hot rollback journal recovers after ownership validation",()=>
        {
            string root=Root();Crash(root,"journal");
            string path=Path.Combine(root,".porto","catalog.db");Fixtures.Check(File.Exists(path+"-journal"));
            using(var destination=new Destination(root))using(var catalog=new Catalog(destination))Fixtures.Check(catalog.Entries().Count==0);
            using var db=new FixtureDatabase(path);Fixtures.Check(db.Scalar("SELECT COUNT(*) FROM runs WHERE id='unfinished'")==0);
        }));
        tests.Add(("complete native non-hot journal remains recoverable",()=>
        {
            string root=Root();Crash(root,"journal-nonhot");
            string path=Path.Combine(root,".porto","catalog.db"),journal=path+"-journal";
            byte[] bytes=File.ReadAllBytes(journal);Fixtures.Check(bytes.Length>=28&&bytes.Take(8).All(b=>b==0));
            using(var destination=new Destination(root))using(var catalog=new Catalog(destination))Fixtures.Check(catalog.Entries().Count==0);
            using var db=new FixtureDatabase(path);Fixtures.Check(db.Scalar("SELECT COUNT(*) FROM runs WHERE id='saved' AND report='committed'")==1);
        }));
        foreach(string phase in new[]{"journal","journal-nonhot"})
        tests.Add(("super-journal footer cannot touch an external file: "+phase,()=>
        {
            string root=Root();Crash(root,phase);
            string path=Path.Combine(root,".porto","catalog.db"),journal=path+"-journal";
            string foreign=Path.Combine(Root(),"foreign-super-journal.bin");byte[] original=Encoding.UTF8.GetBytes("external synthetic data");File.WriteAllBytes(foreign,original);
            AppendSuperJournal(journal,foreign,File.ReadAllBytes(path));
            byte[] databaseHash=SHA256.HashData(File.ReadAllBytes(path)),journalHash=SHA256.HashData(File.ReadAllBytes(journal));
            using var destination=new Destination(root);
            Fixtures.Throws<IOException>(()=>{using var catalog=new Catalog(destination);});
            Fixtures.Check(File.Exists(foreign)&&File.ReadAllBytes(foreign).SequenceEqual(original));
            Fixtures.Check(databaseHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path)))&&journalHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(journal))));
            Fixtures.Check(Directory.GetDirectories(destination.State,".porto-validation-*").Length==0);
        }));
        tests.Add(("failed atomic report writes expose no partial final file",()=>
        {
            string path=Path.Combine(Root(),"report.json");
            Fixtures.Throws<IOException>(()=>AtomicFile.WriteNew(path,stream=>{stream.Write(Encoding.UTF8.GetBytes("{\"status\":"));throw new IOException("synthetic disk full");}));
            Fixtures.Check(!File.Exists(path));
            AtomicFile.WriteNew(path,stream=>stream.Write(Encoding.UTF8.GetBytes("{\"ok\":true}")));
            using var document=JsonDocument.Parse(File.ReadAllText(path));Fixtures.Check(document.RootElement.GetProperty("ok").GetBoolean());
            byte[] before=File.ReadAllBytes(path);
            Fixtures.Throws<IOException>(()=>AtomicFile.WriteNew(path,stream=>stream.Write(Encoding.UTF8.GetBytes("replacement must not win"))));
            Fixtures.Check(File.ReadAllBytes(path).SequenceEqual(before));
        }));
        tests.Add(("process termination during report write leaves only a temporary file",()=>
        {
            string root=Root(),path=Path.Combine(root,"report-crash.json");Crash(root,"report");
            Fixtures.Check(!File.Exists(path)&&Directory.GetFiles(root,".porto-writing-*.tmp").Length==1);
            AtomicFile.WriteNew(path,stream=>JsonSerializer.Serialize(stream,new{complete=true}));
            using var document=JsonDocument.Parse(File.ReadAllText(path));Fixtures.Check(document.RootElement.GetProperty("complete").GetBoolean());
        }));
        tests.Add(("report JSON and text are published complete",()=>
        {
            string root=Root();var report=new ImportReport{Status="synthetic completed report"};
            ReportWriter.Write(root,"test",report);
            using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"report-test.json")));
            Fixtures.Check(json.RootElement.GetProperty("Status").GetString()==report.Status);
            Fixtures.Check(File.ReadAllText(Path.Combine(root,"report-test.txt")).Contains(report.Status));
            Fixtures.Check(Directory.GetFiles(root,".porto-writing-*.tmp").Length==0);
        }));
        tests.Add(("handler depth cannot impersonate the moov trak mdia hierarchy",()=>
        {
            byte[] handler=new byte[12];Encoding.ASCII.GetBytes("vide").CopyTo(handler,8);
            byte[] hdlr=Atom("hdlr",handler);
            foreach(byte[] tree in new[]{Atom("moov",Atom("moov",Atom("moov",hdlr))),Atom("moov",Atom("trak",Atom("trak",hdlr))),Atom("moov",Atom("mdia",hdlr))})
            {
                string path=Path.Combine(Root(),"wrong-hierarchy.mov");File.WriteAllBytes(path,tree.Concat(Atom("mdat",[1])).ToArray());
                var inspection=MediaInspector.Inspect(path,default);Fixtures.Check(!inspection.IsVideo&&inspection.Ambiguous);
            }
            string valid=Path.Combine(Root(),"valid-metadata.mov");File.WriteAllBytes(valid,Video());
            Fixtures.Check(MediaInspector.Inspect(valid,default) is {IsVideo:true,Ambiguous:false});
        }));
    }

    public static int Child(string root,string phase)
    {
        if(phase=="report")
            AtomicFile.WriteNew(Path.Combine(root,"report-crash.json"),stream=>
            {stream.Write(Encoding.UTF8.GetBytes("{\"status\":"));stream.Flush();Environment.Exit(77);});
        if(phase is "journal" or "journal-nonhot")
        {
            using var destination=new Destination(root);
            using var db=new FixtureDatabase(Path.Combine(destination.State,"catalog.db"));
            db.Execute(LegacyCatalog);
            if(phase=="journal")
                db.Execute("PRAGMA synchronous=FULL; PRAGMA cache_size=1; BEGIN IMMEDIATE; INSERT INTO runs VALUES('unfinished',CAST(zeroblob(2097152) AS TEXT));");
            else
            {
                db.Execute("INSERT INTO runs VALUES('saved','committed');");
                db.Execute("PRAGMA synchronous=FULL; PRAGMA cache_size=10000; BEGIN IMMEDIATE; UPDATE runs SET report='unfinished' WHERE id='saved';");
            }
            Environment.Exit(77);
        }
        return 2;
    }

    private const string LegacyCatalog="CREATE TABLE files(id TEXT PRIMARY KEY,state TEXT NOT NULL,temp TEXT NOT NULL,final TEXT NOT NULL,hash TEXT,length INTEGER NOT NULL,source TEXT NOT NULL); CREATE INDEX ix_hash ON files(hash,length); CREATE TABLE runs(id TEXT PRIMARY KEY,report TEXT NOT NULL); CREATE TABLE run_items(run TEXT NOT NULL,item TEXT NOT NULL);";
    private static byte[] JournalHeader(byte[] database,bool hot,uint count,int trailingBytes)
    {
        int pageSize=BinaryPrimitives.ReadUInt16BigEndian(database.AsSpan(16,2));if(pageSize==1)pageSize=65536;
        byte[] bytes=new byte[512+trailingBytes];
        if(hot)new byte[]{0xd9,0xd5,0x05,0xf9,0x20,0xa1,0x63,0xd7}.CopyTo(bytes,0);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8),count);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(12),123456);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16),(uint)(database.Length/pageSize));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20),512);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(24),(uint)pageSize);
        return bytes;
    }
    private static void AppendSuperJournal(string journal,string external,byte[] database)
    {
        int pageSize=BinaryPrimitives.ReadUInt16BigEndian(database.AsSpan(16,2));if(pageSize==1)pageSize=65536;
        byte[] name=Encoding.UTF8.GetBytes(external),pageNumber=new byte[4],footer=new byte[16];
        BinaryPrimitives.WriteUInt32BigEndian(pageNumber,(uint)(0x40000000/pageSize+1));
        BinaryPrimitives.WriteUInt32BigEndian(footer,(uint)name.Length);
        uint checksum=0;foreach(byte value in name)checksum=unchecked(checksum+value);
        BinaryPrimitives.WriteUInt32BigEndian(footer.AsSpan(4),checksum);
        new byte[]{0xd9,0xd5,0x05,0xf9,0x20,0xa1,0x63,0xd7}.CopyTo(footer,8);
        using var stream=new FileStream(journal,FileMode.Append,FileAccess.Write,FileShare.None);
        stream.Write(pageNumber);stream.Write(name);stream.Write(footer);stream.Flush(true);
    }
    private static void Crash(string root,string phase)
    {
        var info=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true};
        if(Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet",StringComparison.OrdinalIgnoreCase))info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        foreach(string argument in new[]{"--storage-crash",root,phase})info.ArgumentList.Add(argument);
        using var child=Process.Start(info)!;Fixtures.Check(child.WaitForExit(15000),"Storage crash child timeout");Fixtures.Check(child.ExitCode==77,"Storage crash child exit "+child.ExitCode);
    }
    private static void HardLink(string link,string existing)
    {
        if(!CreateHardLink(link,existing,0))throw new IOException("Synthetic hard link failed",Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string link,string existing,nint reserved);

    private sealed class FixtureDatabase : IDisposable
    {
        private nint handle;
        public FixtureDatabase(string path)
        {if(sqlite3_open_v2(path,out handle,2|4,0)!=0)throw new IOException("Synthetic SQLite open failed");}
        public void Execute(string sql)
        {
            int result=sqlite3_exec(handle,sql,0,0,out var message);
            try{if(result!=0)throw new IOException("Synthetic SQLite error: "+Marshal.PtrToStringUTF8(message));}
            finally{if(message!=0)sqlite3_free(message);}
        }
        public int Scalar(string sql)
        {
            if(sqlite3_prepare_v2(handle,sql,-1,out var statement,0)!=0)throw new IOException("Synthetic SQLite prepare failed");
            try{if(sqlite3_step(statement)!=100)throw new IOException("Synthetic SQLite scalar failed");return sqlite3_column_int(statement,0);}
            finally{sqlite3_finalize(statement);}
        }
        public void Dispose(){if(handle!=0){sqlite3_close_v2(handle);handle=0;}}
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl),DefaultDllImportSearchPaths(DllImportSearchPath.System32)] private static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)] string path,out nint db,int flags,nint vfs);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl),DefaultDllImportSearchPaths(DllImportSearchPath.System32)] private static extern int sqlite3_close_v2(nint db);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl),DefaultDllImportSearchPaths(DllImportSearchPath.System32)] private static extern int sqlite3_exec(nint db,[MarshalAs(UnmanagedType.LPUTF8Str)] string sql,nint callback,nint state,out nint error);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl),DefaultDllImportSearchPaths(DllImportSearchPath.System32)] private static extern void sqlite3_free(nint memory);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl),DefaultDllImportSearchPaths(DllImportSearchPath.System32)] private static extern int sqlite3_prepare_v2(nint db,[MarshalAs(UnmanagedType.LPUTF8Str)] string sql,int length,out nint statement,nint tail);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl),DefaultDllImportSearchPaths(DllImportSearchPath.System32)] private static extern int sqlite3_step(nint statement);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl),DefaultDllImportSearchPaths(DllImportSearchPath.System32)] private static extern int sqlite3_column_int(nint statement,int column);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl),DefaultDllImportSearchPaths(DllImportSearchPath.System32)] private static extern int sqlite3_finalize(nint statement);
    }
}
