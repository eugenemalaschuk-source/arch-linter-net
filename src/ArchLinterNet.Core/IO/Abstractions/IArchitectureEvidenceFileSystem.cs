namespace ArchLinterNet.Core.IO.Abstractions;

/// <summary>
/// Opens evidence artifacts through a verified repository-local regular-file boundary.
/// </summary>
/// <remarks>
/// Implementations are a trusted host capability. They must atomically resolve
/// <paramref name="repositoryRelativePath"/> beneath <paramref name="repositoryRoot"/>, reject
/// redirects and non-regular files, and return the artifact's exact bytes.
/// </remarks>
public interface IArchitectureEvidenceFileSystem
{
    /// <summary>
    /// Opens the exact bytes of one verified regular file beneath a repository root.
    /// </summary>
    Stream OpenRepositoryLocalRegularFile(string repositoryRoot, string repositoryRelativePath);
}
