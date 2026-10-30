using Porto.Usb;

namespace Porto.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if(args.Length==1&&args[0]=="--usb-worker")return WorkerProtocol.Run();
        if(args.Length==1&&args[0]=="--interop-smoke"){WpdSource.SmokeWithoutDevices();return 0;}
        bool smoke=args.Length==2&&args[0]=="--ui-smoke";
        if(!Environment.Is64BitProcess||System.Runtime.InteropServices.RuntimeInformation.OSArchitecture!=System.Runtime.InteropServices.Architecture.X64)
        {MessageBox.Show("Questa candidata supporta Windows 11 x64. Windows ARM non è stato verificato.","Porto");return 1;}
        ApplicationConfiguration.Initialize();
        using var form=new MainForm(smoke?args[1]:null);
        Application.Run(form);return 0;
    }
}
