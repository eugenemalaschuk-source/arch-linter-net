namespace ArchLinterNet.Core.Model;

public sealed record BuildStatePreflightDiagnostic(
    string ContractName,
    string? ContractId,
    BuildStatePreflightState State,
    BuildStatePreflightEvidence Evidence)
    : ArchitectureDiagnostic(ContractName, ContractId)
{
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.BuildStatePreflight;

    // Every state up to and including UnverifiableArtifact stops the run before contract
    // execution; only Current allows contract execution to proceed for the affected project.
    public bool IsBlocking => State != BuildStatePreflightState.Current;
}
