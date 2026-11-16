using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Profiling;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace ArchLinterNet.Core.IO;

public sealed class RoslynCompilationFactory : IRoslynCompilationFactory
{
    public static readonly RoslynCompilationFactory Real = new();

    public CSharpCompilation Create(
        string assemblyName,
        IReadOnlyList<string> sourceFilePaths,
        IReadOnlyList<string>? preprocessorSymbols,
        IArchitectureFileSystem fileSystem,
        IArchitectureAssemblyLoader assemblyLoader,
        IReadOnlyList<string>? explicitReferenceAssemblyPaths = null)
    {
        CSharpParseOptions? parseOptions = preprocessorSymbols is { Count: > 0 }
            ? CSharpParseOptions.Default.WithPreprocessorSymbols(preprocessorSymbols)
            : null;

        List<SyntaxTree> syntaxTrees = sourceFilePaths
            .Select(filePath => CSharpSyntaxTree.ParseText(
                fileSystem.ReadAllText(filePath),
                options: parseOptions,
                path: filePath))
            .ToList();

        List<MetadataReference> references = explicitReferenceAssemblyPaths is { Count: > 0 }
            ? BuildMetadataReferences(explicitReferenceAssemblyPaths, fileSystem)
            : BuildMetadataReferences(fileSystem, assemblyLoader);

        return CSharpCompilation.Create(
            assemblyName,
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    internal static IReadOnlyList<SyntaxTree> ParseMethodBodySourceFiles(
        IReadOnlyList<ArchitectureMethodBodySourceFile> sourceFiles,
        IReadOnlyList<string>? preprocessorSymbols,
        CancellationToken cancellationToken,
        BenchmarkWorkObserver? benchmarkWorkObserver)
    {
        CSharpParseOptions? parseOptions = preprocessorSymbols is { Count: > 0 }
            ? CSharpParseOptions.Default.WithPreprocessorSymbols(preprocessorSymbols)
            : null;

        List<SyntaxTree> syntaxTrees = new(sourceFiles.Count);
        foreach (ArchitectureMethodBodySourceFile sourceFile in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            benchmarkWorkObserver?.RecordMethodBodySyntaxTreeParse();
            SourceText text = sourceFile.Text;
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(
                text, options: parseOptions, path: sourceFile.Path, cancellationToken: cancellationToken));
        }

        return syntaxTrees;
    }

    internal static CSharpCompilation CreateMethodBodyCompilation(
        string assemblyName,
        IReadOnlyList<SyntaxTree> syntaxTrees,
        IReadOnlyList<string> referenceAssemblyPaths,
        IArchitectureFileSystem fileSystem)
    {
        List<MetadataReference> references = BuildMetadataReferences(referenceAssemblyPaths, fileSystem);
        return CSharpCompilation.Create(
            assemblyName,
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    internal static IReadOnlyList<string> GetDefaultReferenceAssemblyPaths(
        IArchitectureFileSystem fileSystem,
        IArchitectureAssemblyLoader assemblyLoader)
    {
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);

        string? trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
        {
            foreach (string path in trustedPlatformAssemblies.Split(Path.PathSeparator)
                .Where(path => !string.IsNullOrWhiteSpace(path) && fileSystem.FileExists(path)))
            {
                paths.Add(path);
            }
        }

        foreach (System.Reflection.Assembly assembly in assemblyLoader.GetLoadedAssemblies())
        {
            string location;

            try
            {
                location = assembly.Location;
            }
            catch
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(location) && fileSystem.FileExists(location))
            {
                paths.Add(location);
            }
        }

        return paths.ToArray();
    }

    private static List<MetadataReference> BuildMetadataReferences(
        IReadOnlyList<string> referenceAssemblyPaths, IArchitectureFileSystem fileSystem)
    {
        return referenceAssemblyPaths
            .Where(fileSystem.FileExists)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
    }

    private static List<MetadataReference> BuildMetadataReferences(
        IArchitectureFileSystem fileSystem, IArchitectureAssemblyLoader assemblyLoader)
    {
        return BuildMetadataReferences(GetDefaultReferenceAssemblyPaths(fileSystem, assemblyLoader), fileSystem);
    }
}
