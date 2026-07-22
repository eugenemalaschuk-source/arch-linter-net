using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

// Human-readable and CI-JSON rendering of FrameworkReference evidence (TargetFramework,
// explicit/implicit classification, declaring project SourcePath) shared by
// FrameworkReferenceDiagnostic/FrameworkReferenceAllowOnlyDiagnostic. Split into its own partial
// file to keep ArchitectureDiagnosticFormatter.cs under the repository's 800-line decomposition
// limit, mirroring ArchitectureDiagnosticFormatter.Context.cs.
public sealed partial class ArchitectureDiagnosticFormatter
{
    private static string FormatFrameworkReferenceContextForHumans(IReadOnlyCollection<FrameworkReferenceEvidence> evidence)
    {
        string entries = string.Join(", ", evidence.Select(e =>
            $"{e.FrameworkName} ({e.TargetFramework}, {(e.Explicit ? "explicit" : "implicit")}, {e.SourcePath})"));
        return $" [{entries}]";
    }

    private static void ApplyFrameworkReferenceEvidenceCiFields(
        IReadOnlyCollection<FrameworkReferenceEvidence> evidence, Dictionary<string, object?> obj)
    {
        if (evidence.Count == 0)
        {
            return;
        }

        obj["evidence"] = evidence.Select(e => (object)new Dictionary<string, object?>
        {
            ["framework_name"] = e.FrameworkName,
            ["target_framework"] = e.TargetFramework,
            ["explicit"] = e.Explicit,
            ["source_path"] = e.SourcePath,
        }).ToArray();
    }
}
