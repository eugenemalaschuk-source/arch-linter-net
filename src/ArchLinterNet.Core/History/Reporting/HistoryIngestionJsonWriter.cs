using ArchLinterNet.Core.History.Analysis;
using ArchLinterNet.Core.History.Canonical;
using ArchLinterNet.Core.History.Configuration;
using ArchLinterNet.Core.History.Tasks;

namespace ArchLinterNet.Core.History.Reporting;

// The versioned successful report is a read-only projection of finalized canonical evidence. It
// neither reads Git/policy input nor recalculates a finding, preserving the fail-closed boundary.
internal static class HistoryIngestionJsonWriter
{
    private const string ReportKind = "release-architecture-forensics";
    private const string HistorySemanticsVersion = "v1";
    private static readonly IComparer<string> _scalarStringComparer = HistoryScalarValueComparer.Instance;

    public static string Write(HistoryIngestionResult result)
    {
        CanonicalJsonWriter writer = new();
        writer.BeginObject();
        writer.WriteNumber("schemaVersion", 1);
        writer.WriteString("kind", ReportKind);
        writer.WriteString("historySemanticsVersion", HistorySemanticsVersion);
        writer.WriteString("toolVersion", ToolVersion());
        WriteAnalysis(writer, result);
        WriteCommits(writer, result);
        WriteRenameCandidates(writer, result);
        WriteRenameComponents(writer, result);
        WriteLogicalFiles(writer, result);
        WriteHotspotAnalysis(writer, result.HotspotAnalysis);
        WriteCoChangeGraph(writer, result);
        WriteBottleneckAnalysis(writer, result.BottleneckAnalysis);
        WriteOcpAnalysis(writer, result.OcpAnalysis);
        HistoryReportEnrichmentWriter.Write(writer, result.Enrichment);
        HistoryReportCandidateWriter.Write(writer, result);
        writer.EndObject();
        return writer.ToCanonicalText() + "\n";
    }

    private static void WriteAnalysis(CanonicalJsonWriter writer, HistoryIngestionResult result)
    {
        writer.BeginObject("analysis");
        writer.WriteString("objectFormat", result.ObjectFormatName);
        writer.BeginObject("range");
        writer.WriteString("authoredFrom", result.AuthoredFrom);
        writer.WriteString("authoredTo", result.AuthoredTo);
        writer.WriteString("resolvedFrom", result.ResolvedFrom);
        writer.WriteString("resolvedTo", result.ResolvedTo);
        writer.EndObject();
        writer.WriteNumber("analyzedCommitCount", result.Commits.Count);
        writer.WriteNumber("excludedMergeCount", result.ExcludedMergeCount);
        WriteConfiguration(writer, result.Configuration);
        writer.EndObject();
    }

