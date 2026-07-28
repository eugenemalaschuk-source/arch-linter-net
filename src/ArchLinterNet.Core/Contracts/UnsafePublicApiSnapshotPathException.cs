using ArchLinterNet.Core.Contracts.Families;

namespace ArchLinterNet.Core.Contracts;

internal sealed class UnsafePublicApiSnapshotPathException(
    ArchitecturePublicApiSurfaceContract contract,
    InvalidOperationException innerException) : InvalidOperationException(innerException.Message, innerException)
{
    public ArchitecturePublicApiSurfaceContract Contract { get; } = contract;
}
