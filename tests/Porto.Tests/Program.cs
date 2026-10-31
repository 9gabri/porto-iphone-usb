using System.Security.Cryptography;
using Porto.Core;

if(args.Length>0&&args[0]=="--crash")return IntegrationTests.CrashChild(args[1],args[2]);
if(args.Length>0&&args[0]=="--hang"){Thread.Sleep(30000);return 0;}
if(args.Length>0&&args[0]=="--partial")
{using var output=new BinaryWriter(Console.OpenStandardOutput());output.Write((byte)0);output.Write(100);output.Write((byte)1);output.Flush();Thread.Sleep(30000);return 0;}

var tests = new List<(string, Action)>
{
    ("copy and independently reread SHA256", () => {
        byte[] data = new byte[3 * 1024 * 1024 + 19]; new Random(17).NextBytes(data);
        using var input = new MemoryStream(data); using var output = new MemoryStream();
        var result = VerifiedCopy.Copy(input, output, data.LongLength, null, null, default);
        Check(result.Hash == Convert.ToHexString(SHA256.HashData(data)));
        output.Position = 0; VerifiedCopy.Verify(result, VerifiedCopy.Hash(output, default));
    }),
    ("truncation rejected", () => Throws<InvalidDataException>(() => VerifiedCopy.Copy(new MemoryStream([1,2]), Stream.Null, 3, null, null, default))),
    ("unexpected extra bytes rejected", () => Throws<InvalidDataException>(() => VerifiedCopy.Copy(new MemoryStream([1,2]), Stream.Null, 1, null, null, default))),
    ("corruption rejected", () => Throws<InvalidDataException>(() => VerifiedCopy.Verify(new(10,"A"),new(10,"B")))),
    ("pause interrupts before write", () => Throws<OperationCanceledException>(() => VerifiedCopy.Copy(new MemoryStream([1]), Stream.Null, null, null, null, new CancellationToken(true))))
};
IntegrationTests.Add(tests);
ReliabilityTests.Add(tests);
if(args.Contains("--large"))LargeTests.Add(tests);
int failures = 0;
foreach (var (name, test) in tests)
{
    try { test(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures++; Console.WriteLine($"FAIL {name}: {ex}"); }
}
Console.WriteLine($"{tests.Count - failures}/{tests.Count} passed");
return failures == 0 ? 0 : 1;

static void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
static void Throws<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; } throw new Exception($"Expected {typeof(T).Name}");
}
