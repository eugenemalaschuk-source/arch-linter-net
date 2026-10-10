using System.Numerics;
using System.Text.Json;

namespace ArchLinterNet.Core.Contracts;

internal sealed class CanonicalAnnotationCatalog
{
    private const string ManifestResourceName = "ArchLinterNet.Core.SemanticAnnotations.semantic-annotations.v1.json";
    private static readonly Lazy<CanonicalAnnotationCatalog> _current = new(Load);

    private readonly Dictionary<string, CanonicalRole> _rolesByAttribute;
    private readonly HashSet<string> _knownAttributes;
    private readonly Dictionary<string, string> _metadataKeysByProperty;

    private CanonicalAnnotationCatalog(
        int generation,
        string packageId,
        string minimumToolVersion,
        string supportedPackageRange,
        string reservedNamespace,
        string catalogIdentityMarker,
        Dictionary<string, CanonicalRole> rolesByAttribute,
        HashSet<string> knownAttributes,
        Dictionary<string, string> metadataKeysByProperty)
    {
        Generation = generation;
        PackageId = packageId;
        MinimumToolVersion = minimumToolVersion;
        SupportedPackageRange = supportedPackageRange;
        ReservedNamespace = reservedNamespace;
        CatalogIdentityMarker = catalogIdentityMarker;
        _rolesByAttribute = rolesByAttribute;
        _knownAttributes = knownAttributes;
        _metadataKeysByProperty = metadataKeysByProperty;
    }

    public static CanonicalAnnotationCatalog Current => _current.Value;

    public int Generation { get; }

    public string PackageId { get; }

    public string MinimumToolVersion { get; }

    public string SupportedPackageRange { get; }

    public string ReservedNamespace { get; }

    public string CatalogIdentityMarker { get; }

    public IReadOnlyCollection<CanonicalRole> Roles => _rolesByAttribute.Values;

    public bool TryGetMetadataKey(string propertyName, out string metadataKey) =>
        _metadataKeysByProperty.TryGetValue(propertyName, out metadataKey!);

    public bool TryGetRole(string attributeFullName, out CanonicalRole role) =>
        _rolesByAttribute.TryGetValue(attributeFullName, out role!);

    public bool IsKnownAttribute(string attributeFullName) => _knownAttributes.Contains(attributeFullName);

    public bool IsReservedAttribute(string attributeFullName) =>
        attributeFullName.StartsWith(ReservedNamespace + ".", StringComparison.Ordinal);

    public bool IsSupportedPackageVersion(string version)
    {
        if (!SemanticVersion.TryParse(version, out SemanticVersion parsed)
            || !TryParseRange(SupportedPackageRange, out SemanticVersion lower, out SemanticVersion upper,
                out bool includeLower, out bool includeUpper))
        {
            return false;
        }

        int lowerComparison = parsed.CompareTo(lower);
        int upperComparison = parsed.CompareTo(upper);
        return (lowerComparison > 0 || includeLower && lowerComparison == 0)
            && (upperComparison < 0 || includeUpper && upperComparison == 0);
    }

    private static CanonicalAnnotationCatalog Load()
    {
        using Stream stream = typeof(CanonicalAnnotationCatalog).Assembly
            .GetManifestResourceStream(ManifestResourceName)
            ?? throw new InvalidOperationException($"Embedded semantic annotation manifest '{ManifestResourceName}' is missing.");
        using JsonDocument json = JsonDocument.Parse(stream);
        JsonElement root = json.RootElement;

        JsonElement package = root.GetProperty("package");
        string reservedNamespace = root.GetProperty("reservedNamespace").GetString()!;
        string identityMarker = root.GetProperty("catalogIdentityMarker").GetProperty("fqn").GetString()!;
        Dictionary<string, CanonicalRole> roles = new(StringComparer.Ordinal);
        HashSet<string> known = new(StringComparer.Ordinal) { identityMarker };
        Dictionary<string, string> metadataKeysByProperty = new(StringComparer.Ordinal);
        foreach (JsonElement metadataProperty in root.GetProperty("roleAttributeContract")
                     .GetProperty("metadataProperties").EnumerateArray())
        {
            metadataKeysByProperty.Add(
                metadataProperty.GetProperty("property").GetString()!,
                metadataProperty.GetProperty("key").GetString()!);
        }

        foreach (JsonElement role in root.GetProperty("roles").EnumerateArray())
        {
            string name = role.GetProperty("role").GetString()!;
            string fqn = role.GetProperty("fqn").GetString()!;
            string[] scopes = role.GetProperty("scopes").EnumerateArray().Select(value => value.GetString()!).ToArray();
            roles.Add(fqn, new CanonicalRole(name, fqn, scopes));
            known.Add(fqn);
        }

        foreach (JsonElement contextAttribute in root.GetProperty("contextAttributes").EnumerateArray())
        {
            known.Add(contextAttribute.GetProperty("fqn").GetString()!);
        }

        return new CanonicalAnnotationCatalog(
            root.GetProperty("catalogGeneration").GetInt32(),
            package.GetProperty("id").GetString()!,
            package.GetProperty("minimumAnnotationAwareToolVersion").GetString()!,
            package.GetProperty("compatiblePackageRangeForGeneration1").GetString()!,
            reservedNamespace,
            identityMarker,
            roles,
            known,
            metadataKeysByProperty);
    }

