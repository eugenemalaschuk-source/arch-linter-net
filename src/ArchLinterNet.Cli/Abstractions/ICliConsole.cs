using System.IO;

namespace ArchLinterNet.Cli.Abstractions;

internal interface ICliConsole
{
    TextWriter Out { get; }

    TextWriter Error { get; }

    // Most CLI output remains text. Canonical history JSON is distinct because its artifact
    // identity includes exact UTF-8 bytes at the process stdout boundary.
    void WriteCanonicalJson(string json) => Out.Write(json);
}
