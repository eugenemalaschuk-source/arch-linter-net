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
            string id = ValidateIdentity(requirement, index);
            if (!ids.Add(id))
            {
                throw new InvalidOperationException($"external_evidence declares duplicate id '{id}'.");
            }

            ValidateProperties(document, requirement, entryPath, id);
        }
    }

    private static string ValidateIdentity(ArchitectureExternalEvidenceRequirement? requirement, int index)
    {
        if (requirement is null)
        {
            throw new InvalidOperationException($"external_evidence entry {index} must not be null.");
        }

        if (string.IsNullOrWhiteSpace(requirement.Id))
        {
            throw new InvalidOperationException($"external_evidence entry {index} must declare a non-blank id.");
        }

        return requirement.Id;
    }

    private static void ValidateProperties(
        ArchitectureContractDocument document,
        ArchitectureExternalEvidenceRequirement requirement,
        string entryPath,
        string id)
    {
        document.Provenance.SetValidationSubject(Property(entryPath, "format"));
        if (!string.Equals(requirement.Format, SupportedFormat, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"external_evidence entry '{id}' format must be exactly 'sarif'.");
        }

        document.Provenance.SetValidationSubject(Property(entryPath, "tool"));
        if (string.IsNullOrWhiteSpace(requirement.Tool))
        {
            throw new InvalidOperationException($"external_evidence entry '{id}' must declare a non-blank tool.");
        }

        document.Provenance.SetValidationSubject(Property(entryPath, "tool_version"));
        if (requirement.ToolVersion is not null && string.IsNullOrWhiteSpace(requirement.ToolVersion))
        {
            throw new InvalidOperationException($"external_evidence entry '{id}' tool_version must be non-blank when declared.");
        }

        document.Provenance.SetValidationSubject(Property(entryPath, "run"));
        if (string.IsNullOrWhiteSpace(requirement.Run))
        {
            throw new InvalidOperationException($"external_evidence entry '{id}' must declare a non-blank run.");
        }
    }

    private static string EntryPath(int index) => ArchitecturePolicyProvenancePath.AppendIndex(
        ArchitecturePolicyProvenancePath.Property("external_evidence"), index);

    private static string Property(string parent, string property) =>
        ArchitecturePolicyProvenancePath.AppendProperty(parent, property);
}
