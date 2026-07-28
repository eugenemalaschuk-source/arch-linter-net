namespace ArchLinterNet.Core.Validation;

/// <summary>
/// The assembly-free result of checking a policy document and static configuration.
/// </summary>
public sealed record PolicyCheckOutcome(
    IReadOnlyCollection<string> CompletedChecks,
    IReadOnlyCollection<PolicyCheckDeferredCheck> DeferredChecks)
{
    public bool IsValid => true;
}

/// <summary>Describes a policy check that needs project, assembly, or source facts.</summary>
public sealed record PolicyCheckDeferredCheck(string Kind, string Reason);
