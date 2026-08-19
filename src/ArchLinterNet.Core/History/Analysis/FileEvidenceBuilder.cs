using ArchLinterNet.Core.History.Git;

namespace ArchLinterNet.Core.History.Analysis;

// Turns raw per-commit deltas plus resolved rename lineages into canonical file events and logical
// files. Required blob objects are loaded directly; a missing one fails closed rather than being
// treated as zero churn.
internal sealed class FileEvidenceBuilder(GitObjectDatabase objects, LogicalFileIdentity identity)
{
    public IReadOnlyList<LogicalFile> Build(
        IReadOnlyList<CommitDelta> deltas,
        IReadOnlyList<RenameComponent> components)
    {
        Dictionary<string, List<RenameCandidate>> acceptedByCommit = AcceptedByCommit(components);
        Dictionary<string, int> firstOccurrence = new(StringComparer.Ordinal);
        Dictionary<int, List<FileEvent>> eventsByGroup = [];
        for (int order = 0; order < deltas.Count; order++)
        {
            CollectCommit(deltas[order], order, acceptedByCommit, firstOccurrence, eventsByGroup);
        }

        List<LogicalFile> files = [];
        foreach (int group in identity.Groups())
        {
            if (!eventsByGroup.TryGetValue(group, out List<FileEvent>? events) || events.Count == 0)
            {
                continue;
            }

            files.Add(new LogicalFile(identity.CanonicalPathOf(group), identity.AliasesOf(group, firstOccurrence), events));
        }

        files.Sort(static (left, right) => GitPathDecoder.CompareScalarValue(left.CanonicalPath, right.CanonicalPath));
        return files;
    }

    private void CollectCommit(
        CommitDelta delta,
        int order,
        IReadOnlyDictionary<string, List<RenameCandidate>> acceptedByCommit,
        Dictionary<string, int> firstOccurrence,
        Dictionary<int, List<FileEvent>> eventsByGroup)
    {
        Dictionary<int, FileEventAccumulator> accumulators = [];
        HashSet<string> collapsed = new(StringComparer.Ordinal);
        if (acceptedByCommit.TryGetValue(delta.Commit.Id.Hex, out List<RenameCandidate>? accepted))
        {
            foreach (RenameCandidate candidate in accepted)
            {
                collapsed.Add(candidate.SourcePath);
                collapsed.Add(candidate.DestinationPath);
                Accumulator(accumulators, identity.GroupOf(candidate.DestinationPath))
                    .MarkRename(candidate.SourcePath, candidate.DestinationPath);
            }
        }

        foreach (GitTreeChange change in delta.Changes)
        {
            RecordFirstOccurrence(firstOccurrence, change.Path, order);
            if (collapsed.Contains(change.Path))
            {
                continue;
            }

            (LineCountStatus status, long additions, long deletions) = Count(change);
            Accumulator(accumulators, identity.GroupOf(change.Path)).AddOrdinary(ToKind(change.Kind), status, additions, deletions);
        }

        foreach ((int group, FileEventAccumulator accumulator) in accumulators)
        {
            if (!eventsByGroup.TryGetValue(group, out List<FileEvent>? events))
            {
                events = [];
                eventsByGroup[group] = events;
            }

            events.Add(accumulator.ToEvent(delta.Commit.Id.Hex));
        }
    }

    private (LineCountStatus Status, long Additions, long Deletions) Count(GitTreeChange change)
    {
        bool oldApplicable = change.OldMode is null || change.OldIsBlob;
        bool newApplicable = change.NewMode is null || change.NewIsBlob;
        if (!oldApplicable || !newApplicable)
        {
            return (LineCountStatus.BinaryOrUnavailable, 0, 0);
        }

        byte[] oldContent = ReadBlob(change.OldId);
        byte[] newContent = ReadBlob(change.NewId);
        if (LineChurnCalculator.ContainsNul(oldContent) || LineChurnCalculator.ContainsNul(newContent))
        {
            return (LineCountStatus.BinaryOrUnavailable, 0, 0);
        }

        (long additions, long deletions) = LineChurnCalculator.Compute(oldContent, newContent);
        return (LineCountStatus.Text, additions, deletions);
    }

    // An absent add/delete side is the empty byte sequence, never a skipped object read.
    private byte[] ReadBlob(GitObjectId id) => id.IsEmpty ? [] : objects.ReadOfKind(id, GitObjectKind.Blob).Payload;

    private static Dictionary<string, List<RenameCandidate>> AcceptedByCommit(IReadOnlyList<RenameComponent> components)
    {
        Dictionary<string, List<RenameCandidate>> accepted = new(StringComparer.Ordinal);
        foreach (RenameComponent component in components.Where(static component => component.Accepted))
        {
            foreach (RenameCandidate candidate in component.AcceptedSequence)
            {
                if (!accepted.TryGetValue(candidate.Commit.Id.Hex, out List<RenameCandidate>? bucket))
                {
                    bucket = [];
                    accepted[candidate.Commit.Id.Hex] = bucket;
                }

                bucket.Add(candidate);
            }
        }

        return accepted;
    }

    private static FileEventAccumulator Accumulator(Dictionary<int, FileEventAccumulator> accumulators, int group)
    {
        if (!accumulators.TryGetValue(group, out FileEventAccumulator? accumulator))
        {
            accumulator = new FileEventAccumulator();
            accumulators[group] = accumulator;
        }

        return accumulator;
    }

    private static void RecordFirstOccurrence(Dictionary<string, int> firstOccurrence, string path, int order)
        => firstOccurrence.TryAdd(path, order);

    private static FileEventKind ToKind(GitTreeChangeKind kind) => kind switch
    {
        GitTreeChangeKind.Add => FileEventKind.Add,
        GitTreeChangeKind.Delete => FileEventKind.Delete,
        _ => FileEventKind.Modify,
    };
}
