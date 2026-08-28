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
        var seenDeclarations = new HashSet<ArchitectureIgnoredViolation>(ReferenceEqualityComparer.Instance);
        foreach (IArchitectureContract contract in document.Contracts.AllStrict.Concat(document.Contracts.AllAudit))
        {
            foreach (ArchitectureIgnoredViolation ignore in GetIgnoredViolations(contract))
            {
                // Source-set expansion gives each resolved contract a new list, but preserves the
                // authored ignore object in that list. Validate it once: aliases are execution
                // instances, not duplicate policy declarations.
                if (!seenDeclarations.Add(ignore))
                {
                    continue;
                }

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

        ignore.WaiverValidationError = null;

        if (!ignore.HasStructuredWaiverFields)
        {
            if (profile == ArchitectureWaiverProfile.Strict)
            {
                MarkInvalid(
                    document,
                    ignore,
                    $"Strict waiver profile requires structured metadata for ignored violation in contract '{contract.Id ?? contract.Name}'.");
            }

            return;
        }

        document.Provenance.SetValidationSubject(ignore);
        if (string.IsNullOrWhiteSpace(ignore.WaiverId)
            || ignore.Target is null
            || string.IsNullOrWhiteSpace(ignore.Reason)
            || string.IsNullOrWhiteSpace(ignore.Owner)
            || string.IsNullOrWhiteSpace(ignore.Issue)
            || !TryParseDate(ignore.Introduced, out DateOnly introduced)
            || !TryParseDate(ignore.Expires, out DateOnly expires))
        {
            MarkInvalid(document, ignore,
                $"Structured waiver in contract '{contract.Id ?? contract.Name}' requires id, target.fingerprint, reason, owner, issue, introduced, and expires (dates use {DateFormat}).");
            return;
        }

        if (!ArchitectureWaiverTargetFingerprint.IsSupported(ignore.Target.Fingerprint))
        {
            MarkInvalid(document, ignore,
                $"Structured waiver '{ignore.WaiverId}' target.fingerprint must be a canonical lowercase sha256 fingerprint.");
            return;
        }

        if (expires < introduced)
        {
            MarkInvalid(document, ignore,
                $"Structured waiver '{ignore.WaiverId}' expires before it was introduced.");
            return;
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

    private static void MarkInvalid(
        ArchitectureContractDocument document,
        ArchitectureIgnoredViolation ignore,
        string error)
    {
        document.Provenance.SetValidationSubject(ignore);
        ignore.WaiverValidationError = error;
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
