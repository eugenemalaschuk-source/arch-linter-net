using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Contracts;

// Computes the structured delta between a reviewed (declared) surface and the actual exported
// surface.
//
// Two rules carry all the subtlety:
//
// 1. Assembly identity is part of the key. Two assemblies may legitimately export the same fully
//    qualified signature, and the 0.5.1 compatibility contract requires them to stay distinct —
//    otherwise removing an API from one assembly is masked by the copy in the other, or the two get
//    paired into a nonsense cross-assembly `changed` record.
// 2. A declared entry with an empty assembly name is a *wildcard*, not an assembly named "". That is
//    how legacy inline `declared_api` entries enter here: the policy never stated an assembly, so
//    they match any assembly rather than silently failing to match every one of them.
public static class PublicApiSnapshotDiffer
{
    public const string WildcardAssembly = "";

    public static PublicApiDelta Diff(
        IReadOnlyCollection<PublicApiSnapshotEntry> declared,
        IReadOnlyCollection<PublicApiSnapshotEntry> actual)
    {
        ArgumentNullException.ThrowIfNull(declared);
        ArgumentNullException.ThrowIfNull(actual);

        List<PublicApiSnapshotEntry> distinctDeclared = Distinct(declared);
        List<PublicApiSnapshotEntry> distinctActual = Distinct(actual);

        HashSet<(string Assembly, string Signature)> actualKeys = new(
            distinctActual.Select(entry => (entry.AssemblyName, entry.Signature)));
        HashSet<string> actualSignatures = new(distinctActual.Select(entry => entry.Signature), StringComparer.Ordinal);

        List<PublicApiSnapshotEntry> unmatchedDeclared = distinctDeclared
            .Where(entry => !MatchesActual(entry, actualKeys, actualSignatures))
            .ToList();

        HashSet<(string Assembly, string Signature)> declaredKeys = new(
            distinctDeclared.Select(entry => (entry.AssemblyName, entry.Signature)));
        HashSet<string> wildcardDeclaredSignatures = new(
            distinctDeclared.Where(IsWildcard).Select(entry => entry.Signature), StringComparer.Ordinal);

        List<PublicApiSnapshotEntry> unmatchedActual = distinctActual
            .Where(entry => !declaredKeys.Contains((entry.AssemblyName, entry.Signature))
                && !wildcardDeclaredSignatures.Contains(entry.Signature))
            .ToList();

        List<PublicApiDeltaEntry> added = new();
        List<PublicApiDeltaEntry> removed = new();
        List<PublicApiDeltaEntry> changed = new();

        Dictionary<(string, string), List<PublicApiSnapshotEntry>> actualByIdentity = GroupByIdentity(unmatchedActual);
        Dictionary<(string, string), List<PublicApiSnapshotEntry>> declaredByIdentity =
            GroupByIdentity(BindWildcards(unmatchedDeclared, actualByIdentity));

        foreach ((string Assembly, string Identity) key in declaredByIdentity.Keys.Concat(actualByIdentity.Keys)
                     .Distinct()
                     .OrderBy(key => key.Item1, StringComparer.Ordinal)
                     .ThenBy(key => key.Item2, StringComparer.Ordinal))
        {
            List<PublicApiSnapshotEntry> declaredGroup = Group(declaredByIdentity, key);
            List<PublicApiSnapshotEntry> actualGroup = Group(actualByIdentity, key);

            // Positional pairing inside one (assembly, identity) group: both sides are already in
            // ordinal signature order, so the pairing is stable regardless of input order.
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

    private static bool IsWildcard(PublicApiSnapshotEntry entry)
    {
        return entry.AssemblyName.Length == 0;
    }

    private static bool MatchesActual(
        PublicApiSnapshotEntry declared,
        HashSet<(string Assembly, string Signature)> actualKeys,
        HashSet<string> actualSignatures)
    {
        return IsWildcard(declared)
            ? actualSignatures.Contains(declared.Signature)
            : actualKeys.Contains((declared.AssemblyName, declared.Signature));
    }

    // A wildcard declaration has no assembly of its own, so it cannot be grouped against an
    // assembly-qualified actual entry. Bind it to the ordinal-first assembly that exports the same
    // signature identity; if nothing does, it stays a wildcard and is reported as an unattributed
    // removal, which is the honest outcome for an inline entry the policy never attributed.
    private static List<PublicApiSnapshotEntry> BindWildcards(
        List<PublicApiSnapshotEntry> declared,
        Dictionary<(string, string), List<PublicApiSnapshotEntry>> actualByIdentity)
    {
        List<PublicApiSnapshotEntry> bound = new(declared.Count);

        foreach (PublicApiSnapshotEntry entry in declared)
        {
            if (!IsWildcard(entry))
            {
                bound.Add(entry);
                continue;
            }

            string identity = PublicApiSignatureIdentity.Compute(entry.Signature);
            string? assembly = actualByIdentity.Keys
                .Where(key => string.Equals(key.Item2, identity, StringComparison.Ordinal))
                .Select(key => key.Item1)
                .OrderBy(name => name, StringComparer.Ordinal)
                .FirstOrDefault();

            bound.Add(assembly == null ? entry : entry with { AssemblyName = assembly });
        }

        return bound;
    }

    private static List<PublicApiSnapshotEntry> Group(
        Dictionary<(string, string), List<PublicApiSnapshotEntry>> source, (string, string) key)
    {
        return source.TryGetValue(key, out List<PublicApiSnapshotEntry>? group)
            ? group
            : new List<PublicApiSnapshotEntry>();
    }

    private static Dictionary<(string, string), List<PublicApiSnapshotEntry>> GroupByIdentity(
        IEnumerable<PublicApiSnapshotEntry> entries)
    {
        return entries
            .GroupBy(entry => (entry.AssemblyName, PublicApiSignatureIdentity.Compute(entry.Signature)))
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(entry => entry.Signature, StringComparer.Ordinal)
                    .ToList());
    }

    private static List<PublicApiSnapshotEntry> Distinct(IEnumerable<PublicApiSnapshotEntry> entries)
    {
        return entries
            .GroupBy(entry => (entry.AssemblyName, entry.Signature))
            .Select(group => group.First())
            .ToList();
    }

    private static PublicApiDeltaEntry[] Order(List<PublicApiDeltaEntry> entries)
    {
        return entries
            .OrderBy(entry => entry.AssemblyName, StringComparer.Ordinal)
            .ThenBy(entry => entry.Signature, StringComparer.Ordinal)
            .ToArray();
    }
}
