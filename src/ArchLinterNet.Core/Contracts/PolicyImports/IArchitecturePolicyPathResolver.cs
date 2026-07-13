namespace ArchLinterNet.Core.Contracts.PolicyImports;

internal interface IArchitecturePolicyPathResolver
{
    ArchitecturePolicyRootPath ResolveRoot(string rootPath);

    ArchitecturePolicyResolvedPath ResolveImport(ArchitecturePolicyRootPath root, string declaringPath, string importPath);
}

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
