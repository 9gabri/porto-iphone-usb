using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Porto.Apple;

// Only an already captured RAM image is accepted. This class has no path-based open operation.
internal sealed class AppleMemorySqlite : IDisposable
{
    private nint db,buffer;
    private AppleCatalogBuffer? image;
    private bool imageReference;
    private byte writeVersion,readVersion;
    private readonly CancellationToken cancel;
    private readonly long deadline=Environment.TickCount64+30000;
    private readonly Native.Progress progress;
    internal AppleMemorySqlite(AppleCatalogBuffer image,CancellationToken cancel)
    {
        this.cancel=cancel;progress=()=>cancel.IsCancellationRequested||Environment.TickCount64>deadline?1:0;
        AppleSqliteImage.ValidateDatabase(image.Data);
        try
        {
            Check(Native.sqlite3_open_v2(":memory:",out db,6,0));
            Native.sqlite3_progress_handler(db,1000,progress,0);
            Native.sqlite3_limit(db,0,4*1024*1024);
            Query("PRAGMA temp_store=MEMORY",1);Query("PRAGMA trusted_schema=OFF",1);Query("PRAGMA query_only=ON",1);
            AppleCatalogMemory.EnsureAvailable(0);
            this.image=image;image.DangerousAddRef(ref imageReference);buffer=image.DangerousGetHandle();
            writeVersion=Marshal.ReadByte(buffer,18);readVersion=Marshal.ReadByte(buffer,19);
            // WAL has already been validated and applied to this RAM copy; deserialize cannot open WAL.
            Marshal.WriteByte(buffer,18,1);Marshal.WriteByte(buffer,19,1);
            Check(Native.sqlite3_deserialize(db,"main",buffer,image.Length,image.Capacity,4));
            var check=Query("PRAGMA integrity_check",100);
            if(check.Count!=1||check[0].Length!=1||check[0][0]!="ok")throw new IOException("Il catalogo Foto non supera il controllo di integrità.");
        }
        catch(EntryPointNotFoundException){Dispose();throw new IOException("SQLite di Windows non supporta la lettura protetta in memoria del catalogo Foto.");}
        catch{Dispose();throw;}
    }
    internal List<string?[]> Query(string sql,int maxRows)
    {
        ObjectDisposedException.ThrowIf(db==0,this);
        cancel.ThrowIfCancellationRequested();
        if(Environment.TickCount64>deadline)throw new IOException("Controllo del catalogo Foto oltre il limite di tempo.");
        nint statement=0;
        try
        {
            Check(Native.sqlite3_prepare_v2(db,sql,-1,out statement,0));
            var rows=new List<string?[]>();long textBytes=0;int code;
            while((code=Native.sqlite3_step(statement))==100)
            {
                cancel.ThrowIfCancellationRequested();
                if(rows.Count>=maxRows)throw new IOException("Catalogo Foto oltre il limite di elementi verificabili.");
                int count=Native.sqlite3_column_count(statement);
                if(count>32)throw new IOException("Struttura Foto troppo complessa.");
                var row=new string?[count];
                for(int i=0;i<count;i++)
                {
                    if(Native.sqlite3_column_type(statement,i)==5)continue;
                    int length=Native.sqlite3_column_bytes(statement,i);
                    textBytes+=length;
                    if(length>65536||textBytes>64*1024*1024)throw new IOException("Metadati Foto oltre il limite di memoria.");
                    row[i]=Marshal.PtrToStringUTF8(Native.sqlite3_column_text(statement,i),length);
                }
                rows.Add(row);
            }
            if(code!=101)Check(code);
            return rows;
        }
        finally {if(statement!=0)Native.sqlite3_finalize(statement);}
    }
    private void Check(int code)
    {
        cancel.ThrowIfCancellationRequested();
        if(code!=0)throw new IOException(code==9?"Controllo del catalogo Foto interrotto per limite di tempo.":$"Catalogo Foto non leggibile (SQLite {code}).");
    }
    public void Dispose()
    {
        if(db!=0)
        {
            // Never release memory still owned by a live SQLite connection.
            if(Native.sqlite3_close(db)!=0)throw new IOException("Chiusura del catalogo Foto non riuscita.");
            db=0;
        }
        // All statements are finalized in Query. Close SQLite before restoring/releasing its image.
        if(buffer!=0){Marshal.WriteByte(buffer,18,writeVersion);Marshal.WriteByte(buffer,19,readVersion);buffer=0;}
        if(imageReference){imageReference=false;image!.DangerousRelease();}
        image=null;
        GC.KeepAlive(progress);
    }
    private static class Native
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int Progress();
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)]string name,out nint db,int flags,nint vfs);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_close(nint db);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_deserialize(nint db,[MarshalAs(UnmanagedType.LPUTF8Str)]string schema,nint data,long size,long capacity,uint flags);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_limit(nint db,int category,int limit);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern void sqlite3_progress_handler(nint db,int instructions,Progress callback,nint arg);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_prepare_v2(nint db,[MarshalAs(UnmanagedType.LPUTF8Str)]string sql,int size,out nint stmt,nint tail);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_step(nint stmt);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_finalize(nint stmt);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_column_count(nint stmt);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_column_type(nint stmt,int column);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_column_bytes(nint stmt,int column);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern nint sqlite3_column_text(nint stmt,int column);
    }
}

