using Porto.Apple;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

internal static class Rc22MemoryTests
{
    internal static void Add(List<(string,Action)> tests)
    {
        tests.Add(("rc22 reported free RAM supports catalog without a second full copy",()=>
        {
            // Screenshot required 3371982864 = 2*(DB+WAL)+32+512 MiB.
            const long bytes=1_417_555_960;
            long allocation=AppleCatalogMemory.CaptureAllocation(bytes);
            AppleCatalogMemory.ValidateAvailable(allocation,2_641_379_328,3_821_715_456);
            Fixtures.Check(allocation==bytes+32);
            ulong required=(ulong)(allocation+AppleCatalogMemory.ReserveBytes);
            AppleCatalogMemory.ValidateAvailable(allocation,required,required);
            Fixtures.Throws<IOException>(()=>AppleCatalogMemory.ValidateAvailable(allocation,required-1,ulong.MaxValue));
            Fixtures.Throws<IOException>(()=>AppleCatalogMemory.ValidateAvailable(allocation,ulong.MaxValue,required-1));
        }));
        tests.Add(("rc22 shared SQLite image remains readonly and unchanged after disposal",()=>
        {
            byte[] source=AppleGalleryTests.Database();source[18]=2;source[19]=2;
            byte[] hash=SHA256.HashData(source);
            using var buffer=AppleCatalogBuffer.Copy(source);
            for(int i=0;i<AppleCatalogBuffer.Padding;i++)Fixtures.Check(Marshal.ReadByte(buffer.DangerousGetHandle(),buffer.Length+i)==0);
            using(var db=new AppleMemorySqlite(buffer,default))
            {
                Fixtures.Check(db.Query("SELECT COUNT(*) FROM ZASSET",1).Count==1);
                db.Query("PRAGMA query_only=OFF",1);
                Fixtures.Throws<IOException>(()=>db.Query("DELETE FROM ZASSET",1));
                GC.Collect();GC.WaitForPendingFinalizers();
                Fixtures.Check(db.Query("PRAGMA integrity_check",1)[0][0]=="ok");
            }
            Fixtures.Check(SHA256.HashData(buffer.Data).AsSpan().SequenceEqual(hash));
            Fixtures.Check(SHA256.HashData(source).AsSpan().SequenceEqual(hash));
        }));
        tests.Add(("rc22 SQLite keeps borrowed RAM alive until connection closes",()=>
        {
            var buffer=AppleCatalogBuffer.Copy(AppleGalleryTests.Database());
            using var db=new AppleMemorySqlite(buffer,default);
            buffer.Dispose();GC.Collect();GC.WaitForPendingFinalizers();
            Fixtures.Check(db.Query("SELECT COUNT(*) FROM ZASSET",1).Count==1);
            db.Dispose();db.Dispose();
            Fixtures.Check(buffer.IsClosed);
            Fixtures.Throws<ObjectDisposedException>(()=>db.Query("SELECT 1",1));
        }));
        tests.Add(("rc22 cancelled SQLite construction leaves owned capture valid",()=>
        {
            using var buffer=AppleCatalogBuffer.Copy(AppleGalleryTests.Database());
            byte[] hash=SHA256.HashData(buffer.Data);
            Fixtures.Throws<OperationCanceledException>(()=>{using var db=new AppleMemorySqlite(buffer,new CancellationToken(true));});
            Fixtures.Check(SHA256.HashData(buffer.Data).AsSpan().SequenceEqual(hash));
            using var retry=new AppleMemorySqlite(buffer,default);
            Fixtures.Check(retry.Query("PRAGMA integrity_check",1)[0][0]=="ok");
        }));
        tests.Add(("rc22 native capture disposal is idempotent and rejects subsequent access",()=>
        {
            var buffer=AppleCatalogBuffer.Copy(AppleGalleryTests.Database());
            buffer.Dispose();buffer.Dispose();
            Fixtures.Check(buffer.IsClosed);
            Fixtures.Throws<ObjectDisposedException>(()=>{_=buffer.Data.Length;});
        }));
        tests.Add(("rc22 malformed SQLite failure releases borrow and restores WAL header",()=>
        {
            byte[] data=AppleGalleryTests.Database();data[18]=2;data[19]=2;data[100]=0;
            using var buffer=AppleCatalogBuffer.Copy(data);byte[] hash=SHA256.HashData(buffer.Data);
            Fixtures.Throws<IOException>(()=>{using var db=new AppleMemorySqlite(buffer,default);});
            Fixtures.Check(SHA256.HashData(buffer.Data).AsSpan().SequenceEqual(hash));
            buffer.Dispose();Fixtures.Check(buffer.IsClosed);
        }));
    }
}
