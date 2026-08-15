using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    private sealed partial class CandidatePackageFeed
    {
        public void WriteShardEvidence(
            string shardId,
            IReadOnlyList<CheckpointScenarioResult> scenarios,
            ConsumerPolicyShape? policyShape = null)
        {
            string? directory = Environment.GetEnvironmentVariable("CHECKPOINT_B_EVIDENCE_DIRECTORY");
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            string[] duplicateScenarioIds = scenarios
                .GroupBy(static scenario => scenario.Id, StringComparer.Ordinal)
                .Where(static group => group.Count() > 1)
                .Select(static group => group.Key)
                .OrderBy(static id => id, StringComparer.Ordinal)
                .ToArray();
            Assert.That(duplicateScenarioIds, Is.Empty,
                $"Checkpoint B shard '{shardId}' produced duplicate scenario IDs: {string.Join(", ", duplicateScenarioIds)}");

            Directory.CreateDirectory(directory);
            var evidence = new
            {
                schema = "checkpoint-b-platform-shard-evidence/v1",
                checkpoint = "B",
                shard_id = shardId,
                result = scenarios.Any(static scenario => scenario.Result == "failed") ? "failed" : "passed",
                candidate_version = _candidateVersion,
                source_commit = Environment.GetEnvironmentVariable("ARCH_LINTER_SOURCE_SHA") ?? "unknown",
                platform_id = Environment.GetEnvironmentVariable("CHECKPOINT_B_PLATFORM") ?? "unknown",
                platform = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                shell = _shell,
                synthetic_identities_only = true,
                candidate_manifest_sha256 = _manifestSha256,
                packages = _packages,
                policy_shape = policyShape,
                scenarios = scenarios.OrderBy(static scenario => scenario.Id, StringComparer.Ordinal),
            };
            string fileName = $"checkpoint-b-platform-shard-{shardId}.json";
            File.WriteAllText(Path.Combine(directory, fileName), JsonSerializer.Serialize(evidence, _evidenceSerializerOptions));
        }
    }
}
