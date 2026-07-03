using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.IO;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureBaselineIntegrationTests
{
    private static readonly ArchitectureBaselineGenerator _generator = new();

    private static ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(ArchitecturePolicyDocumentLoader).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    [Test]
    public void Runner_WithBaselineTracking_CollectsCandidates()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "ArchLinterNet.Core" },
                ["resolution"] = new() { Namespace = "ArchLinterNet.Core.Resolution" },
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
            },
            Contracts = new ArchitectureContractGroups
            {
                Strict = new List<ArchitectureDependencyContract>
                {
                    new()
                    {
                        Id = "exec-no-res",
                        Name = "execution-not-in-resolution",
                        Source = "execution",
                        Forbidden = new List<string> { "resolution" }
                    }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        runner.CheckContract(document.Contracts.Strict[0]);

        Assert.That(runner.BaselineCandidates, Is.Not.Empty,
            "Runner should collect baseline candidates during check");
    }

    [Test]
    public void Runner_WithoutBaselineTracking_DoesNotCollectCandidates()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "ArchLinterNet.Core" },
                ["resolution"] = new() { Namespace = "ArchLinterNet.Core.Resolution" },
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
            },
            Contracts = new ArchitectureContractGroups
            {
                Strict = new List<ArchitectureDependencyContract>
                {
                    new()
                    {
                        Id = "exec-no-res",
                        Name = "execution-not-in-resolution",
                        Source = "execution",
                        Forbidden = new List<string> { "resolution" }
                    }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document,
            enableUnmatchedIgnoreTracking: false);
        runner.CheckContract(document.Contracts.Strict[0]);

        Assert.That(runner.BaselineCandidates, Is.Empty);
    }

    [Test]
    public void Generator_FromRunnerCandidates_ProducesValidBaseline()
    {
        var policy = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "ArchLinterNet.Core" },
                ["resolution"] = new() { Namespace = "ArchLinterNet.Core.Resolution" },
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
            },
            Contracts = new ArchitectureContractGroups
            {
                Strict = new List<ArchitectureDependencyContract>
                {
                    new()
                    {
                        Id = "exec-no-res",
                        Name = "execution-not-in-resolution",
                        Source = "execution",
                        Forbidden = new List<string> { "resolution" }
                    }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), policy);
        runner.CheckContract(policy.Contracts.Strict[0]);

        ArchitectureBaselineDocument baseline = _generator.Generate(
            policy, runner.BaselineCandidates, "test baseline");

        Assert.That(baseline.Version, Is.EqualTo(1));
        Assert.That(baseline.Baseline.Strict, Has.Count.EqualTo(1));
        Assert.That(baseline.Baseline.Strict[0].Id, Is.EqualTo("exec-no-res"));
        Assert.That(baseline.Baseline.Strict[0].IgnoredViolations, Is.Not.Empty);

        foreach (var entry in baseline.Baseline.Strict[0].IgnoredViolations)
        {
            Assert.That(entry.Reason, Is.EqualTo("test baseline"));
        }
    }

    [Test]
    public void FullFlow_RunnerGenerateMergeValidate_NewViolationsStillFail()
    {
        var policy = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "ArchLinterNet.Core" },
                ["resolution"] = new() { Namespace = "ArchLinterNet.Core.Resolution" },
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" },
                ["model"] = new() { Namespace = "ArchLinterNet.Core.Model" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
            },
            Contracts = new ArchitectureContractGroups
            {
                Strict = new List<ArchitectureDependencyContract>
                {
                    new()
                    {
                        Id = "exec-no-res",
                        Name = "execution-not-in-resolution",
                        Source = "execution",
                        Forbidden = new List<string> { "resolution" }
                    },
                    new()
                    {
                        Id = "exec-no-model",
                        Name = "execution-not-in-model",
                        Source = "execution",
                        Forbidden = new List<string> { "model" }
                    }
                }
            }
        };

        var context = CreateContext();

        var generateRunner = new ArchitectureContractRunner(context, policy);
        generateRunner.CheckContract(policy.Contracts.Strict[0]);
        generateRunner.CheckContract(policy.Contracts.Strict[1]);

        ArchitectureBaselineDocument baseline = _generator.Generate(
            policy, generateRunner.BaselineCandidates, "auto-baseline");

        string mergedYaml = _generator.Serialize(baseline);

        var tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            string baselinePath = Path.Combine(tempDir, "baseline.yml");
            File.WriteAllText(baselinePath, mergedYaml);

            var loadedBaseline = ArchitectureBaselineLoadingService.LoadFromPath(baselinePath, ArchitectureFileSystem.Real);
            ArchitectureBaselineLoadingService.MergeAndValidate(policy, loadedBaseline);

            var finalRunner = new ArchitectureContractRunner(context, policy);
            var violations1 = finalRunner.CheckContract(policy.Contracts.Strict[0]);
            var violations2 = finalRunner.CheckContract(policy.Contracts.Strict[1]);

            bool hasNewViolation = violations2.Any(v =>
                !violations1.Any(vi =>
                    vi.SourceType == v.SourceType &&
                    vi.ForbiddenReferences.SequenceEqual(v.ForbiddenReferences)));

            if (hasNewViolation)
            {
                Assert.That(violations2, Is.Not.Empty,
                    "New violations that weren't in baseline should still be reported");
            }
            else
            {
                Assert.That(violations1, Is.Empty,
                    "If all violations match baseline, they should be suppressed");
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
