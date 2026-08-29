using System.Text.Json;

namespace ArchLinterNet.Core.PolicyContext;

// Topology selector equality must remain structural: it controls canonical export order and
// policy-weakening visibility, so delimiter-bearing metadata values must never collapse.
internal sealed class ArchitecturePolicyContextTopologySelectorComparer : IComparer<ArchitecturePolicyContextTopologySelector>
{
    public static ArchitecturePolicyContextTopologySelectorComparer Instance { get; } = new();

    public int Compare(ArchitecturePolicyContextTopologySelector? x, ArchitecturePolicyContextTopologySelector? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        int comparison = StringComparer.Ordinal.Compare(x.Kind, y.Kind);
        comparison = comparison != 0 ? comparison : StringComparer.Ordinal.Compare(x.Value, y.Value);
        comparison = comparison != 0 ? comparison : StringComparer.Ordinal.Compare(x.NamespaceSuffix, y.NamespaceSuffix);
        return comparison != 0 ? comparison : CompareContext(x.Context, y.Context);
    }

    public static string Describe(ArchitecturePolicyContextTopologySelector selector)
    {
        ArchitecturePolicyContextSelector? context = selector.Context;
        IReadOnlyDictionary<string, string>? metadata = context is null
            ? null
            : context.Metadata
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);

        return JsonSerializer.Serialize(new
        {
            selector.Kind,
            selector.Value,
            selector.NamespaceSuffix,
            Context = context is null ? null : new
            {
                context.Role,
                Metadata = metadata,
                context.When,
            },
        });
    }

    private static int CompareContext(ArchitecturePolicyContextSelector? x, ArchitecturePolicyContextSelector? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        int comparison = StringComparer.Ordinal.Compare(x.Role, y.Role);
        comparison = comparison != 0 ? comparison : CompareMetadata(x.Metadata, y.Metadata);
        return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(x.When, y.When);
    }

    private static int CompareMetadata(IReadOnlyDictionary<string, string> x, IReadOnlyDictionary<string, string> y)
    {
        using IEnumerator<KeyValuePair<string, string>> left = x.OrderBy(item => item.Key, StringComparer.Ordinal).GetEnumerator();
        using IEnumerator<KeyValuePair<string, string>> right = y.OrderBy(item => item.Key, StringComparer.Ordinal).GetEnumerator();
        while (true)
        {
            bool hasLeft = left.MoveNext();
            bool hasRight = right.MoveNext();
            if (!hasLeft || !hasRight)
            {
                return hasLeft.CompareTo(hasRight);
            }

            int comparison = StringComparer.Ordinal.Compare(left.Current.Key, right.Current.Key);
            comparison = comparison != 0 ? comparison : StringComparer.Ordinal.Compare(left.Current.Value, right.Current.Value);
            if (comparison != 0)
            {
                return comparison;
            }
        }
    }
}
