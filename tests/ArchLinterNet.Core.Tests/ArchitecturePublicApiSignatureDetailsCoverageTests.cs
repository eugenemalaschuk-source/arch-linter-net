using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Direct coverage of ArchitecturePublicApiSignatureDetails's branches through the real production
// entry point (ArchitecturePublicApiSurfaceScanner.GetExportedSurface), rather than calling the
// internal detail-builder methods directly: this exercises the same integration path capture/diff
// actually run, and the fixtures double as living examples of what each detail token looks like.
[TestFixture]
public sealed class ArchitecturePublicApiSignatureDetailsCoverageTests
{
    private static IReadOnlyList<ArchitectureExportedApiEntry> _entries = null!;

    [OneTimeSetUp]
    public void ScanFixtureAssembly()
    {
        _entries = ArchitecturePublicApiSurfaceScanner
            .GetExportedSurface(typeof(ArchitecturePublicApiSignatureDetailsCoverageTests).Assembly)
            .ToList();
    }

    private static string ExactSignatureFor(string signatureSubstring)
    {
        return _entries.Single(entry => entry.Signature.Contains(signatureSubstring, StringComparison.Ordinal))
            .ExactSignature;
    }

    [Test]
    public void OpenAbstractType_IsReportedAsAbstract()
    {
        Assert.That(ExactSignatureFor("class PublicApiSurfaceContractTestFixtures.OpenAbstractType"), Does.Contain("[abstract]"));
    }

    [Test]
    public void StaticUtilityType_IsReportedAsStatic()
    {
        Assert.That(ExactSignatureFor("class PublicApiSurfaceContractTestFixtures.StaticUtilityType"), Does.Contain("[static]"));
    }

    [Test]
    public void ReadOnlyStructType_IsReportedAsReadOnly()
    {
        Assert.That(ExactSignatureFor("struct PublicApiSurfaceContractTestFixtures.ReadOnlyStructType"), Does.Contain("[readonly]"));
    }

    [Test]
    public void ConstrainedInterface_ReportsClassAndNewConstraint()
    {
        Assert.That(
            ExactSignatureFor("interface PublicApiSurfaceContractTestFixtures.IConstrainedInterface"),
            Does.Contain("where0:class new()"));
    }

    [Test]
    public void ConstrainedGenericType_ReportsStructConstraintWithoutRedundantNew()
    {
        string exact = ExactSignatureFor("class PublicApiSurfaceContractTestFixtures.ConstrainedGenericType");

        Assert.Multiple(() =>
        {
            Assert.That(exact, Does.Contain("where0:struct"));
            Assert.That(exact, Does.Not.Contain("new()"));
        });
    }

    [Test]
    public void VirtualMethod_NotOverride_IsReportedAsVirtual()
    {
        Assert.That(
            ExactSignatureFor("method PublicApiSurfaceContractTestFixtures.VirtualMethodHolder.DoWork"),
            Does.Contain("[virtual]"));
    }

    [Test]
    public void AbstractMethod_IsReportedAsAbstract()
    {
        Assert.That(
            ExactSignatureFor("method PublicApiSurfaceContractTestFixtures.AbstractMethodHolder.DoOtherWork"),
            Does.Contain("[abstract]"));
    }

    [Test]
    public void OverrideMethod_IsReportedAsOverride()
    {
        Assert.That(
            ExactSignatureFor("method PublicApiSurfaceContractTestFixtures.OverrideMethodHolder.DoWork"),
            Does.Contain("[override]"));
    }

    [Test]
    public void SealedOverrideMethod_IsReportedAsSealedOverride()
    {
        Assert.That(
            ExactSignatureFor("method PublicApiSurfaceContractTestFixtures.SealedOverrideMethodHolder.DoWork"),
            Does.Contain("[sealed override]"));
    }

