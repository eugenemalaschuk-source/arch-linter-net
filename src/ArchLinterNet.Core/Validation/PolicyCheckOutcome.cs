using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

/// <summary>
/// The assembly-free result of checking a policy document and static configuration.
/// </summary>
public sealed record PolicyCheckOutcome(
    IReadOnlyCollection<string> CompletedChecks,
    IReadOnlyCollection<PolicyCheckDeferredCheck> DeferredChecks,
    PolicyCheckFailure? Failure = null)
{
    public bool IsValid => Failure is null;

    public static PolicyCheckOutcome Invalid(PolicyCheckFailure failure)
    {
        return new PolicyCheckOutcome(Array.Empty<string>(), Array.Empty<PolicyCheckDeferredCheck>(), failure);
    }
}

/// <summary>Describes a policy check that needs project, assembly, or source facts.</summary>
public sealed record PolicyCheckDeferredCheck(
    string Kind,
    string Reason,
    IReadOnlyCollection<ArchitecturePolicySourceLocation> PolicyLocations);

/// <summary>Describes an expected typed policy/configuration failure.</summary>
public sealed record PolicyCheckFailure(
    string Message,
    string Category,
    ArchitecturePolicyDiagnostic? Diagnostic);
