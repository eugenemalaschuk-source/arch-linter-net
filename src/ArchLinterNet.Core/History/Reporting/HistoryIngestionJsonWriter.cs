using ArchLinterNet.Core.History.Analysis;
using ArchLinterNet.Core.History.Configuration;
using ArchLinterNet.Core.History.Enrichment;
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
        WriteCoChangeGraph(writer, result);
        WriteBottleneckAnalysis(writer, result.BottleneckAnalysis);
        WriteOcpAnalysis(writer, result.OcpAnalysis);
        WriteDotNetEnrichment(writer, result.DotNetEnrichment);
        writer.EndObject();
        return writer.ToCanonicalText() + "\n";
    }

    private static void WriteDotNetEnrichment(CanonicalJsonWriter writer, HistoryDotNetEnrichment enrichment)
    {
        writer.BeginObject("dotNetEnrichment");
        writer.WriteString("status", ToText(enrichment.Status));
        if (enrichment.Reason is not null)
        {
            writer.WriteString("reason", enrichment.Reason);
        }

        writer.BeginArray("files");
        foreach (HistoryDotNetFileEnrichment file in enrichment.Files)
        {
            writer.BeginObject();
            writer.WriteString("canonicalPath", file.CanonicalPath);
            writer.WriteString("status", ToText(file.Status));
            writer.BeginArray("types");
            foreach (HistoryDotNetTypeContext type in file.Types)
            {
                writer.BeginObject();
                writer.WriteString("projectPath", type.ProjectPath);
                writer.WriteString("assembly", type.AssemblyName);
                writer.WriteString("namespace", type.NamespaceName);
                writer.WriteString("fullName", type.FullTypeName);
                writer.WriteString("name", type.SimpleTypeName);
                writer.WriteString("kind", type.TypeKind.ToString().ToLowerInvariant());
                writer.WriteBoolean("isAbstract", type.IsAbstract);
                writer.EndObject();
            }

            writer.EndArray();
            writer.EndObject();
        }

        writer.EndArray();
        writer.EndObject();
    }

    private static string ToText(HistoryDotNetEnrichmentStatus status) => status switch
    {
        HistoryDotNetEnrichmentStatus.NotRequested => "not_requested",
        HistoryDotNetEnrichmentStatus.NotApplicable => "not_applicable",
        HistoryDotNetEnrichmentStatus.Available => "available",
        _ => "unavailable"
    };

    private static string ToText(HistoryDotNetFileEnrichmentStatus status) => status switch
    {
        HistoryDotNetFileEnrichmentStatus.Available => "available",
        _ => "not_applicable"
    };

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
}
