namespace ArchLinterNet.Core.Contracts;

/// <summary>Resolves the configured architecture-waiver governance profile.</summary>
public static class ArchitectureWaiverProfile
{
    public const string Compatibility = "compatibility";
    public const string Strict = "strict";

    /// <summary>Resolves the explicit profile or the policy-version default.</summary>
    public static string Resolve(ArchitectureContractDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!string.IsNullOrWhiteSpace(document.Analysis.WaiverLifecycleProfile))
        {
            return document.Analysis.WaiverLifecycleProfile;
        }

        return document.Version >= 2 ? Strict : Compatibility;
    }
}
