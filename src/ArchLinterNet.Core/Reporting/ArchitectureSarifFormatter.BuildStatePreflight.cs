using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public partial interface IArchitectureSarifFormatter
{
    /// <summary>
    /// Additive overload — see the classificationPathDeferred-style pattern used elsewhere in this
    /// codebase for why this exists alongside the original overload instead of extending it.
    /// </summary>
    string FormatResultAsSarif(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics,
        string toolVersion);
}

public sealed partial class ArchitectureSarifFormatter
{
    public string FormatResultAsSarif(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics,
        string toolVersion)
    {
        return FormatResultAsSarifCore(
            mode,
            violations,
            cycles.Select(cycle => (Func<string, ResultEntry>)(level => BuildCycleEntry(cycle, level))),
            toolVersion,
            preflightDiagnostics);
    }

    public static string FormatResultAsSarif(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<ArchitectureCycleFinding> cycles,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics,
        string toolVersion)
    {
        return FormatResultAsSarifCore(
            mode,
            violations,
            cycles.Select(cycle => (Func<string, ResultEntry>)(level =>
                BuildCycleEntry(ArchitectureDiagnosticMapper.FromCycle(cycle), level))),
            toolVersion,
            preflightDiagnostics);
    }

    private static ResultEntry BuildPreflightEntry(BuildStatePreflightDiagnostic diagnostic)
    {
        string state = PreflightStateToken(diagnostic.State);
        string ruleId = $"build-state-preflight/{state}";
        BuildStatePreflightEvidence evidence = diagnostic.Evidence;

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
        };

        return new ResultEntry(ruleId, "build-state-preflight", evidence.ProjectPath, "build-state-preflight", json);
    }

    private static string FormatPreflightMessage(string state, BuildStatePreflightEvidence evidence)
    {
        string detail = evidence.Detail != null ? $" {evidence.Detail}" : string.Empty;
        return $"[{state}] {evidence.AssemblyName} ({evidence.ProjectPath}){detail}";
    }

    private static string PreflightStateToken(BuildStatePreflightState state) => state switch
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
