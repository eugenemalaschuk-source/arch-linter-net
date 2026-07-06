namespace ArchLinterNet.Core.Graph.Abstractions;

public interface IArchitectureGraphApplicationService
{
    ArchitectureGraphOutcome BuildGraph(ArchitectureGraphRequest request);
}
