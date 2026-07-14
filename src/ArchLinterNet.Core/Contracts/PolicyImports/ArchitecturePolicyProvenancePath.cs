namespace ArchLinterNet.Core.Contracts.PolicyImports;

internal static class ArchitecturePolicyProvenancePath
{
    public const string Root = "";

    public static string Property(string property) => AppendProperty(Root, property);

    public static string AppendProperty(string parent, string property)
    {
        return $"{Normalize(parent)}/{Escape(property)}";
    }

    public static string AppendIndex(string parent, int index)
    {
        return $"{Normalize(parent)}/{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    }

    public static string Parent(string path)
    {
        string normalized = Normalize(path);
        int separator = normalized.LastIndexOf('/');
        return separator <= 0 ? Root : normalized[..separator];
    }

    public static bool IsSameOrDescendant(string path, string ancestor)
    {
        string normalizedPath = Normalize(path);
        string normalizedAncestor = Normalize(ancestor);
        return normalizedAncestor.Length == 0
            || normalizedPath == normalizedAncestor
            || normalizedPath.StartsWith(normalizedAncestor + "/", StringComparison.Ordinal);
    }

    public static bool IsDirectSequenceItem(string path, string sequencePath)
    {
        string normalizedPath = Normalize(path);
        string normalizedSequencePath = Normalize(sequencePath);
        string prefix = normalizedSequencePath + "/";
        return normalizedPath.StartsWith(prefix, StringComparison.Ordinal)
            && normalizedPath[prefix.Length..].IndexOf('/') < 0;
    }

    public static bool TryGetContractGroup(string path, out string group)
    {
        const string ContractsPrefix = "/contracts/";
        string normalized = Normalize(path);
        if (!normalized.StartsWith(ContractsPrefix, StringComparison.Ordinal))
        {
            group = string.Empty;
            return false;
        }

        string[] segments = normalized[ContractsPrefix.Length..].Split('/');
        if (segments.Length != 2 || !int.TryParse(segments[1], out _))
        {
            group = string.Empty;
            return false;
        }

        group = Unescape(segments[0]);
        return true;
    }

    public static string Normalize(string path)
    {
        return string.IsNullOrEmpty(path) || path == "/" ? Root : path;
    }

    private static string Escape(string value)
    {
        return value.Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal);
    }

    private static string Unescape(string value)
    {
        return value.Replace("~1", "/", StringComparison.Ordinal)
            .Replace("~0", "~", StringComparison.Ordinal);
    }
}
