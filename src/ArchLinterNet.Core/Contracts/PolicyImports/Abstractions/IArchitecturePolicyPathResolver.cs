using ArchLinterNet.Core.Contracts.PolicyImports.Models;

namespace ArchLinterNet.Core.Contracts.PolicyImports;

internal interface IArchitecturePolicyPathResolver
{
    ArchitecturePolicyRootPath ResolveRoot(string rootPath);

    ArchitecturePolicyResolvedPath ResolveImport(ArchitecturePolicyRootPath root, string declaringPath, string importPath);
}
