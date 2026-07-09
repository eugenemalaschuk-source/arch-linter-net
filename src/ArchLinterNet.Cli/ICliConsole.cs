using System.IO;

namespace ArchLinterNet.Cli;

internal interface ICliConsole
{
    TextWriter Out { get; }

    TextWriter Error { get; }
}
