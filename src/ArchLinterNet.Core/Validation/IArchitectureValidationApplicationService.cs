using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation;

public interface IArchitectureValidationApplicationService
{
    ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing = null);
}
