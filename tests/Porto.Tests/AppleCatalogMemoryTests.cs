using Porto.Apple;

internal static class AppleCatalogMemoryTests
{
    internal static void Add(List<(string,Action)> tests)
    {
        tests.Add(("apple memory guard accepts exact physical and commit boundary",()=>
        {
            const long bytes=563425280+4227152;
            ulong required=(ulong)(bytes+AppleCatalogMemory.ReserveBytes);
            AppleCatalogMemory.ValidateAvailable(bytes,required,required);
        }));
        tests.Add(("apple memory guard rejects insufficient physical memory or commit",()=>
        {
            const long bytes=563425280+4227152;
            ulong required=(ulong)(bytes+AppleCatalogMemory.ReserveBytes);
            Fixtures.Throws<IOException>(()=>AppleCatalogMemory.ValidateAvailable(bytes,required-1,ulong.MaxValue));
            Fixtures.Throws<IOException>(()=>AppleCatalogMemory.ValidateAvailable(bytes,ulong.MaxValue,required-1));
            Fixtures.Throws<IOException>(()=>AppleCatalogMemory.ValidateAvailable(-1,ulong.MaxValue,ulong.MaxValue));
            Fixtures.Throws<IOException>(()=>AppleCatalogMemory.ValidateAvailable(long.MaxValue,ulong.MaxValue,ulong.MaxValue));
        }));
        tests.Add(("apple memory guard reads Windows availability without private data",()=>AppleCatalogMemory.EnsureAvailable(0)));
    }
}
