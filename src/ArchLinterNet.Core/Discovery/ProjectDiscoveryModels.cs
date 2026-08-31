namespace ArchLinterNet.Core.Discovery;

public sealed record ArchitectureDiscoveredPackageReference(string PackageId, string? Version);

public sealed record ArchitectureDiscoveredFrameworkReference(
    string FrameworkName,
    string TargetFramework,
    bool Explicit,
    string SourcePath,
    string? Condition = null);

public sealed record ArchitectureDiscoveredProjectProperty(string Name, string Value, string SourcePath);

public sealed record ArchitectureDiscoveredFriendAssembly(string AssemblyName, string SourcePath);

public sealed record ArchitectureDiscoveredProjectReference(string Path, string SourcePath);

internal sealed record DiscoveredProjectFile(
    string AbsolutePath,
    string AssemblyName,
    IReadOnlyList<string> TargetFrameworks,
    IReadOnlyList<ArchitectureDiscoveredPackageReference> PackageReferences,
    IReadOnlyList<ArchitectureDiscoveredFrameworkReference> FrameworkReferences,
    IReadOnlyDictionary<string, ArchitectureDiscoveredProjectProperty> Properties,
    IReadOnlyList<ArchitectureDiscoveredFriendAssembly> FriendAssemblies,
    IReadOnlyList<ArchitectureDiscoveredProjectReference> ProjectReferences);

public sealed record ArchitectureProjectDiscoveryDiagnostic(string Kind, string Subject, string Message);

public sealed record ArchitectureDiscoveredProject(
    string Path,
    string AssemblyName,
    IReadOnlyList<string> TargetFrameworks,
    IReadOnlyList<ArchitectureDiscoveredPackageReference>? PackageReferences = null,
    IReadOnlyList<ArchitectureDiscoveredFrameworkReference>? FrameworkReferences = null,
    IReadOnlyDictionary<string, ArchitectureDiscoveredProjectProperty>? Properties = null,
    IReadOnlyList<ArchitectureDiscoveredFriendAssembly>? FriendAssemblies = null,
    IReadOnlyList<ArchitectureDiscoveredProjectReference>? ProjectReferences = null)
{
    public IReadOnlyList<ArchitectureDiscoveredPackageReference> PackageReferences { get; init; } =
        PackageReferences ?? Array.Empty<ArchitectureDiscoveredPackageReference>();

    public IReadOnlyList<ArchitectureDiscoveredFrameworkReference> FrameworkReferences { get; init; } =
        FrameworkReferences ?? Array.Empty<ArchitectureDiscoveredFrameworkReference>();

    public IReadOnlyDictionary<string, ArchitectureDiscoveredProjectProperty> Properties { get; init; } =
        Properties ?? new Dictionary<string, ArchitectureDiscoveredProjectProperty>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ArchitectureDiscoveredFriendAssembly> FriendAssemblies { get; init; } =
        FriendAssemblies ?? Array.Empty<ArchitectureDiscoveredFriendAssembly>();

    public IReadOnlyList<ArchitectureDiscoveredProjectReference> ProjectReferences { get; init; } =
        ProjectReferences ?? Array.Empty<ArchitectureDiscoveredProjectReference>();
}

public sealed record ProjectDiscoveryResult(
    IReadOnlyCollection<string> TargetAssemblyNames,
    IReadOnlyCollection<string> AssemblySearchPaths,
    IReadOnlyCollection<string> SourceRoots,
    IReadOnlyCollection<ArchitectureProjectDiscoveryDiagnostic> Diagnostics)
{
    public IReadOnlyCollection<ArchitectureDiscoveredProject> DiscoveredProjects { get; init; } =
        Array.Empty<ArchitectureDiscoveredProject>();

    // Exact artifact identities discovered from the selected graph. Post-build snapshot loading
    // uses these paths directly rather than treating the output directories as another generic
    // probing location where an older policy/environment copy could win by precedence.
    public IReadOnlyDictionary<string, string> ResolvedAssemblyPaths { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    // Retains the output artifact that belongs to each discovered project. The legacy
    // simple-name map above is still consumed by ordinary assembly resolution, while metric
    // ownership uses this project-path keyed evidence to avoid collapsing same-name outputs.
    internal IReadOnlyDictionary<string, string> ResolvedAssemblyPathsByNormalizedProjectPath { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public static readonly ProjectDiscoveryResult Empty = new(
        Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
        Array.Empty<ArchitectureProjectDiscoveryDiagnostic>());
}
