using Porto.Apple;

internal static class Rc21CatalogTests
{
    internal const long ReportedDatabase=1_416_859_648;
    internal const long ReportedWal=7_337_752;
    internal static void Add(List<(string,Action)> tests)
    {
        tests.Add(("rc21 catalog accepts reported 1.42 GB database and WAL metadata",()=>
            AppleCatalogCapture.ValidateMetadata(new(ReportedDatabase,"synthetic",true),new(ReportedWal,"synthetic",true))));
        tests.Add(("rc21 catalog accepts reported 1.42 GB database without WAL",()=>
            AppleCatalogCapture.ValidateMetadata(new(ReportedDatabase,"synthetic",true),null)));
        tests.Add(("rc21 reported size retains memory guard with shared verification image",()=>
        {
            long allocation=AppleCatalogMemory.CaptureAllocation(ReportedDatabase+ReportedWal);
            Fixtures.Check(allocation==1_424_197_432);
            const ulong required=1_961_068_344;
            AppleCatalogMemory.ValidateAvailable(allocation,required,required);
            Fixtures.Throws<IOException>(()=>AppleCatalogMemory.ValidateAvailable(allocation,required-1,ulong.MaxValue));
            Fixtures.Throws<IOException>(()=>AppleCatalogMemory.ValidateAvailable(allocation,ulong.MaxValue,required-1));
        }));
        tests.Add(("rc21 catalog maximum leaves array native padding and frame arithmetic headroom",()=>
        {
            long maximum=AppleSqliteImage.MaximumBytes;
            Fixtures.Check(maximum>ReportedDatabase+ReportedWal&&maximum+32<Array.MaxLength&&maximum+65560<int.MaxValue);
            long allocation=AppleCatalogMemory.CaptureAllocation(maximum);
            AppleCatalogMemory.ValidateAvailable(allocation,(ulong)(allocation+AppleCatalogMemory.ReserveBytes),ulong.MaxValue);
            foreach(long invalid in new[]{-1L,0,maximum+1,long.MaxValue})
                Fixtures.Throws<IOException>(()=>AppleCatalogMemory.CaptureAllocation(invalid));
        }));
    }
}
