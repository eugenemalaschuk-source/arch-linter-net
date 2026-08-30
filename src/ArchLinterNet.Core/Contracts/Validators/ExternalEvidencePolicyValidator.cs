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

        ValidateDiagnosticFilter(document, requirement.DiagnosticFilter, entryPath, id);
    }

    private static void ValidateDiagnosticFilter(
        ArchitectureContractDocument document,
        ArchitectureExternalEvidenceDiagnosticFilter? filter,
        string entryPath,
        string id)
    {
        if (filter is null)
        {
            return;
        }

        string filterPath = Property(entryPath, "diagnostic_filter");
        document.Provenance.SetValidationSubject(filterPath);
        ValidateSelectors(document, filterPath, id, "rule_ids", filter.RuleIds);
        ValidateSelectors(document, filterPath, id, "rule_tags", filter.RuleTags);
        ValidateSelectors(document, filterPath, id, "projects", filter.Projects);
        ValidateSelectors(document, filterPath, id, "path_prefixes", filter.PathPrefixes, paths: true);

        string severityPath = Property(filterPath, "severity");
        document.Provenance.SetValidationSubject(severityPath);
        if (filter.Severity is null || filter.Severity.Count == 0)
        {
            throw new InvalidOperationException(
                $"external_evidence entry '{id}' diagnostic_filter severity must be non-empty.");
        }

        foreach ((string sourceSeverity, string mode) in filter.Severity.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            document.Provenance.SetValidationSubject(Property(severityPath, sourceSeverity));
            if (string.IsNullOrWhiteSpace(sourceSeverity)
                || !ExternalDiagnosticFilterRules.SupportedSeverities.Contains(sourceSeverity, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"external_evidence entry '{id}' diagnostic_filter severity key '{sourceSeverity}' is unsupported or blank.");
            }

            if (string.IsNullOrWhiteSpace(mode)
                || !ExternalDiagnosticFilterRules.SupportedModes.Contains(mode, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"external_evidence entry '{id}' diagnostic_filter severity.{sourceSeverity} mode must be exactly 'strict' or 'audit'.");
            }
        }
    }

    private static void ValidateSelectors(
        ArchitectureContractDocument document,
        string filterPath,
        string id,
        string key,
        IEnumerable<string>? values,
        bool paths = false)
    {
        string fieldPath = Property(filterPath, key);
        document.Provenance.SetValidationSubject(fieldPath);
        if (values is null)
        {
            throw new InvalidOperationException(
                $"external_evidence entry '{id}' diagnostic_filter.{key} must not be null.");
        }

        if (values.Count() > ExternalDiagnosticFilterRules.MaxValuesPerSelector)
        {
            throw new InvalidOperationException(
                $"external_evidence entry '{id}' diagnostic_filter.{key} must contain no more than " +
                $"{ExternalDiagnosticFilterRules.MaxValuesPerSelector} values.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (string? value in values)
        {
            document.Provenance.SetValidationSubject(
                ArchitecturePolicyProvenancePath.AppendIndex(fieldPath, index));
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"external_evidence entry '{id}' diagnostic_filter.{key}[{index}] must be non-blank.");
            }

            if (!seen.Add(value))
            {
                throw new InvalidOperationException(
                    $"external_evidence entry '{id}' diagnostic_filter.{key} declares duplicate value '{value}'.");
            }

            if (paths && !ExternalDiagnosticFilterRules.IsSafePathPrefix(value))
            {
                throw new InvalidOperationException(
                    $"external_evidence entry '{id}' diagnostic_filter.path_prefixes value '{value}' must be a safe repository-relative slash-normalized path prefix.");
            }

            index++;
        }
    }

    private static string EntryPath(int index) => ArchitecturePolicyProvenancePath.AppendIndex(
        ArchitecturePolicyProvenancePath.Property("external_evidence"), index);

    private static string Property(string parent, string property) =>
        ArchitecturePolicyProvenancePath.AppendProperty(parent, property);
}
