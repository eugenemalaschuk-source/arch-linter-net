using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

/// <summary>
/// Resolves the <c>reason</c> text for a <em>newly added</em> baseline entry. Adoption rarely wants
/// one sentence for every family at once — package debt and composition debt are usually tracked
/// separately — so a reason can be mapped per contract id and per contract family, with the flat
/// <c>--reason</c> value as the fallback.
/// </summary>
/// <remarks>
/// Mapping never touches an entry carried through from an existing baseline: those keep their
/// recorded reason verbatim, which is what makes an update reviewable as "only the new entries got
/// this text".
/// </remarks>
public sealed class BaselineReasonMap
{
    public const string DefaultReasonText = "generated baseline";

    private readonly Dictionary<string, string> _byContract;
    private readonly Dictionary<string, string> _byFamily;

    private BaselineReasonMap(
        Dictionary<string, string> byContract,
        Dictionary<string, string> byFamily,
        string defaultReason)
    {
        _byContract = byContract;
        _byFamily = byFamily;
        DefaultReason = defaultReason;
    }

    public string DefaultReason { get; }

    public static BaselineReasonMap ForDefault(string defaultReason)
    {
        return new BaselineReasonMap(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            defaultReason);
    }

    /// <summary>
    /// Parses repeatable <c>key=value</c> mapping arguments. A malformed or duplicated key fails
    /// closed with an explicit message instead of being dropped: a silently ignored mapping produces
    /// a baseline whose reasons quietly disagree with what the author asked for.
    /// </summary>
    public static bool TryParse(
        IReadOnlyCollection<string>? contractMappings,
        IReadOnlyCollection<string>? familyMappings,
        string defaultReason,
        out BaselineReasonMap map,
        out string? error)
    {
        map = ForDefault(defaultReason);

        if (!TryParsePairs(contractMappings, "--reason-for-contract", out Dictionary<string, string> byContract, out error)
            || !TryParsePairs(familyMappings, "--reason-for-family", out Dictionary<string, string> byFamily, out error))
        {
            return false;
        }

        map = new BaselineReasonMap(byContract, byFamily, defaultReason);
        return true;
    }

    /// <summary>
    /// Resolution order: contract id, then contract family, then the flat default.
    /// </summary>
    public string Resolve(string contractId, string contractGroup)
    {
        if (_byContract.TryGetValue(contractId, out string? contractReason))
        {
            return contractReason;
        }

        string family = ArchitectureViolationIdentity.ResolveContractFamily(contractGroup);
        if (_byFamily.TryGetValue(family, out string? familyReason))
        {
            return familyReason;
        }

        return DefaultReason;
    }

    private static bool TryParsePairs(
        IReadOnlyCollection<string>? mappings,
        string optionName,
        out Dictionary<string, string> parsed,
        out string? error)
    {
        parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        error = null;

        if (mappings == null)
        {
            return true;
        }

        foreach (string mapping in mappings)
        {
            int separator = mapping.IndexOf('=', StringComparison.Ordinal);
            if (separator < 0)
            {
                error = $"{optionName} expects '<key>=<reason text>' but received '{mapping}'.";
                return false;
            }

            string key = mapping[..separator].Trim();
            string value = mapping[(separator + 1)..].Trim();

            if (key.Length == 0)
            {
                error = $"{optionName} received an empty key in '{mapping}'.";
                return false;
            }

            if (value.Length == 0)
            {
                error = $"{optionName} received an empty reason text for key '{key}'.";
                return false;
            }

            if (!parsed.TryAdd(key, value))
            {
                error = $"{optionName} received more than one mapping for key '{key}'.";
                return false;
            }
        }

        return true;
    }
}
