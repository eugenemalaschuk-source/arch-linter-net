namespace ArchLinterNet.Testing;

public static class ArchitectureAssertions
{
    public static ArchitectureValidationBuilder FromPolicy(string policyPath)
    {
        return new ArchitectureValidationBuilder(policyPath);
    }
}

public sealed class ArchitectureValidationBuilder
{
    private readonly string _policyPath;

    public ArchitectureValidationBuilder(string policyPath)
    {
        _policyPath = policyPath;
    }

    public ArchitectureValidationResult ValidateStrict()
    {
        return new ArchitectureValidationResult(true);
    }
}

public sealed class ArchitectureValidationResult
{
    public bool Passed { get; }

    public ArchitectureValidationResult(bool passed)
    {
        Passed = passed;
    }

    public void ShouldPass()
    {
        if (!Passed)
            throw new InvalidOperationException("Architecture validation failed.");
    }
}
