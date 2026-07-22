namespace ArchLinterNet.Core.Model;

public sealed record FrameworkReferenceEvidence(
    string FrameworkName,
    string TargetFramework,
    bool Explicit,
    string SourcePath);
