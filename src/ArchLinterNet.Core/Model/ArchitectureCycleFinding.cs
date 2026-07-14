namespace ArchLinterNet.Core.Model;

public sealed record ArchitectureCycleFinding(
    string ContractName,
    string? ContractId,
    string Path)
{
    public ArchitecturePolicySourceLocation? PolicyLocation { get; init; }

    public IReadOnlyCollection<ArchitecturePolicySourceLocation> RelatedPolicyLocations { get; init; } =
        Array.Empty<ArchitecturePolicySourceLocation>();
}
