using System.IO;

namespace ArchLinterNet.Cli.Abstractions;

internal interface ICliConsole
{
    TextWriter Out { get; }

    TextWriter Error { get; }
}
