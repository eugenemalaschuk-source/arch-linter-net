using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

public sealed record BaselineUpdateOutcome(
    bool Succeeded,
    string? Yaml,
    int PreservedCount,
    int NewCount,
    IReadOnlyCollection<ArchitectureViolation> ConfigurationViolations);
