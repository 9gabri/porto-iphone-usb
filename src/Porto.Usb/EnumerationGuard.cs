using System.Runtime.CompilerServices;
[assembly:InternalsVisibleTo("Porto.Tests")]
namespace Porto.Usb;

internal sealed class EnumerationStoppedException(string message):Exception(message);
internal sealed class EnumerationGuard(int maximumObjects=1_000_000,int maximumErrors=10_000)
{
    private readonly HashSet<string> seen=["DEVICE"];
    private int attempts,errors;
    public void Visit(string id)
    {
        if(++attempts>maximumObjects)throw new EnumerationStoppedException("Limite di elementi enumerati raggiunto; inventario incompleto.");
        if(string.IsNullOrEmpty(id)||!seen.Add(id))throw new EnumerationStoppedException("Identificatore USB vuoto o ripetuto; inventario interrotto per evitare un ciclo.");
    }
    public void Error()
    {if(++errors>maximumErrors)throw new EnumerationStoppedException("Troppi errori di inventario; sblocca e ricollega il telefono.");}
}
