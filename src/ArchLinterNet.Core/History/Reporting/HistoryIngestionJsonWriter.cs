using ArchLinterNet.Core.History.Analysis;
using ArchLinterNet.Core.History.Tasks;

namespace ArchLinterNet.Core.History.Reporting;

// The minimal deterministic ingestion result #236 owns. It carries the provenance
// release-architecture-forensics declares mandatory; the versioned successful report schema stays
// owned by #243.
internal static class HistoryIngestionJsonWriter
{
    public static string Write(HistoryIngestionResult result)
    {
        CanonicalJsonWriter writer = new();
        writer.BeginObject();
        writer.WriteString("objectFormat", result.ObjectFormatName);
        writer.BeginObject("range");
        writer.WriteString("authoredFrom", result.AuthoredFrom);
        writer.WriteString("authoredTo", result.AuthoredTo);
        writer.WriteString("resolvedFrom", result.ResolvedFrom);
        writer.WriteString("resolvedTo", result.ResolvedTo);
        writer.EndObject();
        writer.WriteNumber("excludedMergeCount", result.ExcludedMergeCount);
        WriteCommits(writer, result);
        WriteRenameCandidates(writer, result);
        WriteRenameComponents(writer, result);
        WriteLogicalFiles(writer, result);
        writer.EndObject();
        return writer.ToCanonicalText() + "\n";
    }

    private static void WriteCommits(CanonicalJsonWriter writer, HistoryIngestionResult result)
    {
        writer.BeginArray("commits");
        foreach (CommitEvidence commit in result.Commits)
        {
            writer.BeginObject();
            writer.WriteString("id", commit.Commit.Id.Hex);
            writer.WriteIntegerText("committerEpochSecond", commit.Commit.Committer.EpochSecondText);
            writer.WriteString("committerTimezone", commit.Commit.Committer.TimezoneToken);
            writer.WriteString("author", commit.CanonicalAuthor);
            writer.WriteBoolean("isMerge", commit.Commit.IsMerge);
            writer.BeginArray("encodingHeaders");
            foreach (string encoding in commit.Commit.EncodingHeaderHex)
            {
                writer.WriteStringElement(encoding);
            }

            writer.EndArray();
            WriteTaskKeys(writer, commit);
            WriteTaskKeyMatches(writer, commit);
            writer.EndObject();
        }

        writer.EndArray();
    }

    private static void WriteTaskKeys(CanonicalJsonWriter writer, CommitEvidence commit)
    {
        writer.BeginArray("taskKeys");
        foreach (TaskKey key in commit.TaskKeys)
        {
            writer.BeginObject();
            writer.WriteString("namespace", key.Namespace);
            writer.WriteIntegerText("id", key.IdText);
            writer.EndObject();
        }

        writer.EndArray();
    }

    private static void WriteTaskKeyMatches(CanonicalJsonWriter writer, CommitEvidence commit)
    {
        writer.BeginArray("taskKeyMatches");
        foreach (TaskKeyMatch match in commit.TaskKeyMatches)
        {
            writer.BeginObject();
            writer.WriteString("extractorId", match.ExtractorId);
            writer.WriteString("namespace", match.Key.Namespace);
            writer.WriteIntegerText("id", match.Key.IdText);
            writer.WriteNumber("spanStart", match.SpanStart);
            writer.WriteNumber("spanEnd", match.SpanEnd);
            writer.WriteString("text", match.MatchedText);
            writer.EndObject();
        }

        writer.EndArray();
    }

    private static void WriteRenameCandidates(CanonicalJsonWriter writer, HistoryIngestionResult result)
    {
        writer.BeginArray("renameCandidates");
        foreach (RenameCandidate candidate in result.RenameCandidates)
        {
            writer.BeginObject();
            writer.WriteString("commitId", candidate.Commit.Id.Hex);
            writer.WriteString("sourcePath", candidate.SourcePath);
            writer.WriteString("destinationPath", candidate.DestinationPath);
            writer.WriteString("blobId", candidate.BlobId.Hex);
            writer.WriteNumber("componentIndex", candidate.ComponentIndex);
            writer.WriteString("status", candidate.Accepted ? "accepted" : "ambiguous_dag");
            writer.EndObject();
        }

        writer.EndArray();
    }

    private static void WriteRenameComponents(CanonicalJsonWriter writer, HistoryIngestionResult result)
    {
        writer.BeginArray("renameComponents");
        foreach (RenameComponent component in result.RenameComponents)
        {
            writer.BeginObject();
            writer.WriteNumber("index", component.Index);
            writer.WriteString("status", component.StatusText);
            writer.BeginArray("candidateIndexes");
            foreach (RenameCandidate candidate in component.Candidates)
            {
                writer.WriteNumberElement(IndexOf(result.RenameCandidates, candidate));
            }

            writer.EndArray();
            writer.EndObject();
        }

        writer.EndArray();
    }

    private static void WriteLogicalFiles(CanonicalJsonWriter writer, HistoryIngestionResult result)
    {
        writer.BeginArray("logicalFiles");
        foreach (LogicalFile file in result.LogicalFiles)
        {
            writer.BeginObject();
            writer.WriteString("canonicalPath", file.CanonicalPath);
            writer.BeginArray("aliases");
            foreach (string alias in file.Aliases)
            {
                writer.WriteStringElement(alias);
            }

            writer.EndArray();
            writer.WriteNumber("commitCount", file.CommitCount);
            writer.WriteNumber("additions", file.Additions);
            writer.WriteNumber("deletions", file.Deletions);
            writer.WriteNumber("churn", file.Churn);
            WriteEvents(writer, file);
            writer.EndObject();
        }

        writer.EndArray();
    }

    private static void WriteEvents(CanonicalJsonWriter writer, LogicalFile file)
    {
        writer.BeginArray("events");
        foreach (FileEvent fileEvent in file.Events)
        {
            writer.BeginObject();
            writer.WriteString("commitId", fileEvent.CommitId);
            writer.WriteString("kind", fileEvent.KindText);
            writer.WriteString("lineCountStatus", fileEvent.LineCountStatusText);
            writer.WriteNumber("additions", fileEvent.Additions);
            writer.WriteNumber("deletions", fileEvent.Deletions);
            writer.WriteString("oldPath", fileEvent.OldPath);
            writer.WriteString("newPath", fileEvent.NewPath);
            writer.EndObject();
        }

        writer.EndArray();
    }

    private static int IndexOf(IReadOnlyList<RenameCandidate> candidates, RenameCandidate candidate)
    {
        for (int index = 0; index < candidates.Count; index++)
        {
            if (ReferenceEquals(candidates[index], candidate))
            {
                return index;
            }
        }

        return -1;
    }
}
