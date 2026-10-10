namespace ArchLinterNet.Core.Model;

/// <summary>A deterministic compatibility or conflict diagnostic for canonical annotation evidence.</summary>
public sealed record ArchitectureCanonicalAnnotationDiagnostic(
    string Subject,
    string Code,
    string Message,
    IReadOnlyList<string> EvidenceSources)
{
    public static ArchitectureCanonicalAnnotationDiagnostic Create(
        string subject, string code, string message, params string[] evidenceSources) =>
        new(subject, code, message, evidenceSources.OrderBy(value => value, StringComparer.Ordinal).ToArray());
}
