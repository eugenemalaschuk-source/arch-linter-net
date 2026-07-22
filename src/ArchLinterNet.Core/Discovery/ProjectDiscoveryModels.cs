namespace ArchLinterNet.Core.Discovery;

public sealed record ArchitectureDiscoveredPackageReference(string PackageId, string? Version);

public sealed record ArchitectureDiscoveredFrameworkReference(string FrameworkName, string? Condition, string? SourcePath = null);

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

    public static readonly ProjectDiscoveryResult Empty = new(
        Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
        Array.Empty<ArchitectureProjectDiscoveryDiagnostic>());
}
