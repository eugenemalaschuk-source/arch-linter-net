namespace ArchLinterNet.Core.Discovery;

public sealed record ArchitectureDiscoveredPackageReference(string PackageId, string? Version);

internal sealed record DiscoveredProjectFile(
    string AbsolutePath,
    string AssemblyName,
    IReadOnlyList<string> TargetFrameworks,
    IReadOnlyList<ArchitectureDiscoveredPackageReference> PackageReferences);

public sealed record ArchitectureProjectDiscoveryDiagnostic(string Kind, string Subject, string Message);

public sealed record ArchitectureDiscoveredProject(
    string Path,
    string AssemblyName,
    IReadOnlyList<string> TargetFrameworks,
    IReadOnlyList<ArchitectureDiscoveredPackageReference>? PackageReferences = null)
{
    public IReadOnlyList<ArchitectureDiscoveredPackageReference> PackageReferences { get; init; } =
        PackageReferences ?? Array.Empty<ArchitectureDiscoveredPackageReference>();
}

public sealed record ProjectDiscoveryResult(
    IReadOnlyCollection<string> TargetAssemblyNames,
    IReadOnlyCollection<string> AssemblySearchPaths,
    IReadOnlyCollection<string> SourceRoots,
    IReadOnlyCollection<ArchitectureProjectDiscoveryDiagnostic> Diagnostics)
{
    public IReadOnlyCollection<ArchitectureDiscoveredProject> DiscoveredProjects { get; init; } =
        Array.Empty<ArchitectureDiscoveredProject>();

    public static readonly ProjectDiscoveryResult Empty = new(
        Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
        Array.Empty<ArchitectureProjectDiscoveryDiagnostic>());
}
