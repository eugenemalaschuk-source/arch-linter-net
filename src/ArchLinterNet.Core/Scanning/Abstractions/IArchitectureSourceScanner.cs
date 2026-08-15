using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Scanning;

internal interface IArchitectureSourceScanner
{
    IEnumerable<ArchitectureViolation> FindMethodBodyViolations( // NOSONAR: all parameters are independently meaningful (injected services, optional overrides); a parameter object would hide the DI surface at the call site
        string repositoryRoot,
        string sourceNamespacePrefix,
        IReadOnlyList<string> forbiddenCallPatterns,
        ArchitectureContractExecutionContext executionContext,
        string[]? sourceRoots = null,
        ArchitectureLayer? sourceLayer = null,
        IReadOnlyList<string>? preprocessorSymbols = null,
        IArchitectureFileSystem? fileSystem = null,
        IRoslynCompilationFactory? compilationFactory = null,
        IArchitectureAssemblyLoader? assemblyLoader = null,
        IReadOnlyList<string>? explicitReferenceAssemblyPaths = null,
        string? sourceAssemblyHint = null,
        CancellationToken cancellationToken = default);

    IReadOnlyList<string> FindMatchingSourceFiles(
        string repositoryRoot,
        ArchitectureLayer layer,
        string[]? sourceRoots = null,
        IArchitectureFileSystem? fileSystem = null);
}
