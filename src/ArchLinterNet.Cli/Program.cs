using ArchLinterNet.Cli.Infrastructure;

namespace ArchLinterNet.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        using CliComposition composition = CliCompositionRoot.Compose();
        return composition.Host.Run(args);
    }
}
