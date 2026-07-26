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

    // Whether two resolved paths that differ only by case name the same on-disk file. Two paths
    // both merely *existing* does not answer this: a case-sensitive filesystem can legitimately
    // hold "Surface.txt" and "surface.txt" as two distinct files. The only reliable answer comes
    // from asking the filesystem what is actually stored at that location.
    bool IsSameFile(string first, string second);

    PublicApiSnapshotDocument Read(string resolvedPath, string authoredPath);
}