internal static class AppleSqliteImage
{
    // Stay below the signed array/native-copy limits, including padding and WAL frame arithmetic.
    internal const int MaximumBytes=2047*1024*1024;
    internal const string MaximumDescription="2047 MiB (circa 2 GiB)";
    internal static int ValidateDatabase(ReadOnlySpan<byte> data)
    {
        if(data.Length<512||data.Length>MaximumBytes||!data[..16].SequenceEqual("SQLite format 3\0"u8))throw Bad();
        int page=BinaryPrimitives.ReadUInt16BigEndian(data[16..]);if(page==1)page=65536;
        if(page<512||page>65536||(page&(page-1))!=0||data.Length%page!=0||data[18] is not(1 or 2)||data[19] is not(1 or 2))throw Bad();
        return page;
    }
    // Independent implementation of SQLite's published WAL wire format, not a remote SQLite engine.
    internal static byte[] ApplyWal(byte[] database,byte[]? wal,CancellationToken cancel)
    {
        using var buffer=AppleCatalogBuffer.Copy(database);
        using var result=ApplyWal(buffer,wal,cancel);
        return result.Data.ToArray();
    }
    internal static AppleCatalogBuffer ApplyWal(AppleCatalogBuffer database,byte[]? wal,CancellationToken cancel)
    {
        int page=ValidateDatabase(database.Data);
        if(wal==null||wal.Length==0)return database;
        if(wal.Length<32||wal.Length>MaximumBytes)throw Bad();
        uint magic=Big(wal,0);if(magic is not(0x377f0682 or 0x377f0683)||Big(wal,4)!=3007000||Big(wal,8)!=page)throw Bad();
        bool big=magic==0x377f0683;uint s0=0,s1=0;
        Checksum(wal.AsSpan(0,24),big,ref s0,ref s1);
        if(s0!=Big(wal,24)||s1!=Big(wal,28))throw Bad();
        int stride=page+24,lastCommit=0;uint finalPages=0;var frames=new List<(uint Page,int Offset)>();
        for(int offset=32;offset<wal.Length;offset+=stride)
        {
            cancel.ThrowIfCancellationRequested();
            if(wal.Length-offset<stride)throw Bad();
            // After a reset the old tail can have a different salt and is outside this WAL generation.
            if(Big(wal,offset+8)!=Big(wal,16)||Big(wal,offset+12)!=Big(wal,20))break;
            uint number=Big(wal,offset),committed=Big(wal,offset+4);
            if(number==0||number>MaximumBytes/page||committed>MaximumBytes/page)throw Bad();
            Checksum(wal.AsSpan(offset,8),big,ref s0,ref s1);Checksum(wal.AsSpan(offset+24,page),big,ref s0,ref s1);
            if(s0!=Big(wal,offset+16)||s1!=Big(wal,offset+20))throw Bad();
            frames.Add((number,offset+24));
            if(committed!=0){lastCommit=frames.Count;finalPages=committed;}
        }
        if(lastCommit==0)return database;
        int length=checked((int)finalPages*page);
        if(length!=database.Length)AppleCatalogMemory.EnsureAvailable(AppleCatalogMemory.CaptureAllocation(length));
        var result=length==database.Length?database:new AppleCatalogBuffer(length);
        try
        {
            if(!ReferenceEquals(result,database))database.Data[..Math.Min(database.Length,length)].CopyTo(result.Data);
            for(int i=0;i<lastCommit;i++)
            {
                cancel.ThrowIfCancellationRequested();var frame=frames[i];
                if(frame.Page<=finalPages)wal.AsSpan(frame.Offset,page).CopyTo(result.Data.Slice(checked(((int)frame.Page-1)*page),page));
            }
            ValidateDatabase(result.Data);return result;
        }
        catch {if(!ReferenceEquals(result,database))result.Dispose();throw;}
    }
    private static uint Big(byte[] data,int offset)=>BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset));
    private static void Checksum(ReadOnlySpan<byte> data,bool big,ref uint first,ref uint second)
    {
        unchecked {for(int i=0;i<data.Length;i+=8){first+=(big?BinaryPrimitives.ReadUInt32BigEndian(data[i..]):BinaryPrimitives.ReadUInt32LittleEndian(data[i..]))+second;second+=(big?BinaryPrimitives.ReadUInt32BigEndian(data[(i+4)..]):BinaryPrimitives.ReadUInt32LittleEndian(data[(i+4)..]))+first;}}
    }
    private static IOException Bad()=>new($"Database o journal Foto troncato, incoerente o oltre il limite di {MaximumDescription}. Nessuna lettura parziale viene presentata come completa.");
}
