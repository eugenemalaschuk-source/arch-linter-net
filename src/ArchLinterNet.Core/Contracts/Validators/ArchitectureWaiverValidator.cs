using System.Reflection;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class ArchitectureWaiverValidator : IArchitecturePolicyDocumentValidator
{
    private const string DateFormat = "yyyy-MM-dd";

    public void Validate(ArchitectureContractDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        string profile = ArchitectureWaiverProfile.Resolve(document);
        if (profile is not (ArchitectureWaiverProfile.Compatibility or ArchitectureWaiverProfile.Strict))
        {
            throw new InvalidOperationException(
                $"Invalid analysis.waiver_lifecycle_profile: {profile}. Use 'strict' or 'compatibility'.");
        }

        var declaredIds = new Dictionary<string, ArchitectureIgnoredViolation>(StringComparer.Ordinal);
        foreach (IArchitectureContract contract in document.Contracts.AllStrict.Concat(document.Contracts.AllAudit))
        {
            foreach (ArchitectureIgnoredViolation ignore in GetIgnoredViolations(contract))
            {
                ValidateEntry(document, contract, ignore, profile, declaredIds);
            }
        }
    }

    private static void ValidateEntry(
        ArchitectureContractDocument document,
        IArchitectureContract contract,
        ArchitectureIgnoredViolation ignore,
        string profile,
        IDictionary<string, ArchitectureIgnoredViolation> declaredIds)
    {
        if (ignore.IsBaselineImported)
        {
            return;
        }

        if (!ignore.HasStructuredWaiverFields)
        {
            if (profile == ArchitectureWaiverProfile.Strict)
            {
                document.Provenance.SetValidationSubject(ignore);
                throw new InvalidOperationException(
                    $"Strict waiver profile requires structured metadata for ignored violation in contract '{contract.Id ?? contract.Name}'.");
            }

            return;
        }

        document.Provenance.SetValidationSubject(ignore);
        if (string.IsNullOrWhiteSpace(ignore.WaiverId)
            || ignore.Target is null
            || !ArchitectureWaiverTargetFingerprint.IsSupported(ignore.Target.Fingerprint)
            || string.IsNullOrWhiteSpace(ignore.Reason)
            || string.IsNullOrWhiteSpace(ignore.Owner)
            || string.IsNullOrWhiteSpace(ignore.Issue)
            || !TryParseDate(ignore.Introduced, out DateOnly introduced)
            || !TryParseDate(ignore.Expires, out DateOnly expires))
        {
            throw new InvalidOperationException(
                $"Structured waiver in contract '{contract.Id ?? contract.Name}' requires id, target.fingerprint, reason, owner, issue, introduced, and expires (dates use {DateFormat}).");
        }

        if (expires < introduced)
        {
            throw new InvalidOperationException(
                $"Structured waiver '{ignore.WaiverId}' expires before it was introduced.");
        }

        if (declaredIds.TryGetValue(ignore.WaiverId, out ArchitectureIgnoredViolation? first))
        {
            Exception enriched = document.Provenance.EnrichValidationException(
                new InvalidOperationException($"Duplicate structured waiver id '{ignore.WaiverId}'."),
                [first, ignore]);
            throw enriched;
        }

        declaredIds.Add(ignore.WaiverId, ignore);
    }

    private static bool TryParseDate(string? value, out DateOnly date) => DateOnly.TryParseExact(
        value, DateFormat, System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.None, out date);

    private static IEnumerable<ArchitectureIgnoredViolation> GetIgnoredViolations(IArchitectureContract contract)
    {
        PropertyInfo? property = contract.GetType().GetProperty("IgnoredViolations");
        return property?.GetValue(contract) as IEnumerable<ArchitectureIgnoredViolation>
            ?? Array.Empty<ArchitectureIgnoredViolation>();
    }
}
