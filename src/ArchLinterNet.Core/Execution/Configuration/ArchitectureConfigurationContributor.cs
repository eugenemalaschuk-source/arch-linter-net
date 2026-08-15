using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Execution.Configuration;

internal delegate void ArchitectureConfigurationContributor(
    ArchitectureAnalysisSession session,
    ArchitectureConfigurationReferenceCollector collector,
    IArchitectureContract contract);
