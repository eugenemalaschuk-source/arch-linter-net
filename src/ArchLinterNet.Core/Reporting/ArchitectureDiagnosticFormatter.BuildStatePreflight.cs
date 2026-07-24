using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public partial interface IArchitectureDiagnosticFormatter
{
    /// <summary>
    /// Additive overload — see the classificationPathDeferred overload in
    /// <see cref="ArchitectureDiagnosticFormatter"/> for why this exists alongside it instead of
    /// extending it. <c>preflightDiagnostics</c> is required (no default) so this overload stays
    /// unambiguous by arity against every prior one.
    /// </summary>
    string FormatResultForCiArtifacts( // NOSONAR: each parameter represents a semantically distinct section of the CI artifact payload; grouping would obscure the data contract
        string mode,
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles,
        ArchitectureClassificationPathDeferredNotice? classificationPathDeferred,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics,
        IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null,
        IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null,
        IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null,
        IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null,
        IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts = null,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures = null)
        => FormatResultForCiArtifacts(
            mode, passed, violations, cycles, classificationRoles, classificationPathDeferred, coverageFindings,
            unmatched, policyConsistencyFindings, coverageSummaries, classificationConflicts, classificationMetadataFailures);

    /// <summary>
    /// Default implementation returns an empty string so a pre-existing third-party
    /// <see cref="IArchitectureDiagnosticFormatter"/> implementer that predates build-state
    /// preflight still compiles without adding this member. Only
    /// <see cref="ArchitectureDiagnosticFormatter"/> itself overrides it with real rendering.
    /// </summary>
    string FormatBuildStatePreflightForHumans(IReadOnlyCollection<BuildStatePreflightDiagnostic> diagnostics) => string.Empty;
}

public sealed partial class ArchitectureDiagnosticFormatter
{
    public string FormatBuildStatePreflightForHumans(IReadOnlyCollection<BuildStatePreflightDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            return string.Empty;
        }

        var lines = diagnostics
            .OrderBy(d => d.Evidence.ProjectPath, StringComparer.Ordinal)
            .Select(FormatBuildStatePreflightLine);

        return "Build-state preflight:" + Environment.NewLine + string.Join(Environment.NewLine, lines);
    }

    private static string FormatBuildStatePreflightLine(BuildStatePreflightDiagnostic diagnostic)
    {
        string state = StateToken(diagnostic.State);
        BuildStatePreflightEvidence evidence = diagnostic.Evidence;

        List<string> parts = new()
        {
            $"  [{state}] {evidence.AssemblyName} ({evidence.ProjectPath})"
        };

        if (evidence.RequestedConfiguration != null || evidence.ObservedConfiguration != null)
        {
            parts.Add($"requested configuration: {evidence.RequestedConfiguration ?? "<unspecified>"}, " +
                $"observed: {evidence.ObservedConfiguration ?? "<unknown>"}");
        }

        if (evidence.RequestedTargetFramework != null || evidence.ObservedTargetFramework != null)
        {
            parts.Add($"requested target framework: {evidence.RequestedTargetFramework ?? "<unspecified>"}, " +
                $"observed: {evidence.ObservedTargetFramework ?? "<unknown>"}");
        }

        if (evidence.BuildCommand != null)
        {
            parts.Add($"build command: {evidence.BuildCommand}");
        }

        if (evidence.Detail != null)
        {
            parts.Add(evidence.Detail);
        }

        return string.Join(Environment.NewLine + "    ", parts);
    }

    private static object[] BuildStatePreflightJson(IReadOnlyCollection<BuildStatePreflightDiagnostic>? diagnostics)
    {
        return (diagnostics ?? Array.Empty<BuildStatePreflightDiagnostic>())
            .OrderBy(d => d.Evidence.ProjectPath, StringComparer.Ordinal)
            .Select(d => (object)new
            {
                state = StateToken(d.State),
                project_path = d.Evidence.ProjectPath,
                assembly_name = d.Evidence.AssemblyName,
                requested_configuration = d.Evidence.RequestedConfiguration,
                observed_configuration = d.Evidence.ObservedConfiguration,
                requested_target_framework = d.Evidence.RequestedTargetFramework,
                observed_target_framework = d.Evidence.ObservedTargetFramework,
                expected_output_path = d.Evidence.ExpectedOutputPath,
                build_command = d.Evidence.BuildCommand,
                detail = d.Evidence.Detail
            })
            .ToArray();
    }

    private static string StateToken(BuildStatePreflightState state) => state switch
    {
        BuildStatePreflightState.Cancelled => "cancelled",
        BuildStatePreflightState.RestoreRequired => "restore-required",
        BuildStatePreflightState.MissingArtifact => "missing-artifact",
        BuildStatePreflightState.WrongConfiguration => "wrong-configuration",
        BuildStatePreflightState.WrongTargetFramework => "wrong-target-framework",
        BuildStatePreflightState.WrongProjectOutput => "wrong-project-output",
        BuildStatePreflightState.InconsistentDependencyArtifact => "inconsistent-dependency-artifact",
        BuildStatePreflightState.StaleArtifact => "stale-artifact",
        BuildStatePreflightState.UnverifiableArtifact => "unverifiable-artifact",
        BuildStatePreflightState.Current => "current",
        BuildStatePreflightState.BuildFailed => "build-failed",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
    };
}
