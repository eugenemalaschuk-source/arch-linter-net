namespace ArchLinterNet.Core.Discovery;

internal sealed record ArchitectureProjectRoslynContext(
    string ProjectPath,
    IReadOnlyList<string> SourceFilePaths,
    IReadOnlyList<string> ReferenceAssemblyPaths);

internal sealed class ArchitectureProjectRoslynResolution
{
    private ArchitectureProjectRoslynResolution(ArchitectureProjectRoslynContext? context, string? failureReason)
    {
        Context = context;
        FailureReason = failureReason;
    }

    public ArchitectureProjectRoslynContext? Context { get; }

    public string? FailureReason { get; }

    public bool Succeeded => Context != null;

    public static ArchitectureProjectRoslynResolution Success(ArchitectureProjectRoslynContext context)
    {
        return new ArchitectureProjectRoslynResolution(context, null);
    }

    public static ArchitectureProjectRoslynResolution Failure(string reason)
    {
        return new ArchitectureProjectRoslynResolution(null, reason);
    }
}
