namespace ArchLinterNet.Core.Model;

// Precedence order defined by the analysis-build-state-fingerprints capability
// (openspec/specs/analysis-build-state-fingerprints/spec.md): the first matching state, in this
// declaration order, is the one emitted for a project — Cancelled has the highest precedence.
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
    Current
}
