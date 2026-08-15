using ArchLinterNet.Core.IO.Abstractions;

namespace ArchLinterNet.Core.Discovery;

internal interface IArchitectureSolutionParser
{
    IReadOnlyList<string> ParseProjectPaths(string solutionPath, IArchitectureFileSystem? fileSystem = null);
}
