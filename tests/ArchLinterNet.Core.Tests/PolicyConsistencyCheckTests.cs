using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using AttributeRoleExtractionTestFixtures;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class PolicyConsistencyCheckTests
{
    private static readonly string[] _domainApplication = { "domain", "application" };
    private static readonly string[] _semanticLayers = { "semantic_a", "semantic_b" };
    private static readonly string[] _coreLayer = { "core" };

    private static ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(ArchitecturePolicyDocumentLoader).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static ArchitectureContractDocument BaseDocument()
    {
        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["domain"] = new() { Namespace = "Test.Domain" },
                ["application"] = new() { Namespace = "Test.Application" },
                ["infrastructure"] = new() { Namespace = "Test.Infrastructure" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" }
            },
            Contracts = new ArchitectureContractGroups()
        };
    }

    [Test]
    public void GreenPolicy_NoFindings()
    {
        var document = BaseDocument();
        document.Contracts.Strict = new List<ArchitectureDependencyContract>
        {
            new() { Name = "domain-no-infra", Source = "domain", Forbidden = new List<string> { "infrastructure" } }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var findings = runner.CheckPolicyConsistency();

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void DuplicateExplicitId_Detected()
    {
        var document = BaseDocument();
        document.Contracts.Strict = new List<ArchitectureDependencyContract>
        {
            new() { Name = "first", Id = "shared-id", Source = "domain", Forbidden = new List<string> { "infrastructure" } }
        };
        document.Contracts.Audit = new List<ArchitectureDependencyContract>
        {
            new() { Name = "second", Id = "shared-id", Source = "application", Forbidden = new List<string> { "infrastructure" } }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var findings = runner.CheckPolicyConsistency();

        var finding = findings.FirstOrDefault(f => f.CheckKind == "duplicate-id");
        Assert.That(finding, Is.Not.Null);
        Assert.That(finding!.ConflictingContractNames, Is.EquivalentTo(new[] { "first", "second" }));
        Assert.That(finding.ConflictingContractIds, Is.EquivalentTo(new[] { "shared-id" }));
    }

    [Test]
    public void DuplicateExpandedTemplateId_Detected()
    {
        var document = BaseDocument();
        document.Contracts.StrictLayerTemplates = new List<ArchitectureLayerTemplateContract>
        {
            new()
            {
                Name = "feature-template",
                Containers = new List<string> { "Test.Domain" },
                Layers = new List<ArchitectureTemplateLayer>
                {
                    new() { Name = "Inner" }
                }
            }
        };
        document.Contracts.AuditLayerTemplates = new List<ArchitectureLayerTemplateContract>
        {
            new()
            {
                Name = "feature-template",
                Containers = new List<string> { "Test.Domain" },
                Layers = new List<ArchitectureTemplateLayer>
                {
                    new() { Name = "Inner" }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var findings = runner.CheckPolicyConsistency();

        Assert.That(findings.Any(f => f.CheckKind == "duplicate-id"), Is.True);
    }

    [Test]
    public void AllowOnlyVsForbidden_DirectConflict_Detected()
    {
        var document = BaseDocument();
        document.Contracts.StrictAllowOnly = new List<ArchitectureAllowOnlyContract>
        {
            new() { Name = "domain-allows-application", Source = "domain", Allowed = new List<string> { "application" } }
        };
        document.Contracts.Strict = new List<ArchitectureDependencyContract>
        {
            new() { Name = "domain-forbids-application", Source = "domain", Forbidden = new List<string> { "application" } }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var findings = runner.CheckPolicyConsistency();

        var finding = findings.FirstOrDefault(f => f.CheckKind == "allow-forbid-conflict");
        Assert.That(finding, Is.Not.Null);
        Assert.That(finding!.Layers, Is.EquivalentTo(_domainApplication));
        Assert.That(finding.ConflictingContractNames,
            Is.EquivalentTo(new[] { "domain-allows-application", "domain-forbids-application" }));
    }

    [Test]
    public void IndependenceVsExplicitAllowedDependency_Conflict_Detected()
    {
        var document = BaseDocument();
        document.Contracts.StrictIndependence = new List<ArchitectureIndependenceContract>
        {
            new() { Name = "domain-app-independent", Layers = new List<string> { "domain", "application" } }
        };
        document.Contracts.StrictAllowOnly = new List<ArchitectureAllowOnlyContract>
        {
            new() { Name = "domain-allows-application", Source = "domain", Allowed = new List<string> { "application" } }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var findings = runner.CheckPolicyConsistency();

        var finding = findings.FirstOrDefault(f => f.CheckKind == "independence-conflict");
        Assert.That(finding, Is.Not.Null);
        Assert.That(finding!.ConflictingContractNames,
            Is.EquivalentTo(new[] { "domain-app-independent", "domain-allows-application" }));
    }

    [Test]
    public void IndependenceVsExpandedLayerTemplateOrder_NamedLayers_Conflict_Detected()
    {
        var document = BaseDocument();
        document.Layers["feature_domain"] = new ArchitectureLayer { Namespace = "MyApp.Feature.Domain" };
        document.Layers["feature_application"] = new ArchitectureLayer { Namespace = "MyApp.Feature.Application" };
        document.Contracts.StrictIndependence = new List<ArchitectureIndependenceContract>
        {
            new()
            {
                Name = "feature-domain-app-independent",
                Layers = new List<string> { "feature_domain", "feature_application" }
            }
        };
        document.Contracts.StrictLayerTemplates = new List<ArchitectureLayerTemplateContract>
        {
            new()
            {
                Name = "feature-stack",
                Containers = new List<string> { "MyApp.Feature" },
                Layers = new List<ArchitectureTemplateLayer>
                {
                    new() { Name = "Domain" },
                    new() { Name = "Application" }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var findings = runner.CheckPolicyConsistency();

        var finding = findings.FirstOrDefault(f => f.CheckKind == "independence-conflict");
        Assert.That(finding, Is.Not.Null);
        Assert.That(finding!.ConflictingContractNames, Contains.Item("feature-domain-app-independent"));
        Assert.That(finding.Layers, Is.EquivalentTo(new[] { "feature_domain", "feature_application" }));
    }

    [Test]
    public void ProtectedImporter_ConflictsWithStrictForbid_Detected()
    {
        var document = BaseDocument();
        document.Contracts.StrictProtected = new List<ArchitectureProtectedContract>
        {
            new()
            {
                Name = "domain-protected",
                Protected = new List<string> { "domain" },
                AllowedImporters = new List<string> { "application" }
            }
        };
        document.Contracts.Strict = new List<ArchitectureDependencyContract>
        {
            new() { Name = "application-forbids-domain", Source = "application", Forbidden = new List<string> { "domain" } }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var findings = runner.CheckPolicyConsistency();

        var finding = findings.FirstOrDefault(f => f.CheckKind == "protected-importer-conflict");
        Assert.That(finding, Is.Not.Null);
        Assert.That(finding!.Layers, Is.EquivalentTo(_domainApplication));
    }

    [Test]
    public void ProtectedImporter_ConflictsWithAnotherProtectedContract_Detected()
    {
        var document = BaseDocument();
        document.Contracts.StrictProtected = new List<ArchitectureProtectedContract>
        {
            new()
            {
                Name = "domain-protected-allows-application",
                Protected = new List<string> { "domain" },
                AllowedImporters = new List<string> { "application" }
            },
            new()
            {
                Name = "domain-protected-no-application",
                Protected = new List<string> { "domain" },
                AllowedImporters = new List<string>()
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var findings = runner.CheckPolicyConsistency();

        var finding = findings.FirstOrDefault(f => f.CheckKind == "protected-importer-conflict"
            && f.ConflictingContractNames.Contains("domain-protected-no-application"));
        Assert.That(finding, Is.Not.Null);
        Assert.That(finding!.Layers, Is.EquivalentTo(_domainApplication));
        Assert.That(finding.ConflictingContractNames,
            Is.EquivalentTo(new[] { "domain-protected-allows-application", "domain-protected-no-application" }));
    }

    [Test]
    public void ProtectedImporter_SameAllowedImportersOnBothSurfaces_NotFlagged()
    {
        var document = BaseDocument();
        document.Contracts.StrictProtected = new List<ArchitectureProtectedContract>
        {
            new()
            {
                Name = "domain-protected-a",
                Protected = new List<string> { "domain" },
                AllowedImporters = new List<string> { "application" }
            },
            new()
            {
                Name = "domain-protected-b",
                Protected = new List<string> { "domain" },
                AllowedImporters = new List<string> { "application" }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var findings = runner.CheckPolicyConsistency();

        Assert.That(findings.Any(f => f.CheckKind == "protected-importer-conflict"), Is.False);
    }

    [Test]
    public void LayerOverlap_TwoInternalLayersMatchSameType_Detected()
    {
        var document = BaseDocument();
        document.Layers["core"] = new ArchitectureLayer { Namespace = "ArchLinterNet.Core.Model" };
        document.Layers["core-sibling"] = new ArchitectureLayer { Namespace = "ArchLinterNet.Core.Model" };
        document.Contracts.StrictLayers = new List<ArchitectureLayerContract>
        {
            new() { Name = "noop", Layers = new List<string> { "core" } }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var findings = runner.CheckPolicyConsistency();

        var finding = findings.FirstOrDefault(f => f.CheckKind == "layer-overlap");
        Assert.That(finding, Is.Not.Null);
        Assert.That(finding!.Layers, Is.EquivalentTo(new[] { "core", "core-sibling" }));
        Assert.That(finding.RepresentativeType, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void LayerOverlap_InternalVsExternal_NotFlagged()
    {
        var document = BaseDocument();
        document.Layers["core"] = new ArchitectureLayer { Namespace = "ArchLinterNet.Core.Model" };
        document.Layers["core-external"] = new ArchitectureLayer { Namespace = "ArchLinterNet.Core.Model", External = true };
        document.Contracts.StrictLayers = new List<ArchitectureLayerContract>
        {
            new() { Name = "noop", Layers = new List<string> { "core" } }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var findings = runner.CheckPolicyConsistency();

        Assert.That(findings.Any(f => f.CheckKind == "layer-overlap"), Is.False);
    }

    [Test]
    public void LayerOverlap_ContainmentHierarchy_NotFlagged()
    {
        var document = BaseDocument();
        document.Layers["core"] = new ArchitectureLayer { Namespace = "ArchLinterNet.Core" };
        document.Layers["core-model"] = new ArchitectureLayer { Namespace = "ArchLinterNet.Core.Model" };
        document.Contracts.StrictLayers = new List<ArchitectureLayerContract>
        {
            new() { Name = "noop", Layers = new List<string> { "core" } }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var findings = runner.CheckPolicyConsistency();

        Assert.That(findings.Any(f => f.CheckKind == "layer-overlap"), Is.False);
    }

    [Test]
    public void LayerOverlap_DisjointCombinedSelectorsInSameNamespace_NotFlagged()
    {
        var document = BaseDocument();
        document.Classification = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute",
                    Role = "DomainLayer",
                    Metadata = new Dictionary<string, object>
                    {
                        ["enabled"] = "property:Enabled",
                        ["tier"] = "property:Tier"
                    }
                }
            }
        };
        document.Layers["enabled_domain"] = new ArchitectureLayer
        {
            Namespace = "AttributeRoleExtractionTestFixtures",
            Selector = new ArchitectureLayerSelector
            {
                Role = "DomainLayer",
                Metadata = new Dictionary<string, object> { ["enabled"] = true }
            }
        };
        document.Layers["tiered_domain"] = new ArchitectureLayer
        {
            Namespace = "AttributeRoleExtractionTestFixtures",
            Selector = new ArchitectureLayerSelector
            {
                Role = "DomainLayer",
                Metadata = new Dictionary<string, object> { ["tier"] = "Domain" }
            }
        };
        document.Contracts.StrictLayers = new List<ArchitectureLayerContract>
        {
            new() { Name = "noop", Layers = new List<string> { "enabled_domain" } }
        };

        var runner = new ArchitectureContractRunner(
            new ArchitectureAnalysisContext(
                "/tmp",
                new[] { typeof(TypeWithBooleanProperty).Assembly },
                Array.Empty<string>(),
                Array.Empty<string>()),
            document);
        var findings = runner.CheckPolicyConsistency();

        Assert.That(findings.Any(f => f.CheckKind == "layer-overlap"), Is.False);
    }

    [Test]
    public void LayerOverlap_SelectorOnlyLayersMatchingSameType_AreDetected()
    {
        var document = BaseDocument();
        document.Classification = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute",
                    Role = "DomainLayer",
                    Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" }
                }
            }
        };
        document.Layers["semantic_a"] = new ArchitectureLayer
        {
            Selector = new ArchitectureLayerSelector { Role = "DomainLayer" }
        };
        document.Layers["semantic_b"] = new ArchitectureLayer
        {
            Selector = new ArchitectureLayerSelector
            {
                Role = "DomainLayer",
                Metadata = new Dictionary<string, object> { ["domain"] = "Sales" }
            }
        };
        document.Contracts.StrictLayers = new List<ArchitectureLayerContract>
        {
            new() { Name = "noop", Layers = new List<string> { "semantic_a" } }
        };

        var runner = new ArchitectureContractRunner(
            new ArchitectureAnalysisContext(
                "/tmp",
                new[] { typeof(TypeWithConstructorDefault).Assembly },
                Array.Empty<string>(),
                Array.Empty<string>()),
            document);
        var findings = runner.CheckPolicyConsistency();

        var finding = findings.FirstOrDefault(f => f.CheckKind == "layer-overlap");
        Assert.That(finding, Is.Not.Null);
        Assert.That(finding!.Layers, Is.EquivalentTo(_semanticLayers));
    }

    [Test]
    public void UnreachableContract_StructurallyImpossibleLayer_Detected()
    {
        var document = BaseDocument();
        document.Layers["impossible"] = new ArchitectureLayer { Namespace = "Test.Domain", NamespaceSuffix = "   " };
        document.Contracts.Strict = new List<ArchitectureDependencyContract>
        {
            new() { Name = "uses-impossible", Source = "impossible", Forbidden = new List<string> { "application" } }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var findings = runner.CheckPolicyConsistency();

        var finding = findings.FirstOrDefault(f => f.CheckKind == "unreachable-contract");
        Assert.That(finding, Is.Not.Null);
        Assert.That(finding!.Layers, Is.EquivalentTo(new[] { "impossible" }));
    }

    [Test]
    public void UnreachableContract_SelectorOnlyLayer_IsNotFlagged()
    {
        var document = BaseDocument();
        document.Layers["semantic"] = new ArchitectureLayer
        {
            Selector = new ArchitectureLayerSelector { Role = "DomainLayer" }
        };
        document.Contracts.Strict = new List<ArchitectureDependencyContract>
        {
            new() { Name = "uses-selector", Source = "semantic", Forbidden = new List<string> { "application" } }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var findings = runner.CheckPolicyConsistency();

        Assert.That(findings.Any(f => f.CheckKind == "unreachable-contract"), Is.False);
    }

    [Test]
    public void StrictAndAuditFamilies_DuplicateIdsDetectedAcrossFamilies()
    {
        var document = BaseDocument();
        document.Contracts.StrictAllowOnly = new List<ArchitectureAllowOnlyContract>
        {
            new() { Name = "strict-allow", Id = "cross-family", Source = "domain", Allowed = new List<string> { "application" } }
        };
        document.Contracts.AuditCycles = new List<ArchitectureCycleContract>
        {
            new() { Name = "audit-cycle", Id = "cross-family", Layers = new List<string> { "domain", "application" } }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var findings = runner.CheckPolicyConsistency();

        Assert.That(findings.Any(f => f.CheckKind == "duplicate-id"
            && f.ConflictingContractNames.Contains("strict-allow")
            && f.ConflictingContractNames.Contains("audit-cycle")), Is.True);
    }

    [Test]
    public void StrictAndAuditFamilies_AllowForbidConflictAcrossFamilies()
    {
        var document = BaseDocument();
        document.Contracts.AuditAllowOnly = new List<ArchitectureAllowOnlyContract>
        {
            new() { Name = "audit-allow", Source = "domain", Allowed = new List<string> { "application" } }
        };
        document.Contracts.Strict = new List<ArchitectureDependencyContract>
        {
            new() { Name = "strict-forbid", Source = "domain", Forbidden = new List<string> { "application" } }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var findings = runner.CheckPolicyConsistency();

        Assert.That(findings.Any(f => f.CheckKind == "allow-forbid-conflict"), Is.True);
    }

    [Test]
    public void Determinism_RepeatedRuns_ProduceIdenticalOrderedFindings()
    {
        var document = BaseDocument();
        document.Contracts.Strict = new List<ArchitectureDependencyContract>
        {
            new() { Name = "first", Id = "dup", Source = "domain", Forbidden = new List<string> { "infrastructure" } }
        };
        document.Contracts.Audit = new List<ArchitectureDependencyContract>
        {
            new() { Name = "second", Id = "dup", Source = "application", Forbidden = new List<string> { "infrastructure" } }
        };
        document.Contracts.StrictAllowOnly = new List<ArchitectureAllowOnlyContract>
        {
            new() { Name = "allow", Source = "domain", Allowed = new List<string> { "infrastructure" } }
        };

        List<PolicyConsistencyDiagnostic> RunOnce()
        {
            var runner = new ArchitectureContractRunner(CreateContext(), document);
            return runner.CheckPolicyConsistency();
        }

        var first = RunOnce();
        var second = RunOnce();

        Assert.That(first.Count, Is.EqualTo(second.Count));
        for (int i = 0; i < first.Count; i++)
        {
            Assert.That(first[i].CheckKind, Is.EqualTo(second[i].CheckKind));
            Assert.That(first[i].Reason, Is.EqualTo(second[i].Reason));
        }
    }

    [Test]
    public void UnmatchedLayerExclusion_TypoedPattern_Detected()
    {
        var document = BaseDocument();
        document.Layers["core"] = new ArchitectureLayer
        {
            Namespace = "ArchLinterNet.Core.Contracts.*",
            Exclude = new List<ArchitectureLayerExclusion>
            {
                // Real sub-namespace under ArchLinterNet.Core.Contracts is "Families", not "Familias".
                new() { Namespace = "ArchLinterNet.Core.Contracts.Familias" }
            }
        };
        document.Contracts.StrictLayers = new List<ArchitectureLayerContract>
        {
            new() { Name = "noop", Layers = new List<string> { "core" } }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var findings = runner.CheckPolicyConsistency();

        var finding = findings.FirstOrDefault(f => f.CheckKind == "unmatched-layer-exclusion");
        Assert.That(finding, Is.Not.Null);
        Assert.That(finding!.Layers, Is.EquivalentTo(_coreLayer));
        Assert.That(finding.Reason, Does.Contain("ArchLinterNet.Core.Contracts.Familias"));
    }

    [Test]
    public void UnmatchedLayerExclusion_MatchingPattern_NotFlagged()
    {
        var document = BaseDocument();
        document.Layers["core"] = new ArchitectureLayer
        {
            Namespace = "ArchLinterNet.Core.Contracts.*",
            Exclude = new List<ArchitectureLayerExclusion>
            {
                new() { Namespace = "ArchLinterNet.Core.Contracts.Families" }
            }
        };
        document.Contracts.StrictLayers = new List<ArchitectureLayerContract>
        {
            new() { Name = "noop", Layers = new List<string> { "core" } }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var findings = runner.CheckPolicyConsistency();

        Assert.That(findings.Any(f => f.CheckKind == "unmatched-layer-exclusion"), Is.False);
    }

    [Test]
    public void UnmatchedLayerExclusion_OverlappingExcludeEntries_NeitherFlaggedUnmatched()
    {
        // Regression for PR #384 review: FindUnmatchedLayerExclusions must test every exclude
        // entry independently, not stop at the first entry that matches a given namespace - a
        // "first wins" scan would leave a later, still-matching entry looking unused even though
        // it also matched. Two entries here match exactly the same namespaces.
        var document = BaseDocument();
        document.Layers["core"] = new ArchitectureLayer
        {
            Namespace = "ArchLinterNet.Core.Contracts.*",
            Exclude = new List<ArchitectureLayerExclusion>
            {
                new() { Namespace = "ArchLinterNet.Core.Contracts.Families" },
                new() { Namespace = "ArchLinterNet.Core.Contracts.Families" }
            }
        };
        document.Contracts.StrictLayers = new List<ArchitectureLayerContract>
        {
            new() { Name = "noop", Layers = new List<string> { "core" } }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var findings = runner.CheckPolicyConsistency();

        Assert.That(findings.Any(f => f.CheckKind == "unmatched-layer-exclusion"), Is.False);
    }

    [Test]
    public void UnmatchedLayerExclusion_NoExcludeEntries_NotFlagged()
    {
        var document = BaseDocument();
        document.Layers["core"] = new ArchitectureLayer { Namespace = "ArchLinterNet.Core.Contracts.*" };
        document.Contracts.StrictLayers = new List<ArchitectureLayerContract>
        {
            new() { Name = "noop", Layers = new List<string> { "core" } }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var findings = runner.CheckPolicyConsistency();

        Assert.That(findings.Any(f => f.CheckKind == "unmatched-layer-exclusion"), Is.False);
    }
}
