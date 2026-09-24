using System.Text;
using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Infrastructure;

internal sealed class SystemCliConsole : ICliConsole
{
    private static readonly UTF8Encoding _canonicalJsonEncoding = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private readonly TextWriter _out;
    private readonly TextWriter _error;
    private readonly Stream? _canonicalJsonOutput;

    public SystemCliConsole()
        : this(
            PrepareConsoleWriter(Console.Out, Console.IsOutputRedirected),
            PrepareConsoleWriter(Console.Error, Console.IsErrorRedirected),
            Console.IsOutputRedirected,
            Console.IsErrorRedirected,
            Console.OpenStandardOutput())
    {
    }

    private static TextWriter PrepareConsoleWriter(TextWriter writer, bool redirected)
    {
        if (redirected)
        {
            // Console.Out/Console.Error are intentionally retained so test hosts and embedders
            // that replace them keep receiving writes. Setting the process encoding fixes the
            // redirected stream without bypassing those replacement writers.
            Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }

        return writer;
    }

    internal SystemCliConsole(
        TextWriter output,
        TextWriter error,
        bool outputRedirected,
        bool errorRedirected,
        Stream? canonicalJsonOutput = null)
    {
        _out = outputRedirected ? new AnsiStrippingTextWriter(output) : output;
        _error = errorRedirected ? new AnsiStrippingTextWriter(error) : error;
        _canonicalJsonOutput = canonicalJsonOutput;
    }

    public TextWriter Out => _out;

    public TextWriter Error => _error;

    public void WriteCanonicalJson(string json)
    {
        if (_canonicalJsonOutput is null)
        {
            _out.Write(json);
            return;
        }

        byte[] bytes = _canonicalJsonEncoding.GetBytes(json);
        _canonicalJsonOutput.Write(bytes);
        _canonicalJsonOutput.Flush();
    }
}
