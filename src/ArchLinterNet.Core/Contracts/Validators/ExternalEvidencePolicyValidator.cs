using ArchLinterNet.Core.Contracts.PolicyImports;

namespace ArchLinterNet.Core.Contracts.Validators;

// Validates the typed external-evidence requirement collection. Artifact reading and result
// selection intentionally happen in later capabilities; this boundary only guarantees that a
// caller receives an unambiguous logical requirement.
internal sealed class ExternalEvidencePolicyValidator : IArchitecturePolicyDocumentValidator
{
    private const string SupportedFormat = "sarif";

    public void Validate(ArchitectureContractDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < document.ExternalEvidence.Count; index++)
        {
            ArchitectureExternalEvidenceRequirement requirement = document.ExternalEvidence[index];
            string entryPath = EntryPath(index);
            document.Provenance.SetValidationSubject(entryPath);

            if (requirement is null)
            {
                throw new InvalidOperationException($"external_evidence entry {index} must not be null.");
            }

            if (string.IsNullOrWhiteSpace(requirement.Id))
            {
                throw new InvalidOperationException($"external_evidence entry {index} must declare a non-blank id.");
            }

            if (!ids.Add(requirement.Id))
            {
                throw new InvalidOperationException(
                    $"external_evidence declares duplicate id '{requirement.Id}'.");
            }

            document.Provenance.SetValidationSubject(Property(entryPath, "format"));
            if (!string.Equals(requirement.Format, SupportedFormat, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"external_evidence entry '{requirement.Id}' format must be exactly 'sarif'.");
            }

            document.Provenance.SetValidationSubject(Property(entryPath, "tool"));
            if (string.IsNullOrWhiteSpace(requirement.Tool))
            {
                throw new InvalidOperationException(
                    $"external_evidence entry '{requirement.Id}' must declare a non-blank tool.");
            }

            document.Provenance.SetValidationSubject(Property(entryPath, "tool_version"));
            if (requirement.ToolVersion is not null && string.IsNullOrWhiteSpace(requirement.ToolVersion))
            {
                throw new InvalidOperationException(
                    $"external_evidence entry '{requirement.Id}' tool_version must be non-blank when declared.");
            }

            document.Provenance.SetValidationSubject(Property(entryPath, "run"));
            if (string.IsNullOrWhiteSpace(requirement.Run))
            {
                throw new InvalidOperationException(
                    $"external_evidence entry '{requirement.Id}' must declare a non-blank run.");
            }
        }
    }

    private static string EntryPath(int index) => ArchitecturePolicyProvenancePath.AppendIndex(
        ArchitecturePolicyProvenancePath.Property("external_evidence"), index);

    private static string Property(string parent, string property) =>
        ArchitecturePolicyProvenancePath.AppendProperty(parent, property);
}
