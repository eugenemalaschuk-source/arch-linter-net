using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;

namespace ArchLinterNet.Core.Execution.Abstractions;

internal delegate void ArchitectureConfigurationContributor(
    ArchitectureAnalysisSession session,
    ArchitectureConfigurationReferenceCollector collector,
    IArchitectureContract contract);
