using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ArchLinterNet.Core.Model;

/// <summary>Creates the stable SHA-256 target fingerprint used by structured architecture waivers.</summary>
public static class ArchitectureWaiverTargetFingerprint
{
    private const string Prefix = "sha256:";

    /// <summary>Creates a canonical target fingerprint for a versioned violation identity.</summary>
    public static string Create(ArchitectureViolationIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        string?[] values =
        [
            identity.IdentityVersion.ToString(CultureInfo.InvariantCulture),
            identity.ContractFamily,
            identity.Kind,
            identity.ContractId,
            identity.SourceAssembly,
            identity.SourceType,
            identity.SourceMember,
            identity.TargetAssembly,
            identity.TargetType,
            identity.TargetMember,
            identity.Occurrence.ToString(CultureInfo.InvariantCulture),
            identity.Configuration,
        ];

        var builder = new StringBuilder();
        foreach (string? value in values)
        {
            string current = value ?? "<null>";
            builder.Append(current.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(current);
            builder.Append(';');
        }

        return Prefix + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    /// <summary>Gets whether a supplied target fingerprint has the supported stable shape.</summary>
    public static bool IsSupported(string? value) => value is { Length: 71 }
        && value.StartsWith(Prefix, StringComparison.Ordinal)
        && value[Prefix.Length..].All(character => char.IsAsciiHexDigit(character));
}
