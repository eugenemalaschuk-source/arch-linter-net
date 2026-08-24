using ArchLinterNet.Core.PolicyContext;
using static ArchLinterNet.Core.PolicyWeakening.ArchitecturePolicyWeakeningComparisonSupport;

namespace ArchLinterNet.Core.PolicyWeakening;

internal static class ArchitecturePolicyWeakeningAnalysisScopeEvaluator
{
    private static readonly StringComparer _comparer = StringComparer.Ordinal;

    internal static void Evaluate(
        ArchitecturePolicyContextExport baseline,
        ArchitecturePolicyContextExport current,
        ICollection<ArchitecturePolicyWeakeningFinding> findings)
    {
        CompareBoundedAnalysisChange("target_assemblies", baseline.Analysis.TargetAssemblies, current.Analysis.TargetAssemblies);
        CompareBoundedAnalysisChange("projects", baseline.Analysis.Projects, current.Analysis.Projects);
        CompareBoundedAnalysisChange("source_roots", baseline.Analysis.SourceRoots, current.Analysis.SourceRoots);
        CompareProjectGlobChange("project_include", baseline.Analysis.ProjectInclude, current.Analysis.ProjectInclude);
        CompareProjectGlobChange("project_exclude", baseline.Analysis.ProjectExclude, current.Analysis.ProjectExclude);

        void CompareBoundedAnalysisChange(string name, IReadOnlyList<string> baseValues, IReadOnlyList<string> currentValues)
        {
            if (baseValues.OrderBy(value => value, _comparer).SequenceEqual(currentValues.OrderBy(value => value, _comparer), _comparer))
            {
                return;
            }

            findings.Add(CreateFinding(
                "analysis_" + name + "_impact_not_proven",
                "analysis:" + name,
                "impact_not_proven",
                current.Guardrails.PolicyWeakening,
                baseValues,
                currentValues,
                null,
                null,
                Array.Empty<string>(),
                "Analysis inputs may be expanded by project discovery or scanner defaults; context artifacts do not prove their effective analysed membership."));
        }

        void CompareProjectGlobChange(string name, IReadOnlyList<string> baseValues, IReadOnlyList<string> currentValues)
        {
            if (baseValues.OrderBy(value => value, _comparer).SequenceEqual(currentValues.OrderBy(value => value, _comparer), _comparer))
            {
                return;
            }

            findings.Add(CreateFinding(
                "analysis_" + name + "_impact_not_proven",
                "analysis:" + name,
                "impact_not_proven",
                current.Guardrails.PolicyWeakening,
                baseValues,
                currentValues,
                null,
                null,
                Array.Empty<string>(),
                "Project include/exclude values are globs; context artifacts do not prove their matched project sets."));
        }
    }
}