    private static void WriteConfiguration(CanonicalJsonWriter writer, Contracts.HistoryAnalysisConfiguration configuration)
    {
        writer.BeginObject("historyAnalysisConfiguration");
        writer.BeginArray("builtInExtractors");
        writer.WriteStringElement("issue");
        writer.EndArray();
        writer.BeginArray("extractors");
        foreach (Contracts.HistoryTaskExtractorConfiguration extractor in configuration.Extractors.OrderBy(static item => item.Id, _scalarStringComparer))
        {
            writer.BeginObject();
            writer.WriteString("id", extractor.Id);
            writer.WriteString("namespace", extractor.Namespace);
            writer.BeginObject("pattern");
            writer.WriteString("prefix", extractor.Pattern.Prefix);
            writer.WriteString("suffix", extractor.Pattern.Suffix);
            writer.EndObject();
            writer.EndObject();
        }

        writer.EndArray();
        writer.BeginObject("paths");
        WriteSortedStrings(writer, "production", configuration.Paths.Production);
        WriteSortedStrings(writer, "tests", configuration.Paths.Tests);
        WriteSortedStrings(writer, "docs", configuration.Paths.Docs);
        WriteSortedStrings(writer, "generated", configuration.Paths.Generated);
        WriteSortedStrings(writer, "buildCi", configuration.Paths.BuildCi);
        WriteSortedStrings(writer, "samplesExamples", configuration.Paths.SamplesExamples);
        writer.EndObject();
        WriteSortedStrings(writer, "ignore", configuration.Ignore);
        writer.BeginObject("weights");
        writer.BeginObject("hotspot");
        writer.WriteCanonicalDecimal("commit", configuration.Weights.Hotspot.Commit);
        writer.WriteCanonicalDecimal("churn", configuration.Weights.Hotspot.Churn);
        writer.WriteCanonicalDecimal("task", configuration.Weights.Hotspot.Task);
        writer.WriteCanonicalDecimal("author", configuration.Weights.Hotspot.Author);
        writer.WriteCanonicalDecimal("temporal", configuration.Weights.Hotspot.Temporal);
        writer.EndObject();
        writer.BeginObject("coChange");
        writer.WriteCanonicalDecimal("commit", configuration.Weights.CoChange.Commit);
        writer.WriteCanonicalDecimal("task", configuration.Weights.CoChange.Task);
        writer.EndObject();
        writer.BeginObject("bottleneck");
        writer.WriteCanonicalDecimal("independentTask", configuration.Weights.Bottleneck.IndependentTask);
        writer.WriteCanonicalDecimal("author", configuration.Weights.Bottleneck.Author);
        writer.WriteCanonicalDecimal("temporal", configuration.Weights.Bottleneck.Temporal);
        writer.WriteCanonicalDecimal("degree", configuration.Weights.Bottleneck.Degree);
        writer.WriteCanonicalDecimal("centrality", configuration.Weights.Bottleneck.Centrality);
        writer.EndObject();
        writer.BeginObject("ocp");
        writer.WriteCanonicalDecimal("independentTask", configuration.Weights.Ocp.IndependentTask);
        writer.WriteCanonicalDecimal("centrality", configuration.Weights.Ocp.Centrality);
        writer.WriteCanonicalDecimal("repeatedEdit", configuration.Weights.Ocp.RepeatedEdit);
        writer.WriteCanonicalDecimal("roleHint", configuration.Weights.Ocp.RoleHint);
        writer.EndObject();
        writer.EndObject();
        writer.BeginObject("thresholds");
        writer.WriteOptionalCanonicalDecimal("coChangeSignificance", configuration.Thresholds.CoChangeSignificance);
        writer.EndObject();
        writer.EndObject();
    }

    private static void WriteSortedStrings(CanonicalJsonWriter writer, string propertyName, IEnumerable<string> values)
    {
        writer.BeginArray(propertyName);
        foreach (string value in values.OrderBy(static item => item, _scalarStringComparer))
        {
            writer.WriteStringElement(value);
        }

        writer.EndArray();
    }

    private static string ToolVersion() => typeof(HistoryIngestionJsonWriter).Assembly.GetName().Version?.ToString(3) ?? "unknown";

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

    private static void WriteHotspotAnalysis(CanonicalJsonWriter writer, HistoryHotspotAnalysis analysis)
    {
        writer.BeginArray("hotspotGroups");
        foreach (HotspotCategoryGroup group in analysis.Groups)
        {
            writer.BeginObject();
            writer.WriteString("category", CategoryText(group.Category));
            writer.BeginArray("findings");
            foreach (HotspotFinding finding in group.Findings)
            {
                WriteHotspotFinding(writer, finding);
            }

            writer.EndArray();
            writer.EndObject();
        }

        writer.EndArray();
    }

