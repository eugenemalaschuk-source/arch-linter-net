using ArchLinterNet.Core;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Testing;

public static class ArchitectureAssertions
{
    public static ArchitectureValidationBuilder FromPolicy(string policyPath)
    {
        return new ArchitectureValidationBuilder(policyPath);
    }

    public static ArchitectureValidationBuilder FromRepositoryRoot(string repositoryRoot)
    {
        string policyPath = Path.Combine(repositoryRoot, "architecture", "dependencies.arch.yml");
        return new ArchitectureValidationBuilder(policyPath);
    }

    /// <summary>Checks policy and static configuration without loading target assemblies.</summary>
    public static PolicyCheckOutcome CheckPolicy(string policyPath)
    {
        return ArchitectureValidator.CheckPolicy(policyPath);
    }
}
