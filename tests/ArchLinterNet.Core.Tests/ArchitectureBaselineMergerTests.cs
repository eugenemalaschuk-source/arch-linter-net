using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureBaselineMergerTests
{
    [Test]
    public void Merge_MatchingId_AppendsIgnores()
    {
        var policy = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "Test.Core" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string>()
            },
            Contracts = new ArchitectureContractGroups
            {
                Strict = new List<ArchitectureDependencyContract>
                {
                    new() { Id = "my-rule", Name = "my-rule", Source = "core", Forbidden = new List<string>() }
                }
            }
        };

        var baseline = new ArchitectureBaselineDocument
        {
            Version = 1,
            Baseline = new ArchitectureBaselineContractGroups
            {
                Strict = new List<ArchitectureBaselineContractEntry>
                {
                    new()
                    {
                        Id = "my-rule",
                        IgnoredViolations = new List<ArchitectureIgnoredViolation>
                        {
                            new() { SourceType = "Old.Type", ForbiddenReference = "Bad.Type", Reason = "migration" }
                        }
                    }
                }
            }
        };

        ArchitectureBaselineMerger.Merge(policy, baseline);

        Assert.That(policy.Contracts.Strict[0].IgnoredViolations, Has.Count.EqualTo(1));
        Assert.That(policy.Contracts.Strict[0].IgnoredViolations[0].SourceType, Is.EqualTo("Old.Type"));
    }

    [Test]
    public void MergeAndValidate_UnknownId_Throws()
    {
        var policy = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "Test.Core" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string>()
            },
            Contracts = new ArchitectureContractGroups()
        };

        var baseline = new ArchitectureBaselineDocument
        {
            Version = 1,
            Baseline = new ArchitectureBaselineContractGroups
            {
                Strict = new List<ArchitectureBaselineContractEntry>
                {
                    new()
                    {
                        Id = "nonexistent-rule",
                        IgnoredViolations = new List<ArchitectureIgnoredViolation>
                        {
                            new() { SourceType = "X", ForbiddenReference = "Y", Reason = "stale" }
                        }
                    }
                }
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureBaselineMerger.MergeAndValidate(policy, baseline));
        Assert.That(ex!.Message, Does.Contain("nonexistent-rule"));
    }

    [Test]
    public void Merge_DuplicateIgnore_Skipped()
    {
        var policy = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "Test.Core" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string>()
            },
            Contracts = new ArchitectureContractGroups
            {
                Strict = new List<ArchitectureDependencyContract>
                {
                    new()
                    {
                        Id = "my-rule",
                        Name = "my-rule",
                        Source = "core",
                        Forbidden = new List<string>(),
                        IgnoredViolations = new List<ArchitectureIgnoredViolation>
                        {
                            new() { SourceType = "Old.Type", ForbiddenReference = "Bad.Type", Reason = "manual" }
                        }
                    }
                }
            }
        };

        var baseline = new ArchitectureBaselineDocument
        {
            Version = 1,
            Baseline = new ArchitectureBaselineContractGroups
            {
                Strict = new List<ArchitectureBaselineContractEntry>
                {
                    new()
                    {
                        Id = "my-rule",
                        IgnoredViolations = new List<ArchitectureIgnoredViolation>
                        {
                            new() { SourceType = "Old.Type", ForbiddenReference = "Bad.Type", Reason = "baseline" }
                        }
                    }
                }
            }
        };

        ArchitectureBaselineMerger.Merge(policy, baseline);

        Assert.That(policy.Contracts.Strict[0].IgnoredViolations, Has.Count.EqualTo(1));
        Assert.That(policy.Contracts.Strict[0].IgnoredViolations[0].Reason, Is.EqualTo("manual"));
    }

    [Test]
    public void Merge_BaselineAddsNewIgnores_DoesNotRemoveExisting()
    {
        var policy = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "Test.Core" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string>()
            },
            Contracts = new ArchitectureContractGroups
            {
                Strict = new List<ArchitectureDependencyContract>
                {
                    new()
                    {
                        Id = "my-rule",
                        Name = "my-rule",
                        Source = "core",
                        Forbidden = new List<string>(),
                        IgnoredViolations = new List<ArchitectureIgnoredViolation>
                        {
                            new() { SourceType = "Existing", ForbiddenReference = "Old", Reason = "manual" }
                        }
                    }
                }
            }
        };

        var baseline = new ArchitectureBaselineDocument
        {
            Version = 1,
            Baseline = new ArchitectureBaselineContractGroups
            {
                Strict = new List<ArchitectureBaselineContractEntry>
                {
                    new()
                    {
                        Id = "my-rule",
                        IgnoredViolations = new List<ArchitectureIgnoredViolation>
                        {
                            new() { SourceType = "New", ForbiddenReference = "Added", Reason = "baseline" }
                        }
                    }
                }
            }
        };

        ArchitectureBaselineMerger.Merge(policy, baseline);

        Assert.That(policy.Contracts.Strict[0].IgnoredViolations, Has.Count.EqualTo(2));
        Assert.That(policy.Contracts.Strict[0].IgnoredViolations[0].SourceType, Is.EqualTo("Existing"));
        Assert.That(policy.Contracts.Strict[0].IgnoredViolations[1].SourceType, Is.EqualTo("New"));
    }

    [Test]
    public void Merge_BaselineIgnores_SuppressViolationInRunner()
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

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(ArchitectureContractLoader).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var preMergeRunner = new ArchitectureContractRunner(context, policy);
        var preMergeViolations = preMergeRunner.CheckContract(policy.Contracts.Strict[0]);
        Assert.That(preMergeViolations, Is.Not.Empty, "Expected baseline violations to exist before merge");

        var baseline = new ArchitectureBaselineDocument
        {
            Version = 1,
            Baseline = new ArchitectureBaselineContractGroups
            {
                Strict = new List<ArchitectureBaselineContractEntry>
                {
                    new()
                    {
                        Id = "exec-no-res",
                        IgnoredViolations = preMergeViolations
                            .SelectMany(v => v.ForbiddenReferences.Select(fr =>
                                new ArchitectureIgnoredViolation
                                {
                                    SourceType = v.SourceType,
                                    ForbiddenReference = fr,
                                    Reason = "baseline"
                                }))
                            .DistinctBy(iv => $"{iv.SourceType}|{iv.ForbiddenReference}")
                            .ToList()
                    }
                }
            }
        };

        ArchitectureBaselineMerger.Merge(policy, baseline);

        var postMergeRunner = new ArchitectureContractRunner(context, policy);
        var postMergeViolations = postMergeRunner.CheckContract(policy.Contracts.Strict[0]);

        Assert.That(postMergeViolations, Is.Empty,
            "Merged baseline ignores should suppress all violations");
    }

    [Test]
    public void Merge_BaselineStaleIgnore_TriggersUnmatched()
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

        var baseline = new ArchitectureBaselineDocument
        {
            Version = 1,
            Baseline = new ArchitectureBaselineContractGroups
            {
                Strict = new List<ArchitectureBaselineContractEntry>
                {
                    new()
                    {
                        Id = "exec-no-res",
                        IgnoredViolations = new List<ArchitectureIgnoredViolation>
                        {
                            new()
                            {
                                SourceType = "Stale.Type.That.Does.Not.Exist",
                                ForbiddenReference = "Also.Stale",
                                Reason = "stale baseline entry"
                            }
                        }
                    }
                }
            }
        };

        ArchitectureBaselineMerger.Merge(policy, baseline);

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(ArchitectureContractLoader).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var runner = new ArchitectureContractRunner(context, policy);
        runner.CheckContract(policy.Contracts.Strict[0]);

        Assert.That(runner.UnmatchedIgnoredViolations, Has.Count.EqualTo(1));
        Assert.That(runner.UnmatchedIgnoredViolations[0].SourceType, Is.EqualTo("Stale.Type.That.Does.Not.Exist"));
        Assert.That(runner.UnmatchedIgnoredViolations[0].ForbiddenReference, Is.EqualTo("Also.Stale"));
    }
}
