using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Contracts;

public sealed class PublicApiSnapshotStore(IArchitectureFileSystem fileSystem) : IPublicApiSnapshotStore
{
    public string ResolvePath(string policyPath, string snapshotPath)
    {
        string boundary = PublicApiSnapshotResolver.ResolveBoundary(policyPath);
        return PublicApiSnapshotResolver.ResolveSnapshotPath(boundary, snapshotPath, "The requested snapshot");
    }

    public bool Exists(string resolvedPath)
    {
        return fileSystem.FileExists(resolvedPath);
    }

    public PublicApiSnapshotDocument Read(string resolvedPath, string authoredPath)
    {
        return PublicApiSnapshotFormat.Parse(fileSystem.ReadAllText(resolvedPath), authoredPath);
    }
}
