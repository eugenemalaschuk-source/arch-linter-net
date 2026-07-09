namespace ArchLinterNet.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        return new CliCompositionRoot().CreateHost().Run(args);
    }
}
