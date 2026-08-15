using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Graph;

public interface IArchitectureGraphFormatter
{
    string FormatAsJson(ArchitectureDependencyGraph graph);

    string FormatAsDot(ArchitectureDependencyGraph graph);

    string FormatAsMermaid(ArchitectureDependencyGraph graph);
}
