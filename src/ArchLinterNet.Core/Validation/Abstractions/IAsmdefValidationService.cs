using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Core.Validation.Abstractions;

public interface IAsmdefValidationService
{
    AsmdefValidationOutcome Validate(AsmdefValidationRequest request);
}
