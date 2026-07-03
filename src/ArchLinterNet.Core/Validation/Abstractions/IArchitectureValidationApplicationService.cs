using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Core.Validation.Abstractions;

public interface IArchitectureValidationApplicationService
{
    ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing = null);
}
