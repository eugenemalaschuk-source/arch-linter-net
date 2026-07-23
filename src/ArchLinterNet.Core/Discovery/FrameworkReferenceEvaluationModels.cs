namespace ArchLinterNet.Core.Discovery;

internal sealed record ArchitectureFrameworkReferenceEvaluationFailure(
    string ProjectPath,
    string? TargetFramework,
    string Reason);

internal sealed record ArchitectureFrameworkReferenceEvaluationResult(
    IReadOnlyList<ArchitectureDiscoveredFrameworkReference> References,
    IReadOnlyList<ArchitectureFrameworkReferenceEvaluationFailure> Failures)
{
    public bool Succeeded => Failures.Count == 0;
}
