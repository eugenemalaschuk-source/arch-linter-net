namespace ArchLinterNet.Core.Model;

// One reviewed line of a public API snapshot: a normalized exported signature attributed to the
// assembly that exports it. Assembly attribution is part of the entry (not just a file section
// header) so a multi-assembly contract can still tell which assembly a delta belongs to.
public sealed record PublicApiSnapshotEntry(string AssemblyName, string Signature);

public sealed record PublicApiSnapshotDocument(
    int Version,
    string ContractId,
    IReadOnlyList<PublicApiSnapshotEntry> Entries);

public enum PublicApiDeltaKind
{
    Added,
    Removed,
    Changed
}

// PreviousSignature is set for Removed (the signature that disappeared) and Changed (the signature
// the reviewed snapshot recorded before the member was re-signed); it is null for Added.
public sealed record PublicApiDeltaEntry(
    PublicApiDeltaKind Kind,
    string AssemblyName,
    string Signature,
    string? PreviousSignature);

public sealed record PublicApiDelta(
    IReadOnlyList<PublicApiDeltaEntry> Added,
    IReadOnlyList<PublicApiDeltaEntry> Removed,
    IReadOnlyList<PublicApiDeltaEntry> Changed)
{
    public static PublicApiDelta Empty { get; } = new(
        Array.Empty<PublicApiDeltaEntry>(),
        Array.Empty<PublicApiDeltaEntry>(),
        Array.Empty<PublicApiDeltaEntry>());

    public bool HasChanges => Added.Count > 0 || Removed.Count > 0 || Changed.Count > 0;

    public IEnumerable<PublicApiDeltaEntry> All => Added.Concat(Removed).Concat(Changed);
}
