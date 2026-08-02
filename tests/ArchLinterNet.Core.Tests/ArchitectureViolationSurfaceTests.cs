using System.Reflection;
using System.Text.Json;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// ArchitectureViolation reaches callers through ArchitectureValidationResult.Violations, so anything
// public on it is package API and shows up in a caller's own JSON projection of a raw violation.
// IdentityReferences (PR #420 review) is pipeline plumbing between identity attachment and finding
// normalization and must stay off that surface; these tests fail if it is ever widened back.
[TestFixture]
public sealed class ArchitectureViolationSurfaceTests
{
    [Test]
    public void IdentityReferences_IsNotPublicApi()
    {
        PropertyInfo? asPublic = typeof(ArchitectureViolation)
            .GetProperty("IdentityReferences", BindingFlags.Instance | BindingFlags.Public);

        Assert.That(asPublic, Is.Null,
            "IdentityReferences is pipeline state, not part of the diagnostics model callers consume");
    }

    // The whole public property set is pinned by CorePublicApiSurfaceApprovalTests, so it is not
    // repeated here — a second copy would have to be updated in step with the baseline. What stays
    // is the named guard above, which reports this specific regression far more usefully than a
    // diff against a 3 800-line surface, plus the two checks below that the approval test does not
    // cover at all.

    // A caller serializing a raw violation must see exactly what it saw before.
    [Test]
    public void SerializedViolation_DoesNotExposeThePairing()
    {
        ArchitectureViolation violation = new(
            "contract", "contract-id", "Ns.Type", "namespace", new[] { "Ns.Type.Alpha: System.String" })
        {
            IdentityReferences = new[] { "Ns.Type.Alpha: System.String" },
        };

        string json = JsonSerializer.Serialize(violation);

        Assert.That(json, Does.Not.Contain("IdentityReferences"));
        Assert.That(json, Does.Not.Contain("identityReferences"));
    }

    // Records compare every instance field, so the pairing's backing field takes part in equality.
    // For a violation a caller builds itself the field is always the Array.Empty singleton, so value
    // equality is exactly what it was before the field existed.
    [Test]
    public void ViolationsWithoutPairing_StayValueEqual()
    {
        ArchitectureViolation Build() => new(
            "contract", "contract-id", "Ns.Type", "namespace", Array.Empty<string>());

        Assert.That(Build(), Is.EqualTo(Build()));
        Assert.That(Build().GetHashCode(), Is.EqualTo(Build().GetHashCode()));
    }
}
