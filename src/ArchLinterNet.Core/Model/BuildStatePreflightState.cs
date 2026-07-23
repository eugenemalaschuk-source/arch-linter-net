namespace ArchLinterNet.Core.Model;

// Precedence order defined by the analysis-build-state-fingerprints capability
// (openspec/specs/analysis-build-state-fingerprints/spec.md): the first matching state, in this
// declaration order, is the one emitted for a project — Cancelled has the highest precedence.
// This precedence applies to states emitted by BuildStatePreflightEvaluator.Evaluate only.
public enum BuildStatePreflightState
{
    Cancelled,
    RestoreRequired,
    MissingArtifact,
    WrongConfiguration,
    WrongTargetFramework,
    WrongProjectOutput,
    InconsistentDependencyArtifact,
    StaleArtifact,
    UnverifiableArtifact,
    Current,

    // Terminal execution-failure outcome: emitted only by BuildStatePreparationService's
    // `dotnet build` invocation during --ensure-built, never by Evaluate()'s precedence walk. It
    // is distinct from MissingArtifact (an ordinary-mode observation of absent output) because a
    // failed build attempt is a different, actionable event with its own evidence (build
    // stdout/stderr), not a report about pre-existing state.
    BuildFailed
}
