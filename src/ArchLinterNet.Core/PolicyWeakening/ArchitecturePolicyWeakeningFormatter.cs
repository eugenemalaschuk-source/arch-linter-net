using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArchLinterNet.Core.PolicyContext;

namespace ArchLinterNet.Core.PolicyWeakening;

/// <summary>Validates policy-context artifacts and formats normalized weakening results.</summary>
public static class ArchitecturePolicyWeakeningFormatter
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    private static readonly JsonSerializerOptions _sarifJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>Parses and validates one complete policy-context artifact.</summary>
    public static ArchitecturePolicyContextExport DeserializeContext(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        try
        {
            ArchitecturePolicyContextExport context = JsonSerializer.Deserialize<ArchitecturePolicyContextExport>(json, _jsonOptions)
                ?? throw new ArgumentException("The architecture policy context is empty.", nameof(json));
            ValidateContext(context, "policy context");
            return context;
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("The architecture policy context is not valid JSON.", nameof(json), exception);
        }
    }

    /// <summary>Calculates the digest that binds optional membership evidence to a policy context.</summary>
    public static string ComputeContextDigest(ArchitecturePolicyContextExport context)
    {
        ValidateContext(context, "policy context");
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(ArchitecturePolicyContextFormatter.FormatAsJson(context)));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Formats one comparison result as deterministic JSON.</summary>
    public static string FormatAsJson(ArchitecturePolicyWeakeningResult result)
    {
        ValidateResult(result);
        return JsonSerializer.Serialize(Normalize(result), _jsonOptions);
    }

    /// <summary>Formats one comparison result for terminal and review output.</summary>
    public static string FormatAsHuman(ArchitecturePolicyWeakeningResult result)
    {
        ValidateResult(result);
        ArchitecturePolicyWeakeningResult normalized = Normalize(result);
        StringBuilder builder = new();
        builder.AppendLine("Architecture policy weakening report");
        builder.AppendLine($"Policy: {normalized.PolicyName} (policy v{normalized.PolicyVersion})");
        builder.AppendLine($"Configured severity: {normalized.Severity}");
        builder.AppendLine($"Findings: {normalized.Findings.Count}");
        foreach (ArchitecturePolicyWeakeningFinding finding in normalized.Findings)
        {
            builder.AppendLine($"- [{finding.Severity}] [{finding.Classification}] [{finding.Kind}] {finding.ControlIdentity}");
            AppendValues(builder, "base", finding.BaseValues);
            AppendValues(builder, "current", finding.CurrentValues);
            if (finding.AffectedSubjects.Count > 0)
            {
                builder.AppendLine($"  affected subjects: {string.Join(", ", finding.AffectedSubjects)}");
            }

            if (finding.BaseProvenance is not null)
            {
                builder.AppendLine($"  base provenance: {finding.BaseProvenance.SourcePath}:{finding.BaseProvenance.YamlPath}");
            }

            if (finding.CurrentProvenance is not null)
            {
                builder.AppendLine($"  current provenance: {finding.CurrentProvenance.SourcePath}:{finding.CurrentProvenance.YamlPath}");
            }

            if (!string.IsNullOrWhiteSpace(finding.Rationale))
            {
                builder.AppendLine($"  rationale: {finding.Rationale}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>Formats one comparison result as a SARIF 2.1.0 document.</summary>
    public static string FormatAsSarif(ArchitecturePolicyWeakeningResult result)
    {
        ValidateResult(result);
        ArchitecturePolicyWeakeningResult normalized = Normalize(result);
        SarifRule[] rules = normalized.Findings
            .GroupBy(finding => finding.Kind, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new SarifRule(
                RuleId(group.Key),
                new SarifMessage($"Architecture policy weakening: {group.Key}")))
            .ToArray();
        SarifResult[] findings = normalized.Findings
            .Select(finding => new SarifResult(
                RuleId(finding.Kind),
                SeverityToSarifLevel(finding.Severity),
                new SarifMessage($"{finding.Kind} weakens {finding.ControlIdentity}."),
                new SarifProperties(
                    finding.Identity,
                    finding.Kind,
                    finding.ControlIdentity,
                    finding.Classification,
                    finding.Severity,
                    finding.BaseValues,
                    finding.CurrentValues,
                    finding.AffectedSubjects,
                    finding.BaseProvenance,
                    finding.CurrentProvenance,
                    finding.Rationale)))
            .ToArray();

        return JsonSerializer.Serialize(
            new SarifLog("https://json.schemastore.org/sarif-2.1.0.json", "2.1.0", [
                new SarifRun(new SarifTool(new SarifDriver("ArchLinterNet policy weakening", rules)), findings),
            ]),
            _sarifJsonOptions);
    }

    internal static void ValidateComparableContexts(
        ArchitecturePolicyContextExport baseline,
        ArchitecturePolicyContextExport current)
    {
        ValidateContext(baseline, "base policy context");
        ValidateContext(current, "current policy context");
        if (!string.Equals(baseline.Policy.Name, current.Policy.Name, StringComparison.Ordinal)
            || baseline.Policy.Version != current.Policy.Version)
        {
            throw new ArgumentException("Base and current policy contexts must have the same policy identity.");
        }
    }

    internal static bool TryGetMembership(
        ArchitecturePolicyMembershipEvidence? evidence,
        ArchitecturePolicyContextExport context,
        string family,
        string id,
        out IReadOnlyList<string> subjects)
    {
        subjects = Array.Empty<string>();
        if (evidence is null || !evidence.Complete || evidence.SchemaVersion != ArchitecturePolicyMembershipEvidence.CurrentSchemaVersion
            || !string.Equals(evidence.Kind, ArchitecturePolicyMembershipEvidence.EvidenceKind, StringComparison.Ordinal)
            || !string.Equals(evidence.ContextDigest, ComputeContextDigest(context), StringComparison.Ordinal)
            || evidence.Contracts is null)
        {
            return false;
        }

        ArchitecturePolicyContractMembership? membership = evidence.Contracts.SingleOrDefault(item =>
            string.Equals(item.Family, family, StringComparison.Ordinal)
            && string.Equals(item.Id, id, StringComparison.Ordinal));
        if (membership is null || membership.Subjects is null || membership.Subjects.Any(string.IsNullOrWhiteSpace))
        {
            return false;
        }

        subjects = membership.Subjects.Distinct(StringComparer.Ordinal).OrderBy(subject => subject, StringComparer.Ordinal).ToArray();
        return true;
    }

    private static void ValidateContext(ArchitecturePolicyContextExport context, string inputName)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.SchemaVersion != ArchitecturePolicyContextExport.CurrentSchemaVersion
            || !string.Equals(context.Kind, "architecture-policy-context", StringComparison.Ordinal))
        {
            throw new ArgumentException($"The {inputName} has an unsupported schema or kind.");
        }

        if (context.Policy is null || string.IsNullOrWhiteSpace(context.Policy.Name) || context.Policy.Version <= 0)
        {
            throw new ArgumentException($"The {inputName} is missing policy identity.");
        }

        if (context.Guardrails is null || context.Guardrails.PolicyWeakening is not ("error" or "warn" or "off"))
        {
            throw new ArgumentException($"The {inputName} is missing a valid policy-weakening severity.");
        }

        if (context.Analysis is null || context.Analysis.TargetAssemblies is null || context.Analysis.Projects is null
            || context.Analysis.ProjectInclude is null || context.Analysis.ProjectExclude is null || context.Analysis.SourceRoots is null
            || context.Sources is null || context.Layers is null || context.Contracts is null || context.Classification is null
            || context.SemanticRoles is null || context.Contexts is null || context.SourceSets is null
            || context.SourceExpansions is null || context.Exceptions is null || context.Guidance is null)
        {
            throw new ArgumentException($"The {inputName} is incomplete.");
        }

        if (context.Sources.Any(source => source is null || string.IsNullOrWhiteSpace(source.Path))
            || context.Contracts.Any(contract => contract is null || string.IsNullOrWhiteSpace(contract.Family)
                || string.IsNullOrWhiteSpace(contract.Id) || contract.Mode is not ("strict" or "audit"))
            || context.SourceSets.Any(sourceSet => sourceSet is null || string.IsNullOrWhiteSpace(sourceSet.Name))
            || context.SourceExpansions.Any(expansion => expansion is null || string.IsNullOrWhiteSpace(expansion.AuthoredContractId)))
        {
            throw new ArgumentException($"The {inputName} contains incomplete effective-policy evidence.");
        }
    }

    private static void ValidateResult(ArchitecturePolicyWeakeningResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.SchemaVersion != ArchitecturePolicyWeakeningResult.CurrentSchemaVersion
            || !string.Equals(result.Kind, ArchitecturePolicyWeakeningResult.ResultKind, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(result.PolicyName)
            || result.PolicyVersion <= 0
            || result.Severity is not ("error" or "warn" or "off")
            || result.Findings is null)
        {
            throw new ArgumentException("The policy weakening result is incomplete or unsupported.", nameof(result));
        }
    }

    private static ArchitecturePolicyWeakeningResult Normalize(ArchitecturePolicyWeakeningResult result) => result with
    {
        Findings = result.Findings
            .OrderBy(finding => finding.Kind, StringComparer.Ordinal)
            .ThenBy(finding => finding.ControlIdentity, StringComparer.Ordinal)
            .ThenBy(finding => finding.Identity, StringComparer.Ordinal)
            .Select(finding => finding with
            {
                BaseValues = finding.BaseValues.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                CurrentValues = finding.CurrentValues.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                AffectedSubjects = finding.AffectedSubjects.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            })
            .ToArray(),
    };

    private static void AppendValues(StringBuilder builder, string label, IReadOnlyList<string> values)
    {
        if (values.Count > 0)
        {
            builder.AppendLine($"  {label}: {string.Join(", ", values)}");
        }
    }

    private static string RuleId(string kind) => "ArchLinterNet.PolicyWeakening." + kind;

    private static string SeverityToSarifLevel(string severity) => severity switch
    {
        "error" => "error",
        "warn" => "warning",
        _ => "note",
    };

    private sealed record SarifLog(
        [property: JsonPropertyName("$schema")] string Schema,
        string Version,
        IReadOnlyList<SarifRun> Runs);

    private sealed record SarifRun(SarifTool Tool, IReadOnlyList<SarifResult> Results);

    private sealed record SarifTool(SarifDriver Driver);

    private sealed record SarifDriver(string Name, IReadOnlyList<SarifRule> Rules);

    private sealed record SarifRule(string Id, SarifMessage ShortDescription);

    private sealed record SarifResult(string RuleId, string Level, SarifMessage Message, SarifProperties Properties);

    private sealed record SarifMessage(string Text);

    private sealed record SarifProperties(
        string Identity,
        string Kind,
        string ControlIdentity,
        string Classification,
        string Severity,
        IReadOnlyList<string> BaseValues,
        IReadOnlyList<string> CurrentValues,
        IReadOnlyList<string> AffectedSubjects,
        ArchitecturePolicyContextProvenance? BaseProvenance,
        ArchitecturePolicyContextProvenance? CurrentProvenance,
        string? Rationale);
}
