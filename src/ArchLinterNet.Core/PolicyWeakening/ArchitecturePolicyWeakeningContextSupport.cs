using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Core.PolicyContext;

namespace ArchLinterNet.Core.PolicyWeakening;

/// <summary>Validates and binds policy-context artifacts used by weakening comparison.</summary>
internal static class ArchitecturePolicyWeakeningContextSupport
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    /// <summary>Parses and validates one complete policy-context artifact.</summary>
    internal static ArchitecturePolicyContextExport DeserializeContext(string json)
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
    internal static string ComputeContextDigest(ArchitecturePolicyContextExport context)
    {
        ValidateContext(context, "policy context");
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(ArchitecturePolicyContextFormatter.FormatAsJson(context)));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Validates that two policy contexts can be compared.</summary>
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

    /// <summary>Resolves membership evidence when it is complete and bound to the supplied context.</summary>
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
        ValidateSchemaAndKind(context, inputName);
        ValidatePolicyIdentity(context, inputName);
        ValidateGuardrails(context, inputName);
        ValidateCompleteness(context, inputName);
        ValidateEffectivePolicyEvidence(context, inputName);
    }

    private static void ValidateSchemaAndKind(ArchitecturePolicyContextExport context, string inputName)
    {
        if (context.SchemaVersion != ArchitecturePolicyContextExport.CurrentSchemaVersion
            || !string.Equals(context.Kind, "architecture-policy-context", StringComparison.Ordinal))
        {
            throw new ArgumentException($"The {inputName} has an unsupported schema or kind.");
        }
    }

    private static void ValidatePolicyIdentity(ArchitecturePolicyContextExport context, string inputName)
    {
        if (context.Policy is null || string.IsNullOrWhiteSpace(context.Policy.Name) || context.Policy.Version <= 0)
        {
            throw new ArgumentException($"The {inputName} is missing policy identity.");
        }
    }

    private static void ValidateGuardrails(ArchitecturePolicyContextExport context, string inputName)
    {
        if (context.Guardrails is null || context.Guardrails.PolicyWeakening is not ("error" or "warn" or "off"))
        {
            throw new ArgumentException($"The {inputName} is missing a valid policy-weakening severity.");
        }
    }

    private static void ValidateCompleteness(ArchitecturePolicyContextExport context, string inputName)
    {
        if (context.Analysis is null || context.Analysis.TargetAssemblies is null || context.Analysis.Projects is null
            || context.Analysis.ProjectInclude is null || context.Analysis.ProjectExclude is null || context.Analysis.SourceRoots is null
            || context.Sources is null || context.Layers is null || context.Contracts is null || context.Classification is null
            || context.SemanticRoles is null || context.Contexts is null || context.SourceSets is null
            || context.SourceExpansions is null || context.Exceptions is null || context.Guidance is null
            || context.Waivers is null || string.IsNullOrWhiteSpace(context.WaiverLifecycleProfile))
        {
            throw new ArgumentException($"The {inputName} is incomplete.");
        }
    }

    private static void ValidateEffectivePolicyEvidence(ArchitecturePolicyContextExport context, string inputName)
    {
        if (HasIncompleteSources(context) || HasIncompleteContracts(context) || HasIncompleteSourceSets(context)
            || HasIncompleteSourceExpansions(context) || HasIncompleteExceptions(context) || HasIncompleteWaivers(context))
        {
            throw new ArgumentException($"The {inputName} contains incomplete effective-policy evidence.");
        }
    }

    private static bool HasIncompleteSources(ArchitecturePolicyContextExport context) =>
        context.Sources.Any(source => source is null || string.IsNullOrWhiteSpace(source.Path));

    private static bool HasIncompleteContracts(ArchitecturePolicyContextExport context) =>
        context.Contracts.Any(contract => contract is null || string.IsNullOrWhiteSpace(contract.Family)
            || string.IsNullOrWhiteSpace(contract.Id) || contract.Mode is not ("strict" or "audit"));

    private static bool HasIncompleteSourceSets(ArchitecturePolicyContextExport context) =>
        context.SourceSets.Any(sourceSet => sourceSet is null || string.IsNullOrWhiteSpace(sourceSet.Name));

    private static bool HasIncompleteSourceExpansions(ArchitecturePolicyContextExport context) =>
        context.SourceExpansions.Any(expansion => expansion is null || string.IsNullOrWhiteSpace(expansion.AuthoredContractId));

    private static bool HasIncompleteExceptions(ArchitecturePolicyContextExport context) =>
        context.Exceptions.Any(exceptionItem => exceptionItem is null
            || string.IsNullOrWhiteSpace(exceptionItem.Scope)
            || string.IsNullOrWhiteSpace(exceptionItem.Subject)
            || string.IsNullOrWhiteSpace(exceptionItem.Kind)
            || (exceptionItem.Kind == "ignored_violation" && (exceptionItem.IgnoredViolation is null
                || string.IsNullOrWhiteSpace(exceptionItem.IgnoredViolation.SourceType)
                || string.IsNullOrWhiteSpace(exceptionItem.IgnoredViolation.ForbiddenReference))));

    private static bool HasIncompleteWaivers(ArchitecturePolicyContextExport context) => context.Waivers.Any(waiver =>
        waiver is null || string.IsNullOrWhiteSpace(waiver.Mode) || string.IsNullOrWhiteSpace(waiver.ContractFamily)
        || string.IsNullOrWhiteSpace(waiver.ContractId) || string.IsNullOrWhiteSpace(waiver.WaiverId)
        || string.IsNullOrWhiteSpace(waiver.TargetFingerprint));
}
