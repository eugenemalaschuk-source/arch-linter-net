using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class UnmatchedIgnoredViolationTests
{
    private static ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(ArchitecturePolicyDocumentLoader).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    [Test]
    public void DependencyContract_MatchedIgnore_NotReportedAsUnmatched()
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
                        Name = "execution-not-in-resolution",
                        Source = "execution",
                        Forbidden = new List<string> { "resolution" }
                    }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var violations = runner.CheckContract(document.Contracts.Strict[0]);

        Assert.That(violations, Is.Not.Empty, "Expected baseline violations for test validity");

        string sourceType = violations[0].SourceType;
        string forbiddenRef = violations[0].ForbiddenReferences.First();

        document.Contracts.Strict[0].IgnoredViolations.Add(new ArchitectureIgnoredViolation
        {
            SourceType = sourceType,
            ForbiddenReference = forbiddenRef,
            Reason = "matched ignore for test"
        });

        runner = new ArchitectureContractRunner(CreateContext(), document);
        runner.CheckContract(document.Contracts.Strict[0]);

        Assert.That(runner.UnmatchedIgnoredViolations,
            Is.Empty,
            "Matched ignore should not appear as unmatched");
    }

    [Test]
    public void DependencyContract_UnmatchedIgnore_Reported()
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
                        Name = "execution-not-in-resolution",
                        Source = "execution",
                        Forbidden = new List<string> { "resolution" },
                        IgnoredViolations = new List<ArchitectureIgnoredViolation>
                        {
                            new()
                            {
                                SourceType = "Does.Not.Exist.Type",
                                ForbiddenReference = "Also.Does.Not.Exist",
                                Reason = "stale ignore for test"
                            }
                        }
                    }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        runner.CheckContract(document.Contracts.Strict[0]);

        Assert.That(runner.UnmatchedIgnoredViolations, Has.Count.EqualTo(1));
        Assert.That(runner.UnmatchedIgnoredViolations[0].SourceType, Is.EqualTo("Does.Not.Exist.Type"));
        Assert.That(runner.UnmatchedIgnoredViolations[0].ForbiddenReference, Is.EqualTo("Also.Does.Not.Exist"));
        Assert.That(runner.UnmatchedIgnoredViolations[0].Reason, Is.EqualTo("stale ignore for test"));
        Assert.That(runner.UnmatchedIgnoredViolations[0].IgnoreIndex, Is.EqualTo(0));
    }

    [Test]
    public void DependencyContract_MixedMatchedAndUnmatched()
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
                        Name = "execution-not-in-resolution",
                        Source = "execution",
                        Forbidden = new List<string> { "resolution" }
                    }
                }
            }
        };

        var baselineRunner = new ArchitectureContractRunner(CreateContext(), document);
        var baselineViolations = baselineRunner.CheckContract(document.Contracts.Strict[0]);
        Assert.That(baselineViolations, Is.Not.Empty);

        string sourceType = baselineViolations[0].SourceType;
        string forbiddenRef = baselineViolations[0].ForbiddenReferences.First();

        document.Contracts.Strict[0].IgnoredViolations = new List<ArchitectureIgnoredViolation>
        {
            new()
            {
                SourceType = sourceType,
                ForbiddenReference = forbiddenRef,
                Reason = "matched — tracks real debt"
            },
            new()
            {
                SourceType = "Stale.Entry.Type",
                ForbiddenReference = "Stale.Ref",
                Reason = "unmatched — debt already resolved"
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        runner.CheckContract(document.Contracts.Strict[0]);

        Assert.That(runner.UnmatchedIgnoredViolations, Has.Count.EqualTo(1));
        Assert.That(runner.UnmatchedIgnoredViolations[0].IgnoreIndex, Is.EqualTo(1));
        Assert.That(runner.UnmatchedIgnoredViolations[0].SourceType, Is.EqualTo("Stale.Entry.Type"));
    }

    [Test]
    public void ExternalContract_UnmatchedIgnore_Reported()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "ArchLinterNet.Core" }
            },
            ExternalDependencies = new Dictionary<string, ArchitectureExternalDependencyGroup>
            {
                ["system"] = new()
                {
                    NamespacePrefixes = new List<string> { "System" }
                }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictExternal = new List<ArchitectureExternalDependencyContract>
                {
                    new()
                    {
                        Name = "core-no-system",
                        Source = "core",
                        Forbidden = new List<string> { "system" }
                    }
                }
            }
        };

        var baselineRunner = new ArchitectureContractRunner(CreateContext(), document);
        var baselineViolations = baselineRunner.CheckExternalContract(document.Contracts.StrictExternal[0]);
        Assert.That(baselineViolations, Is.Not.Empty);

        string sourceType = baselineViolations[0].SourceType;
        string forbiddenRef = baselineViolations[0].ForbiddenReferences.First();

        document.Contracts.StrictExternal[0].IgnoredViolations = new List<ArchitectureIgnoredViolation>
        {
            new()
            {
                SourceType = sourceType,
                ForbiddenReference = forbiddenRef,
                Reason = "matched ignore"
            },
            new()
            {
                SourceType = "Nonexistent.Type",
                ForbiddenReference = "System.Foo",
                Reason = "stale ignore"
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        runner.CheckExternalContract(document.Contracts.StrictExternal[0]);

        Assert.That(runner.UnmatchedIgnoredViolations, Has.Count.EqualTo(1));
        Assert.That(runner.UnmatchedIgnoredViolations[0].SourceType, Is.EqualTo("Nonexistent.Type"));
    }

    [Test]
    public void CycleContract_UnmatchedIgnore_Reported()
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
                StrictCycles = new List<ArchitectureCycleContract>
                {
                    new()
                    {
                        Name = "no-cycles",
                        Layers = new List<string> { "resolution", "execution" },
                        IgnoredViolations = new List<ArchitectureIgnoredViolation>
                        {
                            new()
                            {
                                SourceType = "Does.Not.Exist",
                                ForbiddenReference = "Also.Not.Exist",
                                Reason = "stale cycle ignore"
                            }
                        }
                    }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        runner.CheckCycleContract(document.Contracts.StrictCycles[0]);

        Assert.That(runner.UnmatchedIgnoredViolations, Has.Count.EqualTo(1));
        Assert.That(runner.UnmatchedIgnoredViolations[0].Reason, Is.EqualTo("stale cycle ignore"));
    }

    [Test]
    public void LayerContract_UnmatchedIgnore_Reported()
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
                StrictLayers = new List<ArchitectureLayerContract>
                {
                    new()
                    {
                        Name = "layer-order",
                        Layers = new List<string> { "execution", "resolution", "core" },
                        IgnoredViolations = new List<ArchitectureIgnoredViolation>
                        {
                            new()
                            {
                                SourceType = "Fake.Source",
                                ForbiddenReference = "Fake.Target",
                                Reason = "stale layer ignore"
                            }
                        }
                    }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        runner.CheckLayerContract(document.Contracts.StrictLayers[0]);

        Assert.That(runner.UnmatchedIgnoredViolations, Has.Count.EqualTo(1));
        Assert.That(runner.UnmatchedIgnoredViolations[0].SourceType, Is.EqualTo("Fake.Source"));
    }

    [Test]
    public void NoIgnoredViolations_NoUnmatched()
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
                        Name = "no-ignores",
                        Source = "execution",
                        Forbidden = new List<string> { "resolution" }
                    }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        runner.CheckContract(document.Contracts.Strict[0]);

        Assert.That(runner.UnmatchedIgnoredViolations, Is.Empty);
    }

    [Test]
    public void MultipleContracts_UnmatchedIsolated()
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
                        Name = "first",
                        Source = "execution",
                        Forbidden = new List<string> { "resolution" },
                        IgnoredViolations = new List<ArchitectureIgnoredViolation>
                        {
                            new()
                            {
                                SourceType = "Stale.First",
                                ForbiddenReference = "Stale.Ref",
                                Reason = "first stale"
                            }
                        }
                    },
                    new()
                    {
                        Name = "second",
                        Source = "core",
                        Forbidden = new List<string> { "resolution" },
                        IgnoredViolations = new List<ArchitectureIgnoredViolation>
                        {
                            new()
                            {
                                SourceType = "Stale.Second",
                                ForbiddenReference = "Stale.Ref",
                                Reason = "second stale"
                            }
                        }
                    }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        runner.CheckContract(document.Contracts.Strict[0]);
        runner.CheckContract(document.Contracts.Strict[1]);

        Assert.That(runner.UnmatchedIgnoredViolations, Has.Count.EqualTo(2));
        Assert.That(runner.UnmatchedIgnoredViolations.Any(u => u.SourceType == "Stale.First"), Is.True);
        Assert.That(runner.UnmatchedIgnoredViolations.Any(u => u.SourceType == "Stale.Second"), Is.True);
    }
}
