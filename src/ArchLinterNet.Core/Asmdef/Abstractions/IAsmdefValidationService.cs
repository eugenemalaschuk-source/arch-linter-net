namespace ArchLinterNet.Core.Asmdef.Abstractions;

public interface IAsmdefValidationService
{
    AsmdefValidationOutcome Validate(AsmdefValidationRequest request);
}
