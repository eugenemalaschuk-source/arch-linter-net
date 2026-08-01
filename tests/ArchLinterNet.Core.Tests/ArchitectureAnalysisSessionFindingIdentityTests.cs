using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// AttachFindingIdentities matches the candidates recorded while contracts ran against the
// violations those contracts reported. It buckets candidates by (contract id, source type) instead
// of scanning one flat list (issue #419); these tests pin the matching contract that optimisation
// must preserve — one identity per reported reference, in reported-reference order, with each
// candidate consumed at most once across the violations of a single contract.
[TestFixture]
public sealed class ArchitectureAnalysisSessionFindingIdentityTests
{
    [Test]
    public void AttachFindingIdentities_ExternalContractViolations_AttachOneIdentityPerReportedReference()
    {
        ArchitectureAnalysisSession session = CreateSession(out ArchitectureExternalDependencyContract contract);

        int cursor = session.FindingIdentityCursor;
        List<ArchitectureViolation> violations = session.CheckExternalContract(contract);
        IReadOnlyList<ArchitectureViolation> attached = session.AttachFindingIdentities(violations, cursor);

        Assert.That(attached, Is.Not.Empty);
        Assert.That(attached.Select(v => v.SourceType), Is.EqualTo(violations.Select(v => v.SourceType)));

        foreach (ArchitectureViolation violation in attached)
        {
            Assert.That(violation.Identities, Is.Not.Empty, $"no identities attached for {violation.SourceType}");
            Assert.That(violation.Identities.Count, Is.EqualTo(violation.ForbiddenReferences.Count),
                $"identity count mismatch for {violation.SourceType}");
            Assert.That(violation.Identity, Is.EqualTo(violation.Identities.First()));
            Assert.That(violation.Identities.All(identity => identity.SourceType == violation.SourceType), Is.True);
        }
    }

    // A candidate consumed by one violation must not be reused by another, so identities stay
    // unique across the whole contract even when several source types report the same reference.
    [Test]
    public void AttachFindingIdentities_ExternalContractViolations_ConsumeEachCandidateOnce()
    {
        ArchitectureAnalysisSession session = CreateSession(out ArchitectureExternalDependencyContract contract);

        int cursor = session.FindingIdentityCursor;
        IReadOnlyList<ArchitectureViolation> attached =
            session.AttachFindingIdentities(session.CheckExternalContract(contract), cursor);

        ArchitectureViolationIdentity[] allIdentities = attached
            .SelectMany(violation => violation.Identities)
            .ToArray();

        Assert.That(allIdentities, Is.Not.Empty);
        Assert.That(allIdentities.Distinct().Count(), Is.EqualTo(allIdentities.Length));
    }

    [Test]
    public void AttachFindingIdentities_NoCandidatesAfterCursor_LeavesViolationsUnchanged()
    {
        ArchitectureAnalysisSession session = CreateSession(out ArchitectureExternalDependencyContract contract);

        List<ArchitectureViolation> violations = session.CheckExternalContract(contract);
        IReadOnlyList<ArchitectureViolation> attached =
            session.AttachFindingIdentities(violations, session.FindingIdentityCursor);

        Assert.That(attached.All(violation => violation.Identity == null), Is.True);
        Assert.That(attached.All(violation => violation.Identities.Count == 0), Is.True);
    }

    private static ArchitectureAnalysisSession CreateSession(out ArchitectureExternalDependencyContract contract)
    {
        contract = new ArchitectureExternalDependencyContract
        {
            Id = "core-no-vendor-sdk",
            Name = "core-no-vendor-sdk",
            Source = "core",
            Forbidden = new List<string> { "vendor_sdk" }
        };

        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "ExternalDependencyContractTestsFixtures.Core" }
            },
            ExternalDependencies = new Dictionary<string, ArchitectureExternalDependencyGroup>
            {
                ["vendor_sdk"] = new()
                {
                    NamespacePrefixes = new List<string> { "ExternalDependencyContractTestsFixtures.VendorSdk" }
                }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string>
                {
                    typeof(ArchitectureAnalysisSessionFindingIdentityTests).Assembly.GetName().Name!
                }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictExternal = new List<ArchitectureExternalDependencyContract> { contract }
            }
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(ArchitectureAnalysisSessionFindingIdentityTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        return new ArchitectureAnalysisSession(context, document, null, false, null);
    }
}
