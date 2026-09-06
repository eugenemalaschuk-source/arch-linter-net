using System.Security.Cryptography;
using System.Text;
using ArchLinterNet.Core.PolicyContext;

namespace ArchLinterNet.Core.PolicyWeakening;

/// <summary>Validates and binds policy-context artifacts used by weakening comparison.</summary>
internal static class ArchitecturePolicyWeakeningContextSupport
{
    /// <summary>Parses and validates one complete policy-context artifact.</summary>
    internal static ArchitecturePolicyContextExport DeserializeContext(string json)
        => ArchitecturePolicyContextJsonReader.Deserialize(json);

    /// <summary>Calculates the digest that binds optional membership evidence to a policy context.</summary>
    internal static string ComputeContextDigest(ArchitecturePolicyContextExport context)
    {
        ArchitecturePolicyContextJsonReader.Validate(context, "policy context");
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(ArchitecturePolicyContextFormatter.FormatAsJson(context)));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Validates that two policy contexts can be compared.</summary>
    internal static void ValidateComparableContexts(
        ArchitecturePolicyContextExport baseline,
        ArchitecturePolicyContextExport current)
    {
        ArchitecturePolicyContextJsonReader.Validate(baseline, "base policy context");
        ArchitecturePolicyContextJsonReader.Validate(current, "current policy context");
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

}
