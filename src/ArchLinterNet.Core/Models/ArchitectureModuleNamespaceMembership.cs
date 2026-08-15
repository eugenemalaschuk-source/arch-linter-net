namespace ArchLinterNet.Core.Model;

/// <summary>
/// The structural position of a namespace beneath a feature-module container.
/// </summary>
public sealed record ArchitectureModuleNamespaceMembership(
    string Container,
    string? ModuleName,
    string? Segment,
    bool IsContainerRoot);

/// <summary>
/// Resolves feature-module membership from a namespace without enumerating assemblies or knowing
/// a profile's permitted segments. Both policy checking and host composition use this one parser so
/// a namespace cannot be governed by one interpretation and activated by another.
/// </summary>
public static class ArchitectureModuleNamespaceMembershipResolver
{
    /// <summary>
    /// Resolves a namespace under <paramref name="container"/> into its direct feature module and
    /// first segment below that module.
    /// </summary>
    public static bool TryResolve(
        string container,
        string candidateNamespace,
        out ArchitectureModuleNamespaceMembership? membership)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(container);
        ArgumentNullException.ThrowIfNull(candidateNamespace);

        if (string.Equals(candidateNamespace, container, StringComparison.Ordinal))
        {
            membership = new ArchitectureModuleNamespaceMembership(container, null, null, IsContainerRoot: true);
            return true;
        }

        string prefix = container + ".";
        if (!candidateNamespace.StartsWith(prefix, StringComparison.Ordinal))
        {
            membership = null;
            return false;
        }

        string remainder = candidateNamespace[prefix.Length..];
        int moduleSeparator = remainder.IndexOf('.');
        string moduleName = moduleSeparator < 0 ? remainder : remainder[..moduleSeparator];
        string? segment = moduleSeparator < 0 ? null : FirstSegment(remainder[(moduleSeparator + 1)..]);
        membership = new ArchitectureModuleNamespaceMembership(container, moduleName, segment, IsContainerRoot: false);
        return true;
    }

    private static string FirstSegment(string value)
    {
        int separator = value.IndexOf('.');
        return separator < 0 ? value : value[..separator];
    }
}
