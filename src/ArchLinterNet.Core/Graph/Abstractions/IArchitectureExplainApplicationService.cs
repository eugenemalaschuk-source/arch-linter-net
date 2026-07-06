namespace ArchLinterNet.Core.Graph.Abstractions;

public interface IArchitectureExplainApplicationService
{
    ArchitectureExplainOutcome Explain(ArchitectureExplainRequest request);
}
