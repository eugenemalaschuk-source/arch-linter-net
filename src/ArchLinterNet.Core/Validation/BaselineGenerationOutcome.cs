using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

public sealed record BaselineGenerationOutcome(
    bool Succeeded,
    string? Yaml,
    int CandidateCount,
    IReadOnlyCollection<ArchitectureViolation> ConfigurationViolations);
