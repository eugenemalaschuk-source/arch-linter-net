namespace ArchLinterNet.Cli;

internal sealed class SystemCliConsole : ICliConsole
{
    public TextWriter Out => Console.Out;

    public TextWriter Error => Console.Error;
}
