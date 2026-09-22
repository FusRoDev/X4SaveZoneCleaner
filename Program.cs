namespace X4SaveZoneCleaner;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 1 && args[0].Equals("--self-test", StringComparison.OrdinalIgnoreCase))
        {
            SelfTest.Run();
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
