using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Infrastructure;

internal sealed class SystemCliConsole : ICliConsole
{
    public TextWriter Out => Console.Out;

    public TextWriter Error => Console.Error;
}
