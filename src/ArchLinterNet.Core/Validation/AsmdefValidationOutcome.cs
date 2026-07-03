using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

public sealed record AsmdefValidationOutcome(
    bool Passed,
    IReadOnlyCollection<ArchitectureViolation> Violations);
