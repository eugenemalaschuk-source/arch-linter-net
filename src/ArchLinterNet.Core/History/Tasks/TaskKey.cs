using System.Globalization;
using System.Numerics;
using ArchLinterNet.Core.History.Canonical;

namespace ArchLinterNet.Core.History.Tasks;

// Canonical task identity is structural, not textual: `#001` and `#1` are the same key, while
// `issue#1` and `jira#1` are not. The identifier is arbitrary precision and renders without leading
// zeroes, so no source spelling survives into evidence.
internal readonly struct TaskKey(string keyNamespace, BigInteger id) : IEquatable<TaskKey>, IComparable<TaskKey>
{
    public string Namespace { get; } = keyNamespace;

    public BigInteger Id { get; } = id;

    public string IdText => Id.ToString(CultureInfo.InvariantCulture);

    public bool Equals(TaskKey other)
        => string.Equals(Namespace, other.Namespace, StringComparison.Ordinal) && Id == other.Id;

    public override bool Equals(object? obj) => obj is TaskKey other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Namespace, Id);

    public int CompareTo(TaskKey other)
    {
        int byNamespace = HistoryScalarValueComparer.Compare(Namespace, other.Namespace);
        return byNamespace != 0 ? byNamespace : Id.CompareTo(other.Id);
    }

    public override string ToString() => $"{Namespace}#{IdText}";
}
