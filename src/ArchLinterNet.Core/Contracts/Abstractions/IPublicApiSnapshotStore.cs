using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Contracts.Abstractions;

// Read access to reviewed public API snapshot files, kept behind the same Contracts-level seam as
// baseline loading so the Validation application layer never reaches the file system directly.
// Writing deliberately stays with the host (the CLI), mirroring `baseline generate`: Core produces
// content, the host decides where and whether it lands on disk.
public interface IPublicApiSnapshotStore
{
    // Resolves a repository-local snapshot path against the policy's boundary, throwing when the
    // path is absolute or escapes that boundary.
    string ResolvePath(string policyPath, string snapshotPath);

    bool Exists(string resolvedPath);

    PublicApiSnapshotDocument Read(string resolvedPath, string authoredPath);
}
