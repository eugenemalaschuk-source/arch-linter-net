using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Canonical-identity regression coverage for policy-consistency findings (#683, PR #686 review
// rounds 2-3): distinct occurrences must get distinct identities, and an identity must depend only
// on a finding's own semantic content — never on a position in a YAML list the policy author can
// reorder without changing meaning. Split out of PolicyConsistencyCheckTests.cs to keep both files
// under the repository's 800-line hard limit.
public sealed partial class PolicyConsistencyCheckTests
{
    [Test]
    public void IndependenceConflict_TwoConflictingContractsSharingDuplicateId_GetDistinctIdentities()
    {
        // #686 PR review round 2: two independence-conflict findings against the same independence
        // contract, over the same layer pair, from two allow-only contracts that happen to share a
        // duplicate id (FindDuplicateContractIds treats this as a valid, expected analyzer input —
        // it is itself a check for exactly this policy state). Both findings end up with identical
        // Layers and identical ConflictingContractIds; only ConflictingContractNames differs.
        var document = BaseDocument();
        document.Contracts.StrictIndependence = new List<ArchitectureIndependenceContract>
        {
            new() { Name = "domain-app-independent", Id = "independence-id", Layers = new List<string> { "domain", "application" } }
        };
        document.Contracts.StrictAllowOnly = new List<ArchitectureAllowOnlyContract>
        {
            new() { Name = "allow-a", Id = "duplicate", Source = "domain", Allowed = new List<string> { "application" } }
        };
        document.Contracts.AuditAllowOnly = new List<ArchitectureAllowOnlyContract>
        {
            new() { Name = "allow-b", Id = "duplicate", Source = "domain", Allowed = new List<string> { "application" } }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var findings = runner.CheckPolicyConsistency()
            .Where(f => f.CheckKind == "independence-conflict")
            .ToList();

        Assert.That(findings, Has.Count.EqualTo(2));
        Assert.That(findings.Select(f => f.ConflictingContractIds), Has.All.EquivalentTo(new[] { "independence-id", "duplicate" }));
        Assert.That(findings.Select(f => string.Join(",", f.ConflictingContractNames)).Distinct().Count(), Is.EqualTo(2));

        string[] identities = findings
            .Select(finding => ArchitectureFindingMapper.FromDiagnostic(finding).CanonicalIdentity)
            .ToArray();
        Assert.That(identities.Distinct().Count(), Is.EqualTo(2));
    }

    [Test]
    public void IndependenceConflict_ReorderingIndependenceContractsInRealYaml_DoesNotChangeEitherIdentity()
    {
        // #686 PR review round 3: ArchitecturePolicyProvenanceIndex.Enrich attaches a PolicyLocation
        // to every policy-consistency diagnostic that doesn't already have one, derived from the
        // declaring position of each participating contract (the diagnostic's own contract and every
        // conflicting contract) in its declaring YAML list (e.g. "contracts.strict_independence[0]")
        // — so moving "target" earlier or later relative to the unrelated "other" contract changes
        // its own list index/PolicyLocation even though its conflict with "dep" never changed.
        //
        // Document.Provenance only gets populated by the real YAML loader (it defaults to
        // ArchitecturePolicyProvenanceIndex.Empty for the hand-built ArchitectureContractDocument
        // objects BaseDocument() constructs elsewhere in this fixture), so this test goes through
        // ArchitecturePolicyDocumentLoader against real YAML text to actually exercise it.
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-reorder-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            string IdentityForTarget(bool targetFirst)
            {
                string yaml = $"""
                    version: 1
                    name: ReorderTest
                    layers:
                      domain:
                        namespace: Test.Domain
                      application:
                        namespace: Test.Application
                      infrastructure:
                        namespace: Test.Infrastructure
                    analysis:
                      target_assemblies: [ArchLinterNet.Core]
                    contracts:
                      strict_independence:
                    {(targetFirst
                        ? "    - name: target\n      id: target-id\n      layers: [domain, application]\n    - name: other\n      id: other-id\n      layers: [domain, infrastructure]\n"
                        : "    - name: other\n      id: other-id\n      layers: [domain, infrastructure]\n    - name: target\n      id: target-id\n      layers: [domain, application]\n")}
                      strict_allow_only:
                        - name: dep
                          id: dep-id
                          source: domain
                          allowed: [application]
                    """;
                string path = Path.Combine(temporaryDirectory, $"policy-{targetFirst}.yml");
                File.WriteAllText(path, yaml);

                ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(path);
                var runner = new ArchitectureContractRunner(CreateContext(), document);
                PolicyConsistencyDiagnostic finding = runner.CheckPolicyConsistency()
                    .Single(f => f.CheckKind == "independence-conflict" && f.ContractId == "target-id");
                return ArchitectureFindingMapper.FromDiagnostic(finding).CanonicalIdentity;
            }

            Assert.That(IdentityForTarget(targetFirst: true), Is.EqualTo(IdentityForTarget(targetFirst: false)));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public void UnmatchedLayerExclusion_TwoTypoedEntriesOnSameLayer_GetDistinctIdentities()
    {
        // #683 PR review, P1: two unmatched-exclusion findings on the same layer share
        // ContractName ("<policy-consistency>"), ContractId (null), CheckKind, and Layers (both
        // just [layerName]) - only the exclusion's own pattern (now RepresentativeType) tells them
        // apart. Before the fix, both fell back to the bare CheckKind string and collided.
        var document = BaseDocument();
        document.Layers["core"] = new ArchitectureLayer
        {
            Namespace = "ArchLinterNet.Core.Contracts.*",
            Exclude = new List<ArchitectureLayerExclusion>
            {
                new() { Namespace = "ArchLinterNet.Core.Contracts.Familias" },
                new() { Namespace = "ArchLinterNet.Core.Contracts.Reprts" }
            }
        };
        document.Contracts.StrictLayers = new List<ArchitectureLayerContract>
        {
            new() { Name = "noop", Layers = new List<string> { "core" } }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var findings = runner.CheckPolicyConsistency()
            .Where(f => f.CheckKind == "unmatched-layer-exclusion")
            .ToList();

        Assert.That(findings, Has.Count.EqualTo(2));
        string[] identities = findings
            .Select(finding => ArchitectureFindingMapper.FromDiagnostic(finding).CanonicalIdentity)
            .ToArray();
        Assert.That(identities.Distinct().Count(), Is.EqualTo(2));
    }

    [Test]
    public void UnmatchedLayerExclusion_ReorderingExcludeEntries_DoesNotChangeEitherIdentity()
    {
        // #683 PR review, P2: identity must be stable under YAML reordering, not derived from
        // list position (exclude[0] vs exclude[1]).
        ArchitectureLayerExclusion familias = new() { Namespace = "ArchLinterNet.Core.Contracts.Familias" };
        ArchitectureLayerExclusion reprts = new() { Namespace = "ArchLinterNet.Core.Contracts.Reprts" };

        List<PolicyConsistencyDiagnostic> RunWith(List<ArchitectureLayerExclusion> exclude)
        {
            var document = BaseDocument();
            document.Layers["core"] = new ArchitectureLayer
            {
                Namespace = "ArchLinterNet.Core.Contracts.*",
                Exclude = exclude
            };
            document.Contracts.StrictLayers = new List<ArchitectureLayerContract>
            {
                new() { Name = "noop", Layers = new List<string> { "core" } }
            };
            var runner = new ArchitectureContractRunner(CreateContext(), document);
            return runner.CheckPolicyConsistency().Where(f => f.CheckKind == "unmatched-layer-exclusion").ToList();
        }

        var original = RunWith(new List<ArchitectureLayerExclusion> { familias, reprts });
        var reordered = RunWith(new List<ArchitectureLayerExclusion> { reprts, familias });

        string IdentityFor(List<PolicyConsistencyDiagnostic> findings, string namespacePattern) =>
            ArchitectureFindingMapper.FromDiagnostic(
                findings.Single(f => f.Reason.Contains(namespacePattern, StringComparison.Ordinal))).CanonicalIdentity;

        Assert.Multiple(() =>
        {
            Assert.That(
                IdentityFor(original, "Familias"),
                Is.EqualTo(IdentityFor(reordered, "Familias")));
            Assert.That(
                IdentityFor(original, "Reprts"),
                Is.EqualTo(IdentityFor(reordered, "Reprts")));
        });
    }
}
