using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Scanning;

internal sealed record ArchitectureMethodBodyScanRequest
{
    public required string RepositoryRoot { get; init; }

    public required string SourceNamespacePrefix { get; init; }

    public required IReadOnlyList<string> ForbiddenCallPatterns { get; init; }

    public required ArchitectureContractExecutionContext ExecutionContext { get; init; }

    public string[]? SourceRoots { get; init; }

    public ArchitectureLayer? SourceLayer { get; init; }

    public IReadOnlyList<string>? PreprocessorSymbols { get; init; }

    public IArchitectureFileSystem? FileSystem { get; init; }

    public IRoslynCompilationFactory? CompilationFactory { get; init; }

    public IArchitectureAssemblyLoader? AssemblyLoader { get; init; }

    public IReadOnlyList<string>? ExplicitReferenceAssemblyPaths { get; init; }

    public string? SourceAssemblyHint { get; init; }

    public CancellationToken CancellationToken { get; init; }

    public BenchmarkWorkObserver? BenchmarkWorkObserver { get; init; }
}
