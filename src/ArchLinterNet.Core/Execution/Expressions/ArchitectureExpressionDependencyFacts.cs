namespace ArchLinterNet.Core.Execution.Expressions;

// Shaped exactly like the closed "dependency object" in openspec/specs/cel-policy-model/spec.md.
// See ArchitectureExpressionSubjectFacts for the same Core-owned-DTO rationale.
internal sealed record ArchitectureExpressionDependencyFacts(
    string Kind,
    bool ViaMethodBody,
    string SourceMemberName,
    string TargetMemberName);
