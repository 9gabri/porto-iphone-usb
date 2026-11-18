namespace Porto.Lan;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args is ["--self-test", var root])
            return ReceiverTests.Run(root).GetAwaiter().GetResult();
        ApplicationConfiguration.Initialize();
        Application.Run(new ReceiveWindow());
        return 0;
    }
}
