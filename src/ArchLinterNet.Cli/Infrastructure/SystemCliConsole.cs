using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Infrastructure;

internal sealed class SystemCliConsole : ICliConsole
{
    private readonly TextWriter _out;
    private readonly TextWriter _error;

    public SystemCliConsole()
        : this(Console.Out, Console.Error, Console.IsOutputRedirected, Console.IsErrorRedirected)
    {
    }

    internal SystemCliConsole(
        TextWriter output,
        TextWriter error,
        bool outputRedirected,
        bool errorRedirected)
    {
        _out = outputRedirected ? new AnsiStrippingTextWriter(output) : output;
        _error = errorRedirected ? new AnsiStrippingTextWriter(error) : error;
    }

    public TextWriter Out => _out;

    public TextWriter Error => _error;
}
