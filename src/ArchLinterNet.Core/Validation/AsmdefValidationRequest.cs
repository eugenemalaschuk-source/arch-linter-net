namespace ArchLinterNet.Core.Validation;

public sealed record AsmdefValidationRequest
{
    public required string PolicyPath { get; init; }
}
