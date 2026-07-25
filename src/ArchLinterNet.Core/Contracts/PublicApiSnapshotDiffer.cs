using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Contracts;

// Computes the structured delta between a reviewed (declared) surface and the actual exported
// surface. Correlation runs per identity key so a re-signed member is one `changed` record rather
// than an unrelated removal plus addition — see PublicApiSignatureIdentity for why the key drops
// parameter/member types but keeps the parameter count.
public static class PublicApiSnapshotDiffer
{
    public static PublicApiDelta Diff(
        IReadOnlyCollection<PublicApiSnapshotEntry> declared,
        IReadOnlyCollection<PublicApiSnapshotEntry> actual)
    {
        ArgumentNullException.ThrowIfNull(declared);
        ArgumentNullException.ThrowIfNull(actual);

        HashSet<string> actualSignatures = new(actual.Select(entry => entry.Signature), StringComparer.Ordinal);
        HashSet<string> declaredSignatures = new(declared.Select(entry => entry.Signature), StringComparer.Ordinal);

        List<PublicApiSnapshotEntry> unmatchedDeclared = Distinct(declared)
            .Where(entry => !actualSignatures.Contains(entry.Signature))
            .ToList();
        List<PublicApiSnapshotEntry> unmatchedActual = Distinct(actual)
            .Where(entry => !declaredSignatures.Contains(entry.Signature))
            .ToList();

        List<PublicApiDeltaEntry> added = new();
        List<PublicApiDeltaEntry> removed = new();
        List<PublicApiDeltaEntry> changed = new();

        Dictionary<string, List<PublicApiSnapshotEntry>> declaredByIdentity = GroupByIdentity(unmatchedDeclared);
        Dictionary<string, List<PublicApiSnapshotEntry>> actualByIdentity = GroupByIdentity(unmatchedActual);

        foreach (string identity in declaredByIdentity.Keys.Concat(actualByIdentity.Keys)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(identity => identity, StringComparer.Ordinal))
        {
            List<PublicApiSnapshotEntry> declaredGroup = Group(declaredByIdentity, identity);
            List<PublicApiSnapshotEntry> actualGroup = Group(actualByIdentity, identity);

            // Positional pairing inside an identity group: both sides are already in ordinal
            // signature order, so the pairing is stable regardless of input order.
            int pairs = Math.Min(declaredGroup.Count, actualGroup.Count);
            for (int i = 0; i < pairs; i++)
            {
                changed.Add(new PublicApiDeltaEntry(
                    PublicApiDeltaKind.Changed,
                    actualGroup[i].AssemblyName,
                    actualGroup[i].Signature,
                    declaredGroup[i].Signature));
            }

            for (int i = pairs; i < declaredGroup.Count; i++)
            {
                removed.Add(new PublicApiDeltaEntry(
                    PublicApiDeltaKind.Removed,
                    declaredGroup[i].AssemblyName,
                    declaredGroup[i].Signature,
                    declaredGroup[i].Signature));
            }

            for (int i = pairs; i < actualGroup.Count; i++)
            {
                added.Add(new PublicApiDeltaEntry(
                    PublicApiDeltaKind.Added,
                    actualGroup[i].AssemblyName,
                    actualGroup[i].Signature,
                    null));
            }
        }

        return new PublicApiDelta(Order(added), Order(removed), Order(changed));
    }

    private static List<PublicApiSnapshotEntry> Group(
        Dictionary<string, List<PublicApiSnapshotEntry>> source, string identity)
    {
        return source.TryGetValue(identity, out List<PublicApiSnapshotEntry>? group)
            ? group
            : new List<PublicApiSnapshotEntry>();
    }

    private static Dictionary<string, List<PublicApiSnapshotEntry>> GroupByIdentity(
        IEnumerable<PublicApiSnapshotEntry> entries)
    {
        return entries
            .GroupBy(entry => PublicApiSignatureIdentity.Compute(entry.Signature), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(entry => entry.AssemblyName, StringComparer.Ordinal)
                    .ThenBy(entry => entry.Signature, StringComparer.Ordinal)
                    .ToList(),
                StringComparer.Ordinal);
    }

    private static IEnumerable<PublicApiSnapshotEntry> Distinct(IEnumerable<PublicApiSnapshotEntry> entries)
    {
        return entries
            .GroupBy(entry => (entry.AssemblyName, entry.Signature))
            .Select(group => group.First());
    }

    private static IReadOnlyList<PublicApiDeltaEntry> Order(List<PublicApiDeltaEntry> entries)
    {
        return entries
            .OrderBy(entry => entry.AssemblyName, StringComparer.Ordinal)
            .ThenBy(entry => entry.Signature, StringComparer.Ordinal)
            .ToArray();
    }
}
