using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Covers the identity seam: source-set expansion derives per-instance contract ids, and the
// authored id a policy author actually wrote must keep selecting and covering every instance.
[TestFixture]
public sealed class SourceExpansionIdentityTests
{
    private const string FixtureRoot = "ArchLinterNet.Core.Tests.RuleInputCoverageFixtures";

    private static ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext(
            repositoryRoot: AppContext.BaseDirectory,
            targetAssemblies: new[] { typeof(SourceExpansionIdentityTests).Assembly },
            missingAssemblyNames: Array.Empty<string>(),
            assemblyProbingPaths: Array.Empty<string>());
    }

    // Mirrors what ArchitectureSourceSetExpander produces for
    // `strict_external` + `source_sets: [inner_layers]`, without re-running the loader.
    private static ArchitectureContractDocument CreateExpandedDocument()
    {
        ArchitectureContractDocument document = new();
        document.Layers["audio"] = new ArchitectureLayer { Namespace = $"{FixtureRoot}.Audio" };
        document.Layers["video"] = new ArchitectureLayer { Namespace = $"{FixtureRoot}.Video" };
        document.ExternalDependencies["vendor"] = new ArchitectureExternalDependencyGroup
        {
            NamespacePrefixes = { "Vendor" }
        };

        foreach (string layer in new[] { "audio", "video" })
        {
            document.Contracts.StrictExternal.Add(new ArchitectureExternalDependencyContract
            {
                Name = "inner layers avoid vendor",
                Id = $"inner-no-vendor/{layer}",
                Source = layer,
                Forbidden = { "vendor" },
                Reason = "Inner layers must not reference vendor APIs.",
                ExpansionOrigin = new ArchitectureSourceExpansionOrigin(
                    "inner-no-vendor", "inner layers avoid vendor", layer, "inner_layers", layer)
            });
        }

        document.SourceExpansion = new ArchitectureSourceExpansionInventory(
            [
                new ArchitectureSourceSetResolution(
                    "inner_layers", ArchitectureSourceSetKind.Layer, ["audio", "video"], false, string.Empty)
            ],
            [
                new ArchitectureContractExpansion(
                    "strict_external",
                    "inner-no-vendor",
                    "inner layers avoid vendor",
                    ["inner_layers"],
                    [
                        new ArchitectureExpandedContractInstance("inner-no-vendor/audio", "audio", "inner_layers", "audio"),
                        new ArchitectureExpandedContractInstance("inner-no-vendor/video", "video", "inner_layers", "video")
                    ])
            ]);

        return document;
    }

    private static ArchitectureCoverageContract CreateRuleInputContract(
        string referencedContractId,
        ArchitectureCoverageExclusion? exclusion = null)
    {
        ArchitectureCoverageContract contract = new()
        {
            Name = "rule-input-coverage",
            Id = "rule-input-coverage",
            Scope = "rule_input",
            Reason = "Flag rules whose source layers stop matching any code.",
            ContractIds = { referencedContractId }
        };

        if (exclusion != null)
        {
            contract.Exclude.Add(exclusion);
        }

        return contract;
    }

    [Test]
    public void SelectingAuthoredId_SelectsEveryExpandedInstance()
    {
        ArchitectureContractDocument document = CreateExpandedDocument();
        ArchitectureAnalysisSession session = new(
            CreateContext(),
            document,
            selectedContractIds: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "inner-no-vendor" },
            enableUnmatchedIgnoreTracking: false,
            preprocessorSymbols: null);

        Assert.Multiple(() =>
        {
            Assert.That(document.Contracts.StrictExternal.All(session.IsContractSelected), Is.True);

            // The derived instance id alone is not in the selection — only the contract-aware
            // overload's expansion-origin fallback makes these instances selected.
            Assert.That(session.IsContractSelected(document.Contracts.StrictExternal[0].Id), Is.False);
        });
    }

    [Test]
    public void SelectingUnrelatedId_SelectsNoExpandedInstance()
    {
        ArchitectureContractDocument document = CreateExpandedDocument();
        ArchitectureAnalysisSession session = new(
            CreateContext(),
            document,
            selectedContractIds: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "some-other-contract" },
            enableUnmatchedIgnoreTracking: false,
            preprocessorSymbols: null);

        Assert.That(document.Contracts.StrictExternal.Any(session.IsContractSelected), Is.False);
    }

    [Test]
    public void EmptySelection_SelectsEveryContract()
    {
        ArchitectureContractDocument document = CreateExpandedDocument();
        ArchitectureAnalysisSession session = new(
            CreateContext(),
            document,
            selectedContractIds: null,
            enableUnmatchedIgnoreTracking: false,
            preprocessorSymbols: null);

        Assert.That(document.Contracts.StrictExternal.All(session.IsContractSelected), Is.True);
    }

    [Test]
    public void RuleInputCoverage_ReferencingAuthoredId_CoversEveryExpandedInstance()
    {
        ArchitectureContractDocument document = CreateExpandedDocument();
        ArchitectureContractRunner runner = new(CreateContext(), document);

        ArchitectureCoverageSummary summary = runner.BuildCoverageSummary(CreateRuleInputContract("inner-no-vendor"))!;

        Assert.Multiple(() =>
        {
            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.CoveredItems.Select(item => item.Item), Is.EqualTo(new[]
            {
                "inner-no-vendor/audio:audio",
                "inner-no-vendor/video:video"
            }));
            Assert.That(summary.Counts.Covered, Is.EqualTo(2));
            Assert.That(summary.Counts.Excluded, Is.Zero);
        });
    }

    [Test]
    public void RuleInputCoverage_ExcludingAuthoredId_ExcludesEveryExpandedInstance()
    {
        ArchitectureContractDocument document = CreateExpandedDocument();
        ArchitectureContractRunner runner = new(CreateContext(), document);

        ArchitectureCoverageContract contract = CreateRuleInputContract(
            "inner-no-vendor",
            new ArchitectureCoverageExclusion
            {
                ContractId = "inner-no-vendor",
                Reason = "The whole expanded rule is reviewed elsewhere."
            });

        ArchitectureCoverageSummary summary = runner.BuildCoverageSummary(contract)!;

        Assert.Multiple(() =>
        {
            Assert.That(summary.Counts.Excluded, Is.EqualTo(2));
            Assert.That(summary.Counts.Covered, Is.Zero);
            Assert.That(summary.ExcludedItems.Select(item => item.Item), Is.EqualTo(new[]
            {
                "inner-no-vendor/audio",
                "inner-no-vendor/video"
            }));
        });
    }

    [Test]
    public void RuleInputCoverage_ReferencingOneInstanceId_CoversOnlyThatInstance()
    {
        ArchitectureContractDocument document = CreateExpandedDocument();
        ArchitectureContractRunner runner = new(CreateContext(), document);

        ArchitectureCoverageSummary summary =
            runner.BuildCoverageSummary(CreateRuleInputContract("inner-no-vendor/audio"))!;

        Assert.That(summary.CoveredItems.Select(item => item.Item),
            Is.EqualTo(new[] { "inner-no-vendor/audio:audio" }));
    }

    [Test]
    public void CoverageValidator_AcceptsAuthoredIdOfAnExpandedContract()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"arch-linter-expansion-coverage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "dependencies.arch.yml");
            File.WriteAllText(path, """
                version: 1
                name: Test
                layers:
                  application:
                    namespace: Acme.Application
                  domain:
                    namespace: Acme.Domain
                analysis:
                  target_assemblies: [Acme.Host]
                source_sets:
                  inner_layers:
                    kind: layer
                    members: [domain, application]
                external_dependencies:
                  vendor:
                    namespace_prefixes: [Vendor]
                contracts:
                  strict_external:
                    - name: inner layers avoid vendor
                      id: inner-no-vendor
                      source_sets: [inner_layers]
                      forbidden: [vendor]
                  strict_coverage:
                    - name: rule input coverage
                      id: rule-input-coverage
                      scope: rule_input
                      contract_ids: [inner-no-vendor]
                      reason: Flag rules whose source layers stop matching any code.
                """);

            ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(path);

            Assert.That(document.Contracts.StrictCoverage.Single().ContractIds,
                Is.EqualTo(new[] { "inner-no-vendor" }));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void SourceExpansion_ExcludesExplicitAndSetResolvedSourcesWithoutExpandingTheUniverse()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"arch-linter-expansion-exclusion-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "dependencies.arch.yml");
            File.WriteAllText(path, """
                version: 1
                name: Test
                layers:
                  application:
                    namespace: Acme.Application
                  domain:
                    namespace: Acme.Domain
                  infrastructure:
                    namespace: Acme.Infrastructure
                source_sets:
                  inner_layers:
                    kind: layer
                    members: [application, domain]
                  omitted_layers:
                    kind: layer
                    members: [domain]
                external_dependencies:
                  vendor:
                    namespace_prefixes: [Vendor]
                contracts:
                  strict_external:
                    - name: inner layers avoid vendor
                      id: inner-no-vendor
                      source_sets: [inner_layers]
                      exclude_sources: [infrastructure]
                      exclude_source_sets: [omitted_layers]
                      forbidden: [vendor]
                """);

            ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(path);

            Assert.That(document.Contracts.StrictExternal.Select(contract => contract.Source),
                Is.EqualTo(new[] { "application" }));
            Assert.That(document.SourceExpansion.Contracts.Single().Exclusions.Select(exclusion => (exclusion.Source, exclusion.Matched)),
                Is.EqualTo(new[] { ("infrastructure", false), ("domain", true) }));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void SourceExpansion_AllResolvedSourcesExcluded_ProducesEmptyExpansionWithoutLoadError()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"arch-linter-expansion-empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "dependencies.arch.yml");
            File.WriteAllText(path, """
                version: 1
                name: Test
                layers:
                  application:
                    namespace: Acme.Application
                  domain:
                    namespace: Acme.Domain
                source_sets:
                  inner_layers:
                    kind: layer
                    members: [application, domain]
                external_dependencies:
                  vendor:
                    namespace_prefixes: [Vendor]
                contracts:
                  strict_external:
                    - name: inner layers avoid vendor
                      id: inner-no-vendor
                      source_sets: [inner_layers]
                      exclude_source_sets: [inner_layers]
                      forbidden: [vendor]
                """);

            ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(path);
            ArchitectureContractExpansion expansion = document.SourceExpansion.Contracts.Single();

            Assert.Multiple(() =>
            {
                Assert.That(document.Contracts.StrictExternal, Is.Empty);
                Assert.That(expansion.Instances, Is.Empty);
                Assert.That(expansion.OptionalEmpty, Is.False);
                Assert.That(expansion.Exclusions.All(exclusion => exclusion.Matched), Is.True);
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void SourceExpansion_OverlappingExclusions_BothReportMatched()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"arch-linter-expansion-overlap-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "dependencies.arch.yml");
            File.WriteAllText(path, """
                version: 1
                name: Test
                layers:
                  application:
                    namespace: Acme.Application
                  domain:
                    namespace: Acme.Domain
                  infrastructure:
                    namespace: Acme.Infrastructure
                source_sets:
                  inner_layers:
                    kind: layer
                    members: [application, domain, infrastructure]
                  legacy_layers:
                    kind: layer
                    members: [infrastructure]
                external_dependencies:
                  vendor:
                    namespace_prefixes: [Vendor]
                contracts:
                  strict_external:
                    - name: inner layers avoid vendor
                      id: inner-no-vendor
                      source_sets: [inner_layers]
                      exclude_sources: [infrastructure]
                      exclude_source_sets: [legacy_layers]
                      forbidden: [vendor]
                """);

            ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(path);
            ArchitectureContractExpansion expansion = document.SourceExpansion.Contracts.Single();

            // Both exclusions target 'infrastructure'; the first (exclude_sources) removes it from
            // the live selector set, but the second (exclude_source_sets, resolving to the same
            // source) must still be reported as matched rather than stale, since it too excludes a
            // source that was genuinely part of the included scope.
            Assert.That(expansion.Exclusions.Select(exclusion => (exclusion.Source, exclusion.Matched)),
                Is.EqualTo(new[] { ("infrastructure", true), ("infrastructure", true) }));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void SourceExpansion_OptionalEmptyExcludedSet_RecordsExclusionEvidence()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"arch-linter-expansion-optional-exclude-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "dependencies.arch.yml");
            File.WriteAllText(path, """
                version: 1
                name: Test
                layers:
                  application:
                    namespace: Acme.Application
                  domain:
                    namespace: Acme.Domain
                source_sets:
                  inner_layers:
                    kind: layer
                    members: [application, domain]
                  not_yet_extracted:
                    kind: layer
                    globs: [messaging.*]
                    optional: true
                    reason: Reserved for a module not extracted yet.
                external_dependencies:
                  vendor:
                    namespace_prefixes: [Vendor]
                contracts:
                  strict_external:
                    - name: inner layers avoid vendor
                      id: inner-no-vendor
                      source_sets: [inner_layers]
                      exclude_source_sets: [not_yet_extracted]
                      forbidden: [vendor]
                """);

            ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(path);
            ArchitectureContractExpansion expansion = document.SourceExpansion.Contracts.Single();
            ArchitectureExpandedContractExclusion exclusion = expansion.Exclusions.Single();

            // An excluded set that resolves to nothing must still surface as evidence - not vanish
            // from the exclusion list the way it silently did before.
            Assert.Multiple(() =>
            {
                Assert.That(exclusion.SetName, Is.EqualTo("not_yet_extracted"));
                Assert.That(exclusion.Source, Is.Null);
                Assert.That(exclusion.Matched, Is.False);
                Assert.That(exclusion.OptionalEmpty, Is.True);
                Assert.That(exclusion.OptionalReason, Does.Contain("not extracted yet"));
                Assert.That(document.Contracts.StrictExternal.Select(contract => contract.Source),
                    Is.EqualTo(new[] { "application", "domain" }));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void SourceExpansion_InstancesCarryItemLevelPolicyLocation()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"arch-linter-expansion-location-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "dependencies.arch.yml");
            File.WriteAllText(path, """
                version: 1
                name: Test
                layers:
                  application:
                    namespace: Acme.Application
                  domain:
                    namespace: Acme.Domain
                source_sets:
                  inner_layers:
                    kind: layer
                    members: [domain]
                external_dependencies:
                  vendor:
                    namespace_prefixes: [Vendor]
                contracts:
                  strict_external:
                    - name: inner layers avoid vendor
                      id: inner-no-vendor
                      sources: [application]
                      source_sets: [inner_layers]
                      forbidden: [vendor]
                """);

            ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(path);
            ArchitectureContractExpansion expansion = document.SourceExpansion.Contracts.Single();

            Assert.Multiple(() =>
            {
                Assert.That(expansion.Instances.Select(instance => instance.Source),
                    Is.EqualTo(new[] { "application", "domain" }));
                Assert.That(expansion.Instances.All(instance => instance.PolicyLocation != null), Is.True,
                    "Every included instance must carry the authored location it came from: the " +
                    "matching 'sources[i]' entry for an explicit source, or the referenced source " +
                    "set's own declaration for a resolved member.");
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