    private static bool TryParseRange(
        string range,
        out SemanticVersion lower,
        out SemanticVersion upper,
        out bool includeLower,
        out bool includeUpper)
    {
        lower = default;
        upper = default;
        includeLower = false;
        includeUpper = false;
        if (range.Length < 5 || (range[0] != '[' && range[0] != '(')
            || (range[^1] != ']' && range[^1] != ')'))
        {
            return false;
        }

        int comma = range.IndexOf(',');
        if (comma < 0 || !SemanticVersion.TryParse(range[1..comma], out lower)
            || !SemanticVersion.TryParse(range[(comma + 1)..^1], out upper))
        {
            return false;
        }

        includeLower = range[0] == '[';
        includeUpper = range[^1] == ']';
        return true;
    }

    internal sealed record CanonicalRole(string Name, string AttributeFullName, IReadOnlyList<string> Scopes);

    internal readonly record struct SemanticVersion(BigInteger Major, BigInteger Minor, BigInteger Patch, string? PreRelease)
        : IComparable<SemanticVersion>
    {
        public static bool TryParse(string value, out SemanticVersion version)
        {
            version = default;
            if (string.IsNullOrEmpty(value) || value[0] == 'v' || value[0] == 'V')
            {
                return false;
            }

            int plus = value.IndexOf('+');
            if (plus >= 0 && (plus != value.LastIndexOf('+') || !IsValidIdentifierList(value[(plus + 1)..], allowNumericLeadingZeros: true)))
            {
                return false;
            }

            string coreAndPreRelease = plus < 0 ? value : value[..plus];
            int dash = coreAndPreRelease.IndexOf('-');
            string core = dash < 0 ? coreAndPreRelease : coreAndPreRelease[..dash];
            string? preRelease = dash < 0 ? null : coreAndPreRelease[(dash + 1)..];
            string[] parts = core.Split('.');
            if (parts.Length != 3
                || !TryParseNumeric(parts[0], out BigInteger major)
                || !TryParseNumeric(parts[1], out BigInteger minor)
                || !TryParseNumeric(parts[2], out BigInteger patch)
                || (dash >= 0 && !IsValidPreRelease(preRelease!)))
            {
                return false;
            }

            version = new SemanticVersion(major, minor, patch, preRelease);
            return true;
        }

        public int CompareTo(SemanticVersion other)
        {
            int comparison = Major.CompareTo(other.Major);
            if (comparison == 0) comparison = Minor.CompareTo(other.Minor);
            if (comparison == 0) comparison = Patch.CompareTo(other.Patch);
            if (comparison != 0) return comparison;
            if (PreRelease is null) return other.PreRelease is null ? 0 : 1;
            if (other.PreRelease is null) return -1;

            string[] left = PreRelease.Split('.');
            string[] right = other.PreRelease.Split('.');
            for (int index = 0; index < Math.Min(left.Length, right.Length); index++)
            {
                bool leftNumeric = IsNumericIdentifier(left[index]);
                bool rightNumeric = IsNumericIdentifier(right[index]);
                comparison = leftNumeric && rightNumeric
                    ? CompareNumericIdentifiers(left[index], right[index])
                    : leftNumeric ? -1 : rightNumeric ? 1 : string.CompareOrdinal(left[index], right[index]);
                if (comparison != 0) return comparison;
            }

            return left.Length.CompareTo(right.Length);
        }

        private static bool TryParseNumeric(string text, out BigInteger value)
        {
            value = BigInteger.Zero;
            return text.Length > 0 && (text.Length == 1 || text[0] != '0')
                && BigInteger.TryParse(text, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        private static bool IsValidPreRelease(string value) =>
            IsValidIdentifierList(value, allowNumericLeadingZeros: false);

        private static bool IsValidIdentifierList(string value, bool allowNumericLeadingZeros)
        {
            if (value.Length == 0) return false;
            return value.Split('.').All(identifier => identifier.Length > 0
                && identifier.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
                && (allowNumericLeadingZeros || !IsNumericIdentifier(identifier)
                    || identifier.Length == 1 || identifier[0] != '0'));
        }

        private static bool IsNumericIdentifier(string value) => value.All(char.IsAsciiDigit);

        private static int CompareNumericIdentifiers(string left, string right) => left.Length != right.Length
            ? left.Length.CompareTo(right.Length)
            : string.CompareOrdinal(left, right);
    }
}
