using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// An aggregated violation expands into one finding per identity, and each finding must carry the
// reference that identity was derived from. The method-body families format that reference from the
// identity's own (source member, target member) pair with the target member at the *end*, which the
// target-member-anchored matching alone could never attribute — so every finding fell back to the
// violation's entire reference list. That is wrong content (a finding claiming its siblings'
// references) and quadratic output: on a broad audit_external group it turned 17k references into
// 2.4M and exhausted memory before the report could be written (issue #419).
[TestFixture]
public sealed class ArchitectureFindingMapperReferenceAttributionTests
{
    [Test]
    public void FromViolations_ExternalMethodBodyReferences_AttributeOneReferencePerIdentity()
    {
        string[] references =
        {
            "Ns.Type.Alpha: System.Text.Json.JsonSerializer",
            "Ns.Type.Alpha: System.Text.Json.Nodes.JsonNode",
            "Ns.Type.Beta: System.Text.Json.Nodes.JsonNode",
        };

        ArchitectureViolation violation = ExternalViolation(references, new[]
        {
            Identity("Ns.Type.Alpha", "System.Text.Json.JsonSerializer", occurrence: 0),
            Identity("Ns.Type.Alpha", "System.Text.Json.Nodes.JsonNode", occurrence: 0),
            Identity("Ns.Type.Beta", "System.Text.Json.Nodes.JsonNode", occurrence: 0),
        });

        IReadOnlyList<ArchitectureFinding> findings = ArchitectureFindingMapper.FromViolations(new[] { violation });

        Assert.That(findings.Count, Is.EqualTo(3));
        Assert.That(findings.Select(ReferencesOf), Is.EqualTo(new[]
        {
            new[] { references[0] },
            new[] { references[1] },
            new[] { references[2] },
        }));
    }

    // A source member whose name is a prefix of another's must not claim the other's reference:
    // attribution anchors on the delimiter that closes the source member in the display.
    [Test]
    public void FromViolations_SourceMemberPrefixOfAnother_DoesNotClaimItsReference()
    {
        string[] references =
        {
            "Ns.Type.Convert: System.Text.Json.Nodes.JsonNode",
            "Ns.Type.ConvertNode: System.Text.Json.Nodes.JsonNode",
        };

        ArchitectureViolation violation = ExternalViolation(references, new[]
        {
            Identity("Ns.Type.Convert", "System.Text.Json.Nodes.JsonNode", occurrence: 0),
            Identity("Ns.Type.ConvertNode", "System.Text.Json.Nodes.JsonNode", occurrence: 0),
        });

        IReadOnlyList<ArchitectureFinding> findings = ArchitectureFindingMapper.FromViolations(new[] { violation });

        Assert.That(findings.Select(ReferencesOf), Is.EqualTo(new[]
        {
            new[] { references[0] },
            new[] { references[1] },
        }));
    }

    // A target member that only looks like a suffix of the reference is not a match — the character
    // before it has to be the separator the display puts there.
    [Test]
    public void FromViolations_TargetMemberIsOnlyADottedSuffix_IsNotAttributed()
    {
        string[] references = { "Ns.Type.Alpha: Vendor.System.String", "Ns.Type.Beta: System.Int32" };

        ArchitectureViolation violation = ExternalViolation(references, new[]
        {
            Identity("Ns.Type.Alpha", "System.String", occurrence: 0),
            Identity("Ns.Type.Beta", "System.Int32", occurrence: 0),
        });

        IReadOnlyList<ArchitectureFinding> findings = ArchitectureFindingMapper.FromViolations(new[] { violation });

        // First identity attributes nothing, so it keeps the documented whole-list fallback.
        Assert.That(ReferencesOf(findings[0]), Is.EqualTo(references));
        Assert.That(ReferencesOf(findings[1]), Is.EqualTo(new[] { references[1] }));
    }

    // Identities without a source member (metadata-level families such as package references) keep
    // the existing target-member-anchored behaviour untouched.
    [Test]
    public void FromViolations_IdentityWithoutSourceMember_KeepsTargetMemberMatching()
    {
        string[] references = { "Newtonsoft.Json@13.0.3", "Serilog@2.12.0" };

        ArchitectureViolation violation = ExternalViolation(references, new[]
        {
            Identity(sourceMember: null, "Newtonsoft.Json", occurrence: 0),
            Identity(sourceMember: null, "Serilog", occurrence: 0),
        });

        IReadOnlyList<ArchitectureFinding> findings = ArchitectureFindingMapper.FromViolations(new[] { violation });

        Assert.That(findings.Select(ReferencesOf), Is.EqualTo(new[]
        {
            new[] { references[0] },
            new[] { references[1] },
        }));
    }

    [Test]
    public void FromViolations_SingleIdentity_IsNotProjected()
    {
        string[] references = { "Ns.Type.Alpha: System.String", "Ns.Type.Beta: System.String" };

        ArchitectureViolation violation = ExternalViolation(
            references,
            new[] { Identity("Ns.Type.Alpha", "System.String", occurrence: 0) });

        IReadOnlyList<ArchitectureFinding> findings = ArchitectureFindingMapper.FromViolations(new[] { violation });

        Assert.That(findings.Count, Is.EqualTo(1));
        Assert.That(ReferencesOf(findings[0]), Is.EqualTo(references));
    }

    private static string[] ReferencesOf(ArchitectureFinding finding)
    {
        return ((ExternalDependencyDiagnostic)finding.Details).ForbiddenReferences.ToArray();
    }

    private static ArchitectureViolation ExternalViolation(
        string[] references,
        ArchitectureViolationIdentity[] identities)
    {
        return new ArchitectureViolation(
            "core-audit-system",
            "core-audit-system",
            "Ns.Type",
            "external dependency group 'system'",
            references)
        {
            Payload = new ExternalDependencyPayload("system"),
            Identity = identities[0],
            Identities = identities,
        };
    }

    private static ArchitectureViolationIdentity Identity(string? sourceMember, string targetMember, int occurrence)
    {
        return new ArchitectureViolationIdentity(
            ArchitectureViolationIdentity.CurrentVersion,
            "external",
            "reference",
            "core-audit-system",
            "TestAssembly",
            "Ns.Type",
            sourceMember,
            "TargetAssembly",
            targetMember,
            targetMember,
            occurrence);
    }
}
