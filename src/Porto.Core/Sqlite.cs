using System.Runtime.InteropServices;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace Porto.Core;

internal enum SqliteDatabaseKind { Catalog, Work }

// SQLite supplied and serviced by Windows. All filenames and values use bound parameters.
internal sealed class Sqlite : IDisposable
{
    private nint db;
    public Sqlite(string path,SqliteDatabaseKind kind)
    {
        bool existing=SqliteOwnership.Validate(path,kind);
        try
        {
            if(!existing)
            {
                // Publish the ownership header before exposing the database name.
                // A crash during first initialization cannot leave an unmarked file.
                string temporary=Path.Combine(Path.GetDirectoryName(path)!,".porto-database-"+Guid.NewGuid().ToString("N")+".tmp");
                Destination.CheckAncestors(temporary);
                Open(temporary,2|4);Configure(kind);Dispose();
                Destination.Publish(temporary,path);
            }
            Open(path,2);Configure(kind);
        }
        catch {Dispose();throw;}
    }
    // Only ownership validation uses this constructor; it never changes settings.
    internal Sqlite(string path,bool readOnly)=>Open(path,readOnly?1:2);
    private void Open(string path,int flags)
    {
        int rc=Native.sqlite3_open_v2(path,out db,flags,0);
        if(rc!=0){Dispose();throw new IOException($"Catalogo non apribile (SQLite {rc}).");}
    }
    private void Configure(SqliteDatabaseKind kind)
    {
        Execute("PRAGMA journal_mode=DELETE");
        Execute("PRAGMA synchronous=FULL");
        Execute("PRAGMA busy_timeout=3000");
        Execute($"PRAGMA application_id={SqliteOwnership.ApplicationId(kind)}");
        Execute("PRAGMA user_version=1");
    }
    public void Execute(string sql, params object?[] values) => Query(sql, values);
    public List<string?[]> Query(string sql, params object?[] values)
    {
        Check(Native.sqlite3_prepare_v2(db, sql, -1, out var stmt, 0));
        try
        {
            for (int i=0; i<values.Length; i++)
            {
                int rc = values[i] switch
                {
                    null => Native.sqlite3_bind_null(stmt, i+1),
                    long n => Native.sqlite3_bind_int64(stmt, i+1, n),
                    int n => Native.sqlite3_bind_int64(stmt, i+1, n),
                    string s => Native.sqlite3_bind_text(stmt, i+1, s, -1, new nint(-1)),
                    _ => throw new ArgumentException("Unsupported SQLite value")
                };
                Check(rc);
            }
            List<string?[]> rows = [];
            int result;
            while ((result = Native.sqlite3_step(stmt)) == 100)
            {
                var row = new string?[Native.sqlite3_column_count(stmt)];
                for (int i=0;i<row.Length;i++) row[i] = Marshal.PtrToStringUTF8(Native.sqlite3_column_text(stmt,i));
                rows.Add(row);
            }
            if (result != 101) Check(result);
            return rows;
        }
        finally { Native.sqlite3_finalize(stmt); }
    }
    public void Transaction(Action action)
    {
        Execute("BEGIN IMMEDIATE");
        try { action(); Execute("COMMIT"); }
        catch { try { Execute("ROLLBACK"); } catch (IOException) { } throw; }
    }
    private void Check(int code)
    {
        if (code != 0) throw new IOException($"Errore catalogo SQLite {code}: {Marshal.PtrToStringUTF8(Native.sqlite3_errmsg(db))}");
    }
    public void Dispose() { if (db != 0) { Native.sqlite3_close_v2(db); db = 0; } }

    private static class Native
    {
        private const string Dll = "winsqlite3.dll";
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)] string path,out nint db,int flags,nint vfs);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_close_v2(nint db);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern nint sqlite3_errmsg(nint db);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_prepare_v2(nint db,[MarshalAs(UnmanagedType.LPUTF8Str)] string sql,int length,out nint stmt,nint tail);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_step(nint stmt);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_finalize(nint stmt);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_bind_null(nint stmt,int index);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_bind_int64(nint stmt,int index,long value);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_bind_text(nint stmt,int index,[MarshalAs(UnmanagedType.LPUTF8Str)] string value,int length,nint destructor);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_column_count(nint stmt);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern nint sqlite3_column_text(nint stmt,int column);
    }
}
