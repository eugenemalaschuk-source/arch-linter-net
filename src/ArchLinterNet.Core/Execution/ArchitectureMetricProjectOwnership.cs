using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Execution;

// Project metrics need the resolved CLR assembly to come from one discovered output artifact,
// not a same-simple-name assembly that happened to be loaded by the host process first.
internal static class ArchitectureMetricProjectOwnership
{
    internal static bool RequiresExactArtifactBinding(ArchitectureContractDocument document) =>
        document.Metrics.Any(metric => string.Equals(metric.Unit, "project", StringComparison.Ordinal))
        || (string.Equals(document.Topology?.SubjectKind, "project", StringComparison.Ordinal)
            && document.Metrics.Any(metric =>
                !string.Equals(metric.Kind, ArchitectureMetricKinds.PublicContractSurfaceCount, StringComparison.Ordinal)));
}
