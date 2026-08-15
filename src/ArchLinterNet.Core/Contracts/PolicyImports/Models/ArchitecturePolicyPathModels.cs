namespace ArchLinterNet.Core.Contracts.PolicyImports.Models;

internal sealed record ArchitecturePolicyRootPath(
    string AuthoredPath,
    string FullPath,
    string PhysicalPath,
    string BoundaryPath,
    string PhysicalBoundaryPath,
    string FileIdentity);

internal sealed record ArchitecturePolicyResolvedPath(
    string FullPath,
    string PhysicalPath,
    string PortableIdentity,
    string FileIdentity);