    private static void WriteHotspotFinding(CanonicalJsonWriter writer, HotspotFinding finding)
    {
        writer.BeginObject();
        writer.WriteString("id", FindingId("hotspot", finding.Category, finding.CanonicalPath));
        writer.WriteString("canonicalPath", finding.CanonicalPath);
        WriteStringArray(writer, "aliases", finding.Aliases);
        writer.WriteBoolean("pathnameReuseMayConflateGenerations", finding.RawEvidence.PathnameReuseMayConflateGenerations);
        writer.WriteNumber("commitCount", finding.RawEvidence.CommitCount);
        writer.WriteNumber("churn", finding.RawEvidence.Churn);
        writer.WriteNumber("taskSpread", finding.RawEvidence.TaskSpread);
        writer.WriteNumber("authorSpread", finding.RawEvidence.AuthorSpread);
        writer.WriteIntegerText("temporalSpanSeconds", finding.RawEvidence.TemporalSpanSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        writer.BeginArray("lineCountStatuses");
        foreach (LineCountStatus status in finding.RawEvidence.LineCountStatuses)
        {
            writer.WriteStringElement(LineCountStatusText(status));
        }

        writer.EndArray();
        writer.BeginObject("components");
        writer.WriteCanonicalDecimal("commit", finding.Components.Commit);
        writer.WriteCanonicalDecimal("churn", finding.Components.Churn);
        writer.WriteCanonicalDecimal("task", finding.Components.Task);
        writer.WriteCanonicalDecimal("author", finding.Components.Author);
        writer.WriteCanonicalDecimal("temporal", finding.Components.Temporal);
        writer.EndObject();
        writer.BeginObject("weights");
        writer.WriteCanonicalDecimal("commit", finding.Weights.Commit);
        writer.WriteCanonicalDecimal("churn", finding.Weights.Churn);
        writer.WriteCanonicalDecimal("task", finding.Weights.Task);
        writer.WriteCanonicalDecimal("author", finding.Weights.Author);
        writer.WriteCanonicalDecimal("temporal", finding.Weights.Temporal);
        writer.EndObject();
        writer.WriteCanonicalDecimal("score", finding.Score);
        writer.BeginArray("taskKeys");
        foreach (TaskKey key in finding.RawEvidence.TaskKeys)
        {
            WriteTaskKey(writer, key);
        }

        writer.EndArray();
        writer.BeginArray("taskKeyProvenance");
        foreach (HotspotTaskKeyProvenance item in finding.RawEvidence.TaskKeyProvenance)
        {
            writer.BeginObject();
            writer.WriteString("commitId", item.CommitId);
            writer.WriteString("extractorId", item.Match.ExtractorId);
            WriteTaskKey(writer, "task", item.Match.Key);
            writer.WriteNumber("spanStart", item.Match.SpanStart);
            writer.WriteNumber("spanEnd", item.Match.SpanEnd);
            writer.WriteString("text", item.Match.MatchedText);
            writer.EndObject();
        }

        writer.EndArray();
        WriteStringArray(writer, "canonicalAuthors", finding.RawEvidence.CanonicalAuthors);
        writer.BeginArray("authorProvenance");
        foreach (HotspotAuthorProvenance item in finding.RawEvidence.AuthorProvenance)
        {
            writer.BeginObject();
            writer.WriteString("commitId", item.CommitId);
            writer.WriteString("author", item.CanonicalAuthor);
            writer.EndObject();
        }

        writer.EndArray();
        writer.EndObject();
    }

    private static void WriteCoChangeGraph(CanonicalJsonWriter writer, HistoryIngestionResult result)
    {
        CoChangeGraph graph = result.CoChangeGraph;
        writer.BeginObject("coChangeGraph");
        writer.BeginObject("weights");
        writer.WriteCanonicalDecimal("commit", graph.CommitWeight);
        writer.WriteCanonicalDecimal("task", graph.TaskWeight);
        writer.EndObject();
        writer.WriteOptionalCanonicalDecimal("significanceThreshold", graph.SignificanceThreshold);
        WriteVertices(writer, result, graph);
        WritePairs(writer, graph);
        WriteClusters(writer, graph);
        writer.EndObject();
    }

    private static void WriteBottleneckAnalysis(CanonicalJsonWriter writer, HistoryBottleneckAnalysis analysis)
    {
        writer.BeginArray("bottleneckGroups");
        foreach (HistoryBottleneckCategoryGroup group in analysis.Groups)
        {
            writer.BeginObject();
            writer.WriteString("category", CategoryText(group.Category));
            writer.BeginArray("findings");
            foreach (HistoryBottleneckFinding finding in group.Findings)
            {
                WriteBottleneckFinding(writer, finding);
            }

            writer.EndArray();
            writer.EndObject();
        }

        writer.EndArray();
    }

    private static void WriteOcpAnalysis(CanonicalJsonWriter writer, HistoryOcpAnalysis analysis)
    {
        writer.BeginArray("ocpGroups");
        foreach (HistoryOcpCategoryGroup group in analysis.Groups)
        {
            writer.BeginObject();
            writer.WriteString("category", CategoryText(group.Category));
            writer.BeginArray("findings");
            foreach (HistoryOcpFinding finding in group.Findings)
            {
                WriteOcpFinding(writer, finding);
            }

            writer.EndArray();
            writer.EndObject();
        }

        writer.EndArray();
    }

    private static void WriteOcpFinding(CanonicalJsonWriter writer, HistoryOcpFinding finding)
    {
        writer.BeginObject();
        writer.WriteString("id", FindingId("ocp-pressure", finding.Category, finding.CanonicalPath));
        writer.WriteString("canonicalPath", finding.CanonicalPath);
        WriteStringArray(writer, "aliases", finding.Aliases);
        writer.WriteBoolean("pathnameReuseMayConflateGenerations", finding.RawEvidence.PathnameReuseMayConflateGenerations);
        writer.WriteNumber("independentTaskSpread", finding.RawEvidence.IndependentTaskSpread);
        writer.WriteNumber("incidentCommitDegree", finding.RawEvidence.IncidentCommitDegree);
        writer.WriteNumber("incidentTaskDegree", finding.RawEvidence.IncidentTaskDegree);
        writer.WriteNumber("repeatedEditTotal", finding.RawEvidence.RepeatedEditTotal);
        writer.WriteCanonicalDecimal("roleHint", finding.RawEvidence.RoleHint);
        writer.BeginObject("components");
        writer.WriteCanonicalDecimal("independentTask", finding.Components.IndependentTask);
        writer.WriteCanonicalDecimal("centrality", finding.Components.Centrality);
        writer.WriteCanonicalDecimal("repeatedEdit", finding.Components.RepeatedEdit);
        writer.WriteCanonicalDecimal("roleHint", finding.Components.RoleHint);
        writer.EndObject();
        writer.BeginObject("weights");
        writer.WriteCanonicalDecimal("independentTask", finding.Weights.IndependentTask);
        writer.WriteCanonicalDecimal("centrality", finding.Weights.Centrality);
        writer.WriteCanonicalDecimal("repeatedEdit", finding.Weights.RepeatedEdit);
        writer.WriteCanonicalDecimal("roleHint", finding.Weights.RoleHint);
        writer.EndObject();
        writer.WriteCanonicalDecimal("score", finding.Score);
        writer.BeginArray("taskKeys");
        foreach (TaskKey key in finding.RawEvidence.TaskKeys)
        {
            WriteTaskKey(writer, key);
        }

        writer.EndArray();
        writer.BeginArray("independentTaskPairs");
        foreach (BottleneckTaskPair pair in finding.RawEvidence.IndependentTaskPairs)
        {
            WriteBottleneckPair(writer, pair);
        }

        writer.EndArray();
        writer.BeginArray("repeatedEdits");
        foreach (OcpTaskRepeatedEdit repeated in finding.RawEvidence.RepeatedEdits)
        {
            writer.BeginObject();
            WriteTaskKey(writer, "task", repeated.TaskKey);
            WriteStringArray(writer, "qualifyingCommitIds", repeated.QualifyingCommitIds);
            writer.WriteNumber("repeatedEditCount", repeated.RepeatedEditCount);
            writer.EndObject();
        }

        writer.EndArray();
        WriteStringArray(writer, "roleTokens", finding.RawEvidence.RoleTokens);
        writer.EndObject();
    }

    private static void WriteBottleneckFinding(CanonicalJsonWriter writer, HistoryBottleneckFinding finding)
    {
        writer.BeginObject();
        writer.WriteString("id", FindingId("bottleneck", finding.Category, finding.CanonicalPath));
        writer.WriteString("canonicalPath", finding.CanonicalPath);
        writer.BeginArray("aliases");
        foreach (string alias in finding.Aliases)
        {
            writer.WriteStringElement(alias);
        }

        writer.EndArray();
        writer.WriteBoolean("pathnameReuseMayConflateGenerations", finding.RawEvidence.PathnameReuseMayConflateGenerations);
        writer.WriteNumber("independentTaskSpread", finding.RawEvidence.IndependentTaskSpread);
        writer.WriteNumber("distinctAuthorCount", finding.RawEvidence.DistinctAuthorCount);
        writer.WriteCanonicalDecimal("independentTemporalProximity", finding.RawEvidence.IndependentTemporalProximity);
        writer.WriteNumber("distinctNeighborDegree", finding.RawEvidence.DistinctNeighborDegree);
        writer.WriteNumber("incidentCommitDegree", finding.RawEvidence.IncidentCommitDegree);
        writer.WriteNumber("incidentTaskDegree", finding.RawEvidence.IncidentTaskDegree);
        WriteBottleneckComponents(writer, finding.Components);
        WriteBottleneckWeights(writer, finding.Weights);
        writer.WriteCanonicalDecimal("score", finding.Score);
        writer.BeginArray("canonicalAuthors");
        foreach (string author in finding.RawEvidence.CanonicalAuthors)
        {
            writer.WriteStringElement(author);
        }

        writer.EndArray();
        writer.BeginArray("taskKeys");
        foreach (TaskKey key in finding.RawEvidence.TaskKeys)
        {
            WriteTaskKey(writer, key);
        }

        writer.EndArray();
        writer.BeginArray("independentTaskPairs");
        foreach (BottleneckTaskPair pair in finding.RawEvidence.IndependentTaskPairs)
        {
            WriteBottleneckPair(writer, pair);
        }

        writer.EndArray();
        writer.EndObject();
    }

    private static void WriteBottleneckComponents(CanonicalJsonWriter writer, BottleneckComponents components)
    {
        writer.BeginObject("components");
        writer.WriteCanonicalDecimal("independentTask", components.IndependentTask);
        writer.WriteCanonicalDecimal("author", components.Author);
        writer.WriteCanonicalDecimal("temporal", components.Temporal);
        writer.WriteCanonicalDecimal("degree", components.Degree);
        writer.WriteCanonicalDecimal("incidentCommit", components.IncidentCommit);
        writer.WriteCanonicalDecimal("incidentTask", components.IncidentTask);
        writer.WriteCanonicalDecimal("centrality", components.Centrality);
        writer.EndObject();
    }

    private static void WriteBottleneckWeights(CanonicalJsonWriter writer, BottleneckWeights weights)
    {
        writer.BeginObject("weights");
        writer.WriteCanonicalDecimal("independentTask", weights.IndependentTask);
        writer.WriteCanonicalDecimal("author", weights.Author);
        writer.WriteCanonicalDecimal("temporal", weights.Temporal);
        writer.WriteCanonicalDecimal("degree", weights.Degree);
        writer.WriteCanonicalDecimal("centrality", weights.Centrality);
        writer.EndObject();
    }

    private static void WriteBottleneckPair(CanonicalJsonWriter writer, BottleneckTaskPair pair)
    {
        writer.BeginObject();
        WriteTaskKey(writer, "firstTask", pair.First);
        WriteTaskKey(writer, "secondTask", pair.Second);
        WriteBottleneckInterval(writer, "firstInterval", pair.FirstInterval);
        WriteBottleneckInterval(writer, "secondInterval", pair.SecondInterval);
        writer.WriteIntegerText("gapSeconds", pair.GapSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        writer.WriteIntegerText("daysBetween", pair.DaysBetween.ToString(System.Globalization.CultureInfo.InvariantCulture));
        writer.WriteCanonicalDecimal("temporalProximity", pair.TemporalProximity);
        WriteStringArray(writer, "firstExclusiveCommitIds", pair.FirstExclusiveCommitIds);
        WriteStringArray(writer, "secondExclusiveCommitIds", pair.SecondExclusiveCommitIds);
        WriteBottleneckProvenance(writer, "firstProvenance", pair.FirstProvenance);
        WriteBottleneckProvenance(writer, "secondProvenance", pair.SecondProvenance);
        writer.EndObject();
    }

    private static void WriteTaskKey(CanonicalJsonWriter writer, TaskKey key)
    {
        writer.BeginObject();
        writer.WriteString("namespace", key.Namespace);
        writer.WriteIntegerText("id", key.IdText);
        writer.EndObject();
    }

    private static void WriteTaskKey(CanonicalJsonWriter writer, string propertyName, TaskKey key)
    {
        writer.BeginObject(propertyName);
        writer.WriteString("namespace", key.Namespace);
        writer.WriteIntegerText("id", key.IdText);
        writer.EndObject();
    }

    private static void WriteBottleneckInterval(CanonicalJsonWriter writer, string propertyName, BottleneckTaskInterval interval)
    {
        writer.BeginObject(propertyName);
        writer.WriteIntegerText("startEpochSecond", interval.StartEpochSecond.ToString(System.Globalization.CultureInfo.InvariantCulture));
        writer.WriteIntegerText("endEpochSecond", interval.EndEpochSecond.ToString(System.Globalization.CultureInfo.InvariantCulture));
        writer.EndObject();
    }

    private static void WriteStringArray(CanonicalJsonWriter writer, string propertyName, IReadOnlyList<string> values)
    {
        writer.BeginArray(propertyName);
        foreach (string value in values)
        {
            writer.WriteStringElement(value);
        }

        writer.EndArray();
    }

    private static void WriteBottleneckProvenance(CanonicalJsonWriter writer, string propertyName, IReadOnlyList<BottleneckTaskProvenance> provenance)
    {
        writer.BeginArray(propertyName);
        foreach (BottleneckTaskProvenance item in provenance)
        {
            writer.BeginObject();
            writer.WriteString("commitId", item.CommitId);
            writer.WriteString("extractorId", item.Match.ExtractorId);
            writer.WriteNumber("spanStart", item.Match.SpanStart);
            writer.WriteNumber("spanEnd", item.Match.SpanEnd);
            writer.WriteString("text", item.Match.MatchedText);
            writer.EndObject();
        }

        writer.EndArray();
    }

    private static void WriteVertices(CanonicalJsonWriter writer, HistoryIngestionResult result, CoChangeGraph graph)
    {
        writer.BeginArray("vertices");
        foreach (CoChangeVertex vertex in graph.Vertices)
        {
            writer.BeginObject();
            writer.WriteString("canonicalPath", vertex.CanonicalPath);
            writer.WriteString("category", CategoryText(vertex.Category));
            writer.BeginArray("renameComponentIndexes");
            foreach (RenameComponent component in vertex.RenameComponents)
            {
                writer.WriteNumberElement(IndexOf(result.RenameComponents, component));
            }

            writer.EndArray();
            writer.EndObject();
        }

        writer.EndArray();
    }

    private static void WritePairs(CanonicalJsonWriter writer, CoChangeGraph graph)
    {
        writer.BeginArray("pairs");
        foreach (CoChangePair pair in graph.Pairs)
        {
            writer.BeginObject();
            writer.WriteString("firstPath", pair.First.CanonicalPath);
            writer.WriteString("secondPath", pair.Second.CanonicalPath);
            WriteCohort(writer, pair.Cohort);
            writer.WriteNumber("commitCoChange", pair.CommitCoChange);
            writer.WriteNumber("taskCoChange", pair.TaskCoChange);
            writer.WriteBoolean("isBaseEdge", pair.IsBaseEdge);
            writer.WriteOptionalNumber("cohortRank", pair.CohortRank);
            writer.WriteOptionalCanonicalDecimal("commitComponent", pair.CommitComponent);
            writer.WriteOptionalCanonicalDecimal("taskComponent", pair.TaskComponent);
            writer.WriteOptionalCanonicalDecimal("combinedCoChange", pair.CombinedCoChange);
            writer.BeginArray("commitIds");
            foreach (string commitId in pair.CommitIds)
            {
                writer.WriteStringElement(commitId);
            }

            writer.EndArray();
            writer.BeginArray("taskKeys");
            foreach (TaskKey key in pair.TaskKeys)
            {
                writer.BeginObject();
                writer.WriteString("namespace", key.Namespace);
                writer.WriteIntegerText("id", key.IdText);
                writer.EndObject();
            }

            writer.EndArray();
            writer.EndObject();
        }

        writer.EndArray();
    }

    private static void WriteClusters(CanonicalJsonWriter writer, CoChangeGraph graph)
    {
        writer.BeginArray("clusters");
        foreach (CoChangeCluster cluster in graph.Clusters)
        {
            writer.BeginObject();
            writer.WriteString("id", ClusterId(cluster));
            WriteCohort(writer, cluster.Cohort);
            writer.WriteCanonicalDecimal("maximum", cluster.Maximum);
            writer.WriteCanonicalDecimal("aggregate", cluster.Aggregate);
            writer.BeginArray("members");
            foreach (CoChangeVertex member in cluster.Members)
            {
                writer.WriteStringElement(member.CanonicalPath);
            }

            writer.EndArray();
            writer.BeginArray("edges");
            foreach (CoChangePair edge in cluster.Edges)
            {
                writer.BeginObject();
                writer.WriteString("firstPath", edge.First.CanonicalPath);
                writer.WriteString("secondPath", edge.Second.CanonicalPath);
                writer.EndObject();
            }

            writer.EndArray();
            writer.EndObject();
        }

        writer.EndArray();
    }

    private static void WriteCohort(CanonicalJsonWriter writer, CoChangeCohort cohort)
    {
        writer.BeginObject("cohort");
        writer.WriteString("firstCategory", CategoryText(cohort.First));
        writer.WriteString("secondCategory", CategoryText(cohort.Second));
        writer.EndObject();
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

    private static int IndexOf(IReadOnlyList<RenameComponent> components, RenameComponent component)
    {
        for (int index = 0; index < components.Count; index++)
        {
            if (ReferenceEquals(components[index], component))
            {
                return index;
            }
        }

        return -1;
    }

    private static string CategoryText(HistoryPathCategory category) => category switch
    {
        HistoryPathCategory.Production => "production",
        HistoryPathCategory.Tests => "tests",
        HistoryPathCategory.Docs => "docs",
        HistoryPathCategory.Generated => "generated",
        HistoryPathCategory.BuildCi => "build_ci",
        HistoryPathCategory.SamplesExamples => "samples_examples",
        HistoryPathCategory.Unknown => "unknown",
        _ => "unknown",
    };

    internal static string FindingId(string kind, HistoryPathCategory category, string path) => $"{kind}:{CategoryText(category)}:{path}";

    internal static string ClusterId(CoChangeCluster cluster)
    {
        string members = string.Concat(cluster.Members.Select(static item => $"{item.CanonicalPath.Length}:{item.CanonicalPath}"));
        return $"co-change-cluster:{CategoryText(cluster.Cohort.First)}:{CategoryText(cluster.Cohort.Second)}:{members}";
    }

    private static string LineCountStatusText(LineCountStatus status) => status switch
    {
        LineCountStatus.Text => "text",
        LineCountStatus.ExactRename => "exact_rename",
        LineCountStatus.BinaryOrUnavailable => "binary_or_unavailable",
        _ => "unknown",
    };

}
