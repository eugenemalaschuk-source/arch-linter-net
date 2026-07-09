using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class TransitiveDependencyContractTests
{
    private static ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext(
            "/tmp",
            new[]
            {
                typeof(ArchitectureContractDocument).Assembly
            },
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    [Test]
    public void CheckContract_DefaultDepth_DirectViolationDetected()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" },
                ["contracts"] = new() { Namespace = "ArchLinterNet.Core.Contracts" }
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
                        Name = "execution-must-not-depend-on-contracts",
                        Source = "execution",
                        Forbidden = new List<string> { "contracts" }
                    }
                }
            }
        };

        var context = CreateContext();
        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckContract(document.Contracts.Strict[0]).ToList();

        Assert.That(violations.Count, Is.GreaterThan(0));
        Assert.That(violations.All(v => (v.Payload as ConfigurationPayload)?.DependencyPaths == null), Is.True);
    }

    [Test]
    public void CheckContract_TransitiveMode_ProducesDependencyPaths()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" },
                ["contracts"] = new() { Namespace = "ArchLinterNet.Core.Contracts" }
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
                        Name = "execution-must-not-depend-on-contracts",
                        Source = "execution",
                        Forbidden = new List<string> { "contracts" },
                        DependencyDepth = DependencyDepthMode.Transitive
                    }
                }
            }
        };

        var context = CreateContext();
        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckContract(document.Contracts.Strict[0]).ToList();

        Assert.That(violations.Count, Is.GreaterThan(0));
        Assert.That(violations.All(v => (v.Payload as ConfigurationPayload)?.DependencyPaths != null), Is.True);
        Assert.That(violations.All(v => (v.Payload as ConfigurationPayload)?.DependencyPaths!.Count == v.ForbiddenReferences.Count), Is.True);
    }

    [Test]
    public void CheckContract_TransitiveMode_PathStartsWithSourceType()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" },
                ["contracts"] = new() { Namespace = "ArchLinterNet.Core.Contracts" }
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
                        Name = "execution-must-not-depend-on-contracts",
                        Source = "execution",
                        Forbidden = new List<string> { "contracts" },
                        DependencyDepth = DependencyDepthMode.Transitive
                    }
                }
            }
        };

        var context = CreateContext();
        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckContract(document.Contracts.Strict[0]).ToList();

        Assert.That(violations.Count, Is.GreaterThan(0));
        foreach (var violation in violations)
        {
            Assert.That((violation.Payload as ConfigurationPayload)?.DependencyPaths, Is.Not.Null);
            foreach (var path in (violation.Payload as ConfigurationPayload)?.DependencyPaths!)
            {
                Assert.That(path.First(), Is.EqualTo(violation.SourceType));
            }
        }
    }

    [Test]
    public void CheckContract_TransitiveMode_PathEndsWithForbiddenReference()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" },
                ["contracts"] = new() { Namespace = "ArchLinterNet.Core.Contracts" }
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
                        Name = "execution-must-not-depend-on-contracts",
                        Source = "execution",
                        Forbidden = new List<string> { "contracts" },
                        DependencyDepth = DependencyDepthMode.Transitive
                    }
                }
            }
        };

        var context = CreateContext();
        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckContract(document.Contracts.Strict[0]).ToList();

        Assert.That(violations.Count, Is.GreaterThan(0));
        foreach (var violation in violations)
        {
            var configurationPayload = (ConfigurationPayload)violation.Payload!;
            Assert.That(configurationPayload.DependencyPaths, Is.Not.Null);
            var refsList = violation.ForbiddenReferences.ToList();
            for (int i = 0; i < refsList.Count; i++)
            {
                var path = configurationPayload.DependencyPaths!.ElementAt(i);
                Assert.That(path.Last(), Is.EqualTo(refsList[i]));
            }
        }
    }

    [Test]
    public void CheckContract_TransitiveMode_FindsAtLeastAsManyAsDirect()
    {
        var documentDirect = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" },
                ["contracts"] = new() { Namespace = "ArchLinterNet.Core.Contracts" }
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
                        Name = "execution-must-not-depend-on-contracts",
                        Source = "execution",
                        Forbidden = new List<string> { "contracts" },
                        DependencyDepth = DependencyDepthMode.Direct
                    }
                }
            }
        };

        var documentTransitive = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" },
                ["contracts"] = new() { Namespace = "ArchLinterNet.Core.Contracts" }
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
                        Name = "execution-must-not-depend-on-contracts",
                        Source = "execution",
                        Forbidden = new List<string> { "contracts" },
                        DependencyDepth = DependencyDepthMode.Transitive
                    }
                }
            }
        };

        var context = CreateContext();
        var runnerDirect = new ArchitectureContractRunner(context, documentDirect);
        var runnerTransitive = new ArchitectureContractRunner(context, documentTransitive);
        var directViolations = runnerDirect.CheckContract(documentDirect.Contracts.Strict[0])
            .SelectMany(v => v.ForbiddenReferences).ToHashSet();
        var transitiveViolations = runnerTransitive.CheckContract(documentTransitive.Contracts.Strict[0])
            .SelectMany(v => v.ForbiddenReferences).ToHashSet();

        Assert.That(transitiveViolations.IsSupersetOf(directViolations), Is.True,
            "Transitive mode should find all direct violations plus any indirect ones");
    }

    [Test]
    public void CheckContract_TransitiveMode_AllowedTypesExcludesForbiddenTerminal()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" },
                ["contracts"] = new() { Namespace = "ArchLinterNet.Core.Contracts" }
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
                        Name = "execution-must-not-depend-on-contracts",
                        Source = "execution",
                        Forbidden = new List<string> { "contracts" },
                        DependencyDepth = DependencyDepthMode.Transitive,
                        AllowedTypes = new List<string>
                        {
                            typeof(ArchitectureDependencyContract).FullName!,
                            typeof(ArchitectureContractDocument).FullName!,
                            typeof(ArchitectureContractGroups).FullName!,
                            typeof(ArchitectureLayer).FullName!,
                            typeof(ArchitectureAnalysisConfiguration).FullName!,
                            typeof(IArchitectureContract).FullName!,
                            typeof(ArchitectureIgnoredViolation).FullName!,
                            typeof(ArchitectureLayerContract).FullName!,
                            typeof(ArchitectureAllowOnlyContract).FullName!,
                            typeof(ArchitectureCycleContract).FullName!,
                            typeof(ArchitectureMethodBodyContract).FullName!,
                            typeof(ArchitectureAsmdefContract).FullName!,
                            typeof(ArchitectureIndependenceContract).FullName!,
                            typeof(ArchitectureProtectedContract).FullName!,
                            typeof(ArchitectureLayerTemplateContract).FullName!,
                            typeof(ArchitectureTemplateLayer).FullName!,
                        }
                    }
                }
            }
        };

        var context = CreateContext();
        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckContract(document.Contracts.Strict[0]).ToList();

        if (violations.Count > 0)
        {
            var allRefs = violations.SelectMany(v => v.ForbiddenReferences).ToList();
            Assert.That(allRefs, Does.Not.Contain(typeof(ArchitectureDependencyContract).FullName));
        }
    }

    [Test]
    public void CheckContract_TransitiveMode_AuditContract_ProducesDependencyPaths()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" },
                ["contracts"] = new() { Namespace = "ArchLinterNet.Core.Contracts" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
            },
            Contracts = new ArchitectureContractGroups
            {
                Audit = new List<ArchitectureDependencyContract>
                {
                    new()
                    {
                        Name = "audit-execution-not-depend-on-contracts",
                        Source = "execution",
                        Forbidden = new List<string> { "contracts" },
                        DependencyDepth = DependencyDepthMode.Transitive
                    }
                }
            }
        };

        var context = CreateContext();
        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.AuditContracts().First();
        var results = runner.CheckContract(violations).ToList();

        Assert.That(results.Count, Is.GreaterThan(0));
        Assert.That(results.All(v => (v.Payload as ConfigurationPayload)?.DependencyPaths != null), Is.True);
    }

    [Test]
    public void CheckContract_TransitiveMode_IgnoredViolations_SuppressesViolation()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" },
                ["contracts"] = new() { Namespace = "ArchLinterNet.Core.Contracts" }
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
                        Name = "execution-must-not-depend-on-contracts",
                        Source = "execution",
                        Forbidden = new List<string> { "contracts" },
                        DependencyDepth = DependencyDepthMode.Transitive,
                        IgnoredViolations = new List<ArchitectureIgnoredViolation>
                        {
                            new()
                            {
                                SourceType = "ArchLinterNet.Core.Execution.ArchitectureContractRunner",
                                ForbiddenReference = "*",
                                Reason = "baseline"
                            }
                        }
                    }
                }
            }
        };

        var context = CreateContext();
        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckContract(document.Contracts.Strict[0]).ToList();

        Assert.That(violations.All(v => v.SourceType != typeof(ArchitectureContractRunner).FullName), Is.True,
            "Ignored violation should suppress the source+terminal pair");
    }

    [Test]
    public void CheckContract_TransitiveMode_ProducesDeterministicResults()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" },
                ["contracts"] = new() { Namespace = "ArchLinterNet.Core.Contracts" }
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
                        Name = "execution-must-not-depend-on-contracts",
                        Source = "execution",
                        Forbidden = new List<string> { "contracts" },
                        DependencyDepth = DependencyDepthMode.Transitive
                    }
                }
            }
        };

        var context = CreateContext();
        var runner = new ArchitectureContractRunner(context, document);
        var run1 = runner.CheckContract(document.Contracts.Strict[0]).ToList();
        var run2 = runner.CheckContract(document.Contracts.Strict[0]).ToList();

        Assert.That(run1.Count, Is.EqualTo(run2.Count));
        for (int i = 0; i < run1.Count; i++)
        {
            Assert.That(run1[i].SourceType, Is.EqualTo(run2[i].SourceType));
            Assert.That(run1[i].ForbiddenReferences, Is.EqualTo(run2[i].ForbiddenReferences));
        }
    }

    [Test]
    public void CheckContract_TransitiveMode_SourceTypesAreSortedGlobally()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["execution"] = new() { Namespace = "ArchLinterNet.Core.Execution" },
                ["contracts"] = new() { Namespace = "ArchLinterNet.Core.Contracts" }
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
                        Name = "execution-must-not-depend-on-contracts",
                        Source = "execution",
                        Forbidden = new List<string> { "contracts" },
                        DependencyDepth = DependencyDepthMode.Transitive
                    }
                }
            }
        };

        var context = CreateContext();
        var runner = new ArchitectureContractRunner(context, document);
        var violations = runner.CheckContract(document.Contracts.Strict[0]).ToList();

        Assert.That(violations.Count, Is.GreaterThan(1));
        for (int i = 1; i < violations.Count; i++)
        {
            Assert.That(
                string.Compare(violations[i - 1].SourceType, violations[i].SourceType, StringComparison.Ordinal),
                Is.LessThanOrEqualTo(0),
                $"Violations should be sorted by SourceType: '{violations[i - 1].SourceType}' should come before '{violations[i].SourceType}'");
        }
    }
}
