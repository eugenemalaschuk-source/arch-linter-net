using ArchLinterNet.Core.Model;
using static ArchLinterNet.Core.Reporting.ArchitectureSarifFormatter;

namespace ArchLinterNet.Core.Reporting;

internal static class ArchitectureSarifBuildStatePreflightProjector
{
    internal static string FormatResultAsSarif(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics,
        IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries,
        string toolVersion)
    {
        return FormatResultAsSarifCore(
            mode,
            violations,
            cycles.Select(cycle => (Func<string, ArchitectureSarifResultEntry>)(level => BuildCycleEntry(cycle, level))),
            toolVersion,
            preflightDiagnostics,
            coverageSummaries);
    }

    internal static string FormatResultAsSarif(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<ArchitectureCycleFinding> cycles,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics,
        IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries,
        string toolVersion)
    {
        return FormatResultAsSarifCore(
            mode,
            violations,
            cycles.Select(cycle => (Func<string, ArchitectureSarifResultEntry>)(level =>
                BuildCycleEntry(ArchitectureDiagnosticMapper.FromCycle(cycle), level))),
            toolVersion,
            preflightDiagnostics,
            coverageSummaries);
    }

    internal static string FormatResultAsSarif(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics,
        string toolVersion)
    {
        return FormatResultAsSarifCore(
            mode,
            violations,
            cycles.Select(cycle => (Func<string, ArchitectureSarifResultEntry>)(level => BuildCycleEntry(cycle, level))),
            toolVersion,
            preflightDiagnostics);
    }

    internal static string FormatResultAsSarif(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<ArchitectureCycleFinding> cycles,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics,
        string toolVersion)
    {
        return FormatResultAsSarifCore(
            mode,
            violations,
            cycles.Select(cycle => (Func<string, ArchitectureSarifResultEntry>)(level =>
                BuildCycleEntry(ArchitectureDiagnosticMapper.FromCycle(cycle), level))),
            toolVersion,
            preflightDiagnostics);
    }

    internal static ArchitectureSarifResultEntry BuildPreflightEntry(BuildStatePreflightDiagnostic diagnostic, string mode)
    {
        string state = PreflightStateToken(diagnostic.State);
        string ruleId = $"build-state-preflight/{state}";
        BuildStatePreflightEvidence evidence = diagnostic.Evidence;
        ArchitectureFinding finding = ArchitectureFindingMapper.FromDiagnostic(diagnostic, mode);

        var json = new Dictionary<string, object?>
        {
            ["ruleId"] = ruleId,
            // Every preflight state that reaches SARIF is a blocking one (Current never becomes
            // a diagnostic worth reporting here) — always "error", independent of mode, since
            // build-state preflight failing closed is not a strict/audit severity choice.
            ["level"] = "error",
            [MessagePropertyName] = new Dictionary<string, object?>
            {
                ["text"] = FormatPreflightMessage(state, evidence),
            },
            ["locations"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["physicalLocation"] = new Dictionary<string, object?>
                    {
                        ["artifactLocation"] = new Dictionary<string, object?> { ["uri"] = evidence.ProjectPath },
                    },
                },
            },
            ["properties"] = new Dictionary<string, object?>
            {
                ["arch_linter_net"] = ArchitectureDiagnosticFormatter.FormatNormalizedFindingForSarif(finding),
            },
        };

        return new ArchitectureSarifResultEntry(ruleId, "build-state-preflight", evidence.ProjectPath, "build-state-preflight", json);
    }

    private static string FormatPreflightMessage(string state, BuildStatePreflightEvidence evidence)
    {
        string detail = evidence.Detail != null ? $" {evidence.Detail}" : string.Empty;
        return $"[{state}] {evidence.AssemblyName} ({evidence.ProjectPath}){detail}";
    }

    internal static string PreflightStateToken(BuildStatePreflightState state) => state switch
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
        BuildStatePreflightState.RestoreFailed => "restore-failed",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
    };
}
