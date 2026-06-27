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
}
