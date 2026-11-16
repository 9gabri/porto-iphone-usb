using System.Security.Cryptography;
using Porto.Core;

if(args.Length>0&&args[0]=="--crash")return IntegrationTests.CrashChild(args[1],args[2]);
if(args.Length>0&&args[0]=="--hang"){Thread.Sleep(30000);return 0;}
if(args.Length>0&&args[0]=="--partial")
{using var output=new BinaryWriter(Console.OpenStandardOutput());output.Write((byte)0);output.Write(100);output.Write((byte)1);output.Flush();Thread.Sleep(30000);return 0;}
if(args.Length>0&&args[0].StartsWith("--protocol-",StringComparison.Ordinal))return ProtocolTests.Child(args[0]);
if(args.Length>0&&args[0].StartsWith("--adversarial-usb-",StringComparison.Ordinal))return AdversarialUsbTests.Child(args[0]);
if(args.Length>0&&args[0].StartsWith("--final-usb-",StringComparison.Ordinal))return FinalUsbTests.Child(args[0]);
if(args.Length>0&&args[0]=="--storage-crash")return AdversarialStorageTests.Child(args[1],args[2]);

if(args.Length>0&&args[0].StartsWith("--rc5-protocol-",StringComparison.Ordinal))return Rc5ProtocolTests.Child(args[0]);
if(args.Length>0&&args[0]=="--rc6-lifetime")return Rc6StreamLifetimeTests.Child();
if(args.Length>0&&args[0]=="--rc6-cleanup-hang")return Rc6StreamLifetimeTests.Child(true);
if(args.Length>0&&args[0]=="--rc6-cleanup-failure")return Rc6StreamLifetimeTests.Child(failCleanup:true);
if(args.Length>0&&args[0].StartsWith("--rc7-recovery-",StringComparison.Ordinal))return Rc7RecoveryTests.Child(args[0]);
if(args.Length>0&&args[0].StartsWith("--rc8-recovery-",StringComparison.Ordinal))return Rc8RecoveryTests.Child(args[0]);
if(args.Length>0&&args[0]=="--rc8-promotion")return Rc8CoverageTests.CrashChild(args[1],args[2],bool.Parse(args[3]));
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
AdversarialLedgerTests.Add(tests);
FinalLedgerTests.Add(tests);
AdversarialUsbTests.Add(tests);
FinalUsbTests.Add(tests);
AdversarialStorageTests.Add(tests);
FinalStorageTests.Add(tests);
FinalPreferenceTests.Add(tests);
EncodedMediaTests.Add(tests);
ProtocolTests.Add(tests);
NativeUsbTests.Add(tests);
Rc5ResourceTests.Add(tests);
Rc5ProtocolTests.Add(tests);
Rc5NativeResourceTests.Add(tests);
Rc5InventoryTests.Add(tests);
Rc6StreamLifetimeTests.Add(tests);
Rc6ClassificationTests.Add(tests);
Rc6NativeCleanupTests.Add(tests);
Rc6RecoveryTests.Add(tests);
Rc7RecoveryTests.Add(tests);
Rc8MediaTests.Add(tests);
Rc8RecoveryTests.Add(tests);
Rc8CoverageTests.Add(tests);
FolderSourceTests.Add(tests);
FolderImportTests.Add(tests);
if(args.Contains("--large"))LargeTests.Add(tests);
int failures = 0;
string? filter=args.FirstOrDefault(a=>a.StartsWith("--filter=",StringComparison.Ordinal))?[9..];
if(filter!=null)tests=tests.Where(t=>t.Item1.Contains(filter,StringComparison.OrdinalIgnoreCase)).ToList();
if(tests.Count==0){Console.WriteLine("No matching tests");return 2;}
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
