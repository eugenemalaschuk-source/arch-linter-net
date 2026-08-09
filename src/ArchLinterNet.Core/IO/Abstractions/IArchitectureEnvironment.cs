namespace ArchLinterNet.Core.IO.Abstractions;

public interface IArchitectureEnvironment
{
    string? GetEnvironmentVariable(string name);

    string BaseDirectory { get; }

    // The currently running CoreCLR's own shared-framework directory (e.g.
    // ".../shared/Microsoft.NETCore.App/10.0.0"), used as a fallback root when locating an
    // installed shared framework (see ArchitectureSharedFrameworkResolver).
    string RuntimeDirectory { get; }
}
