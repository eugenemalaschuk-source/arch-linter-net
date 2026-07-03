using Microsoft.CodeAnalysis.CSharp;

namespace ArchLinterNet.Core.IO;

public interface IRoslynCompilationFactory
{
    CSharpCompilation Create(
        string assemblyName,
        IReadOnlyList<string> sourceFilePaths,
        IReadOnlyList<string>? preprocessorSymbols,
        IArchitectureFileSystem fileSystem,
        IArchitectureAssemblyLoader assemblyLoader);
}
