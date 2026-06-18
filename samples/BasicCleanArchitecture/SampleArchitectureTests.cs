using ArchLinterNet.Testing;

// Strict validation — blocks CI when violations are found
ArchitectureAssertions
    .FromPolicy("architecture/dependencies.arch.yml")
    .ValidateStrict()
    .ShouldPass();

// Audit validation — reports diagnostics without blocking CI
ArchitectureAssertions
    .FromPolicy("architecture/dependencies.arch.yml")
    .ValidateAudit()
    .ShouldPass();
