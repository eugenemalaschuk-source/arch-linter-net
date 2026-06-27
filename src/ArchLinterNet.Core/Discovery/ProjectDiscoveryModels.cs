namespace ArchLinterNet.Core.Discovery;

internal sealed record DiscoveredProjectFile(string AbsolutePath, string AssemblyName, IReadOnlyList<string> TargetFrameworks);

public sealed record ArchitectureProjectDiscoveryDiagnostic(string Kind, string Subject, string Message);

public sealed record ProjectDiscoveryResult(
    IReadOnlyCollection<string> TargetAssemblyNames,
    IReadOnlyCollection<string> AssemblySearchPaths,
    IReadOnlyCollection<string> SourceRoots,
    IReadOnlyCollection<ArchitectureProjectDiscoveryDiagnostic> Diagnostics)
{
    public static readonly ProjectDiscoveryResult Empty = new(
        Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
        Array.Empty<ArchitectureProjectDiscoveryDiagnostic>());
}
