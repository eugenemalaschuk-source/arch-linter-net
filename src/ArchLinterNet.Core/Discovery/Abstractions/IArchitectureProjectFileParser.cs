using ArchLinterNet.Core.IO.Abstractions;

namespace ArchLinterNet.Core.Discovery;

internal interface IArchitectureProjectFileParser
{
    DiscoveredProjectFile Parse(string projectPath, IArchitectureFileSystem? fileSystem = null);
}