    [Test]
    public void RefOutInParameters_AreDistinguishedByDirection()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                ExactSignatureFor("method PublicApiSurfaceContractTestFixtures.ParameterModifierHolder.TakeRef"),
                Does.Contain("param0:ref"));
            Assert.That(
                ExactSignatureFor("method PublicApiSurfaceContractTestFixtures.ParameterModifierHolder.TakeOut"),
                Does.Contain("param0:out"));
            Assert.That(
                ExactSignatureFor("method PublicApiSurfaceContractTestFixtures.ParameterModifierHolder.TakeIn"),
                Does.Contain("param0:in"));
        });
    }

    [Test]
    public void ParamsParameter_IsReportedAsParams()
    {
        Assert.That(
            ExactSignatureFor("method PublicApiSurfaceContractTestFixtures.ParameterModifierHolder.TakeParams"),
            Does.Contain("param0:params"));
    }

    [Test]
    public void GenericMethodConstraint_ReportsClassAndNewConstraint()
    {
        Assert.That(
            ExactSignatureFor("method PublicApiSurfaceContractTestFixtures.GenericMethodConstraintHolder.Do"),
            Does.Contain("where0:class new()"));
    }

    [Test]
    public void StaticProperty_IsReportedAsStatic()
    {
        Assert.That(
            ExactSignatureFor("property PublicApiSurfaceContractTestFixtures.PropertyVariantHolder.StaticProperty"),
            Does.Contain("[static, get, set]"));
    }

    [Test]
    public void InitOnlyProperty_IsReportedAsInit()
    {
        Assert.That(
            ExactSignatureFor("property PublicApiSurfaceContractTestFixtures.PropertyVariantHolder.InitOnlyProperty"),
            Does.Contain("[get, init]"));
    }

    [Test]
    public void ProtectedSetProperty_IsDistinguishedFromPublicSet()
    {
        Assert.That(
            ExactSignatureFor("PropertyVariantHolder.PublicGetProtectedSetProperty"),
            Does.Contain("[get, set:protected]"));
    }

    [Test]
    public void ProtectedInternalSetProperty_IsDistinguishedFromPublicSet()
    {
        Assert.That(
            ExactSignatureFor("PropertyVariantHolder.PublicGetProtectedInternalSetProperty"),
            Does.Contain("[get, set:protected internal]"));
    }

    [Test]
    public void StaticField_IsReportedAsStatic()
    {
        Assert.That(
            ExactSignatureFor("field PublicApiSurfaceContractTestFixtures.FieldVariantHolder.StaticField"),
            Does.Contain("[static]"));
    }

    [Test]
    public void ReadOnlyInstanceField_IsReportedAsReadOnly()
    {
        Assert.That(
            ExactSignatureFor("field PublicApiSurfaceContractTestFixtures.FieldVariantHolder.ReadOnlyField"),
            Does.Contain("[readonly]"));
    }

    [Test]
    public void StaticEvent_IsReportedAsStatic()
    {
        Assert.That(
            ExactSignatureFor("event PublicApiSurfaceContractTestFixtures.EventVariantHolder.StaticEvent"),
            Does.Contain("[static]"));
    }

    // Dispatch shape (virtual/override) is dropped by the legacy identity signature just like
    // visibility is, for properties and events exactly as for ordinary methods: an override that
    // silently becomes sealed, or a virtual property that stops being overridable, would otherwise
    // be an invisible, byte-identical snapshot.
    [Test]
    public void VirtualProperty_NotOverride_IsReportedAsVirtual()
    {
        Assert.That(
            ExactSignatureFor("property PublicApiSurfaceContractTestFixtures.VirtualPropertyHolder.Value"),
            Does.Contain("virtual"));
    }

    [Test]
    public void OverrideProperty_IsReportedAsOverride()
    {
        Assert.That(
            ExactSignatureFor("property PublicApiSurfaceContractTestFixtures.OverridePropertyHolder.Value"),
            Does.Contain("override"));
    }

    [Test]
    public void VirtualEvent_NotOverride_IsReportedAsVirtual()
    {
        Assert.That(
            ExactSignatureFor("event PublicApiSurfaceContractTestFixtures.VirtualEventHolder.Changed"),
            Does.Contain("virtual"));
    }

    [Test]
    public void OverrideEvent_IsReportedAsOverride()
    {
        Assert.That(
            ExactSignatureFor("event PublicApiSurfaceContractTestFixtures.OverrideEventHolder.Changed"),
            Does.Contain("override"));
    }

    [Test]
    public void BoolConstant_IsFormattedAsLowercaseLiteral()
    {
        Assert.That(ExactSignatureFor("ConstantVariantHolder.BoolConst"), Does.Contain("[value:true]"));
    }

    [Test]
    public void CharConstant_IsQuoted()
    {
        Assert.That(ExactSignatureFor("ConstantVariantHolder.CharConst"), Does.Contain("[value:\"x\"]"));
    }

    [Test]
    public void FloatConstant_IsFormattedInvariantly()
    {
        Assert.That(ExactSignatureFor("ConstantVariantHolder.FloatConst"), Does.Contain("[value:1.5]"));
    }

    [Test]
    public void DoubleConstant_IsFormattedInvariantly()
    {
        Assert.That(ExactSignatureFor("ConstantVariantHolder.DoubleConst"), Does.Contain("[value:2.5]"));
    }

    [Test]
    public void IntConstant_IsFormattedThroughTheIFormattableFallback()
    {
        Assert.That(ExactSignatureFor("ConstantVariantHolder.IntConst"), Does.Contain("[value:42]"));
    }

    [Test]
    public void StringConstant_EscapesQuotesBackslashesAndControlCharacters()
    {
        string exact = ExactSignatureFor("ConstantVariantHolder.EscapedStringConst");

        Assert.That(
            exact,
            Does.Contain("[value:\"line1\\nline2\\t\\\"quoted\\\"\\\\backslash\"]"));
    }

    // The detail suffix boundary is located by searching for the last " [" / trailing "]" in the
    // whole exact signature. Without escaping, a string constant whose value itself contains " ["
    // would be indistinguishable from the real outer delimiter and StripDetails would truncate the
    // signature in the middle of the constant's value instead of at the true boundary.
    [Test]
    public void StringConstantContainingBrackets_EscapesThemSoTheDetailBoundaryStaysUnambiguous()
    {
        string exact = ExactSignatureFor("ConstantVariantHolder.BracketConst");

        Assert.Multiple(() =>
        {
            Assert.That(exact, Does.Contain("[value:\"foo \\[bar\\]\"]"));
            Assert.That(
                ArchitecturePublicApiSignatureDetails.StripDetails(exact),
                Is.EqualTo(
                    "const PublicApiSurfaceContractTestFixtures.ConstantVariantHolder.BracketConst: System.String"));
        });
    }
}
