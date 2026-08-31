using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;

namespace ArchLinterNet.Core.Execution;

internal static partial class ArchitectureTopologyEvaluator
{
    // Metrics preserve resolved-artifact ownership in their native subject identities. Ordinary
    // validation intentionally retains its historical simple-name project projection so existing
    // topology policy matching remains compatible.
    internal static Result EvaluateForMetrics(ArchitectureAnalysisSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArchitectureTopology? topology = session.Document.Topology;
        if (topology is null)
        {
            return Result.Empty;
        }

        (IReadOnlyList<ObservedSubject> subjects, IReadOnlyList<ObservedDependency> dependencies,
            IReadOnlySet<string> incompleteDependencySourceIdentities) =
            Observe(session, topology.SubjectKind, useMetricProjectOwnership: true);
        return Evaluate(session, topology, subjects, dependencies, incompleteDependencySourceIdentities);
    }

    // Shared with the metric projection so source-type external edges use the exact owner binding
    // used when this evaluator observes project topology subjects. The fallback is deliberately a
    // canonical assembly-derived sentinel, never a simple-name ownership inference.
    internal static string ResolveProjectForMetric(ArchitectureAnalysisSession session, Type type) =>
        ResolveProjectForMetric(session, type.Assembly);

    private static string ResolveProjectForMetric(ArchitectureAnalysisSession session, Assembly assembly) =>
        session.Facts.TryGetProjectByResolvedAssembly(assembly, out ArchitectureDiscoveredProject? project)
            ? ProjectPathNormalizer.Normalize(project.Path)
            : $"unbound-project|{CanonicalAssemblyIdentity(assembly)}";

    private static string ResolveProjectSelectorForMetric(ArchitectureAnalysisSession session, Type type) =>
        ResolveProjectSelectorForMetric(session, type.Assembly);

    private static string ResolveProjectSelectorForMetric(ArchitectureAnalysisSession session, Assembly assembly) =>
        session.Facts.TryGetProjectByResolvedAssembly(assembly, out ArchitectureDiscoveredProject? project)
            ? project.AssemblyName
            : assembly.GetName().Name ?? string.Empty;
}
