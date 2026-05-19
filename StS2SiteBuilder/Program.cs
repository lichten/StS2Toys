using System.Runtime.InteropServices;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0)
        {
            NativeMethods.AttachConsole(-1);
            var distDir = SiteBuilderCore.GetDistDir();
            SiteBuilderCore.Build(distDir, Console.WriteLine);
        }
        else
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}

static class NativeMethods
{
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AttachConsole(int dwProcessId);
}
