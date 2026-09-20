using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ArchLinterNet.Core.Tests;

internal static class BenchmarkIdentity
{
    public static string Sha256(string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    public static BenchmarkCanonicalResultIdentity CreateCanonicalResult(
        string completionStatus,
        int exitCode,
        IReadOnlyList<BenchmarkFindingIdentity> findings)
    {
        var canonicalFindings = findings
            .Select((finding, index) => new
            {
                index,
                contract_id = finding.ContractId,
                kind = finding.Kind,
                source_assembly = finding.SourceAssembly,
                source_type = finding.SourceType,
                source_member = finding.SourceMember,
                location = NormalizePath(finding.Location),
            })
            .ToList();
        string canonical = BenchmarkJson.Serialize(new
        {
            completion_status = completionStatus,
            exit_code = exitCode,
            findings = canonicalFindings,
        });
        return new BenchmarkCanonicalResultIdentity
        {
            Algorithm = "sha256-canonical-result/v1",
            Sha256 = Sha256(canonical),
            FindingCount = findings.Count,
            CompletionStatus = completionStatus,
            ExitCode = exitCode,
        };
    }

    public static string NormalizePath(string path)
    {
        string normalized = path.Replace('\\', '/');
        string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        int sourceIndex = Array.FindIndex(segments, segment => string.Equals(segment, "src", StringComparison.OrdinalIgnoreCase));
        return sourceIndex >= 0 ? string.Join('/', segments[sourceIndex..]) : normalized;
    }

    public static string NormalizeJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
        {
            WriteIndented = false,
        });
    }
}

internal static class BenchmarkEvidenceFactory
{
    public static BenchmarkEvidenceDocument Create(
        BenchmarkWorkloadDefinition workload,
        JsonElement rawProfile,
        BenchmarkCanonicalResultIdentity canonicalResult,
        BenchmarkProfileSample? sample = null,
        BenchmarkComplexityEvidence? complexity = null,
        BenchmarkExpectedEffectEvidence? expectedEffect = null)
    {
        BenchmarkProfileSample resolvedSample = sample ?? new BenchmarkProfileSample
        {
            Run = new BenchmarkRunDescriptor
            {
                ExecutionMode = workload.Workflow.ExecutionMode,
                CacheMode = "disabled",
                PreparedStateMode = "unprepared",
                ParallelMode = "sequential",
                SampleOrdinal = 1,
                IsWarmSample = false,
            },
            RawAnalysisProfile = rawProfile,
            CompletionStatus = "Success",
            ExitCode = 0,
            OutputFailed = false,
            WallClock = BenchmarkResourceMeasurement.Unavailable("Synthetic contract sample has no wall-clock measurement."),
            ProcessorTime = BenchmarkResourceMeasurement.Unavailable("Synthetic contract sample has no processor-time measurement."),
            AllocatedBytes = BenchmarkResourceMeasurement.Unavailable("Synthetic contract sample has no allocation measurement."),
            PeakManagedMemory = BenchmarkResourceMeasurement.Unavailable("Synthetic contract sample has no memory measurement."),
        };
        var evidence = new BenchmarkEvidenceDocument
        {
            EvidenceSchemaId = BenchmarkEvidenceDocument.SchemaId,
            Workload = workload.ToManifest(),
            Run = resolvedSample.Run,
            DeterministicWork = new BenchmarkDeterministicWorkEvidence
            {
                ProjectCount = workload.Inventory.ProjectCount,
                AssemblyCount = workload.Inventory.AssemblyCount,
                SourceFileCount = workload.Inventory.SourceFileCount,
                TypeCount = workload.Inventory.TypeCount,
                ReferenceEdgeCount = workload.Inventory.ReferenceEdgeCount,
                LayerCount = workload.Inventory.LayerCount,
                SelectorMembershipCount = workload.Inventory.SelectorMembershipCount,
                ContractCount = workload.Inventory.ContractCount,
                FindingCandidateCount = workload.Inventory.FindingCandidateCount,
                SourceRootCount = workload.Inventory.SourceRootCount,
                GraphTraversalCount = workload.Topology.ReferenceEdgeCount,
                WitnessPathMaterializationCount = workload.Topology.AlternatePathCount,
                CanonicalIdentityCandidateCount = workload.Inventory.FindingCandidateCount,
            },
            Samples = [resolvedSample],
            CanonicalResult = canonicalResult,
            Environment = new BenchmarkEnvironmentEvidence
            {
                Runtime = "synthetic-contract",
                OperatingSystem = "synthetic",
                Architecture = "synthetic",
                Configuration = "deterministic",
                SourceIdentity = workload.WorkloadIdentity,
                ToolIdentity = "ArchLinterNet synthetic benchmark foundation",
                PeakWorkingSet = BenchmarkResourceMeasurement.Unavailable("Not measured."),
                ManagedAllocation = BenchmarkResourceMeasurement.Unavailable("Not measured."),
            },
            Complexity = complexity,
            ExpectedEffect = expectedEffect,
            Disposition = BenchmarkEvidenceDisposition.Candidate,
        };
        evidence.Validate();
        return evidence;
    }
}
