using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.PolicyImports;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitecturePolicySourceParserTests
{
    [Test]
    public void Parse_FragmentWithNestedImport_PreservesDescriptorAndImports()
    {
        var parser = new ArchitecturePolicySourceParser();
        ArchitecturePolicySource source = parser.Parse(
            Descriptor(ArchitecturePolicyDocumentRole.Fragment), "full", "physical", "identity", "imports: [nested.yml]\n");

        Assert.Multiple(() =>
        {
            Assert.That(source.Role, Is.EqualTo(ArchitecturePolicyDocumentRole.Fragment));
            Assert.That(source.PortableIdentity, Is.EqualTo("architecture/fragments/policy.yml"));
            Assert.That(source.Imports, Is.EqualTo(new[] { "nested.yml" }));
        });
    }

    [TestCase(ArchitecturePolicyDocumentRole.Root, "version: 1\nname: Example\nunexpected: true\n", "unexpected")]
    [TestCase(ArchitecturePolicyDocumentRole.Root, "version: 1\n", "$")]
    [TestCase(ArchitecturePolicyDocumentRole.Fragment, "name: Fragment\nlayers:\n  core:\n    namespace: App.Core\n", "name")]
    [TestCase(ArchitecturePolicyDocumentRole.Fragment, "{}\n", "$")]
    public void Parse_SourceShapeErrors_ExposeTypedLocation(
        ArchitecturePolicyDocumentRole role,
        string yaml,
        string expectedYamlPath)
    {
        var parser = new ArchitecturePolicySourceParser();
        ArchitecturePolicySourceDescriptor descriptor = Descriptor(role);

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => parser.Parse(descriptor, "full", "physical", "identity", yaml))!;
        ArchitecturePolicySourceLocation location = exception.Diagnostic!.Location!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Category, Is.EqualTo(ArchitecturePolicyImportErrorCategory.SourceShape));
            Assert.That(location.SourcePath, Is.EqualTo(descriptor.SourcePath));
            Assert.That(location.YamlPath, Is.EqualTo(expectedYamlPath));
        });
    }

    [Test]
    public void Parse_MalformedFragment_EnrichesParserExceptionWithSourceLocation()
    {
        var parser = new ArchitecturePolicySourceParser();
        ArchitecturePolicySourceDescriptor descriptor = Descriptor(ArchitecturePolicyDocumentRole.Fragment);

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => parser.Parse(descriptor, "full", "physical", "identity", "layers: [unterminated"))!;
        ArchitecturePolicySourceLocation location = exception.Diagnostic!.Location!;

        Assert.Multiple(() =>
        {
            Assert.That(location.SourcePath, Is.EqualTo("architecture/fragments/policy.yml"));
            Assert.That(location.YamlPath, Is.EqualTo("$"));
            Assert.That(location.Role, Is.EqualTo(ArchitecturePolicyDocumentRole.Fragment));
        });
    }

    [Test]
    public void Parse_NonScalarImportEntry_UsesIndexedLocation()
    {
        var parser = new ArchitecturePolicySourceParser();
        ArchitecturePolicySourceDescriptor descriptor = Descriptor(ArchitecturePolicyDocumentRole.Fragment);

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => parser.Parse(
                descriptor, "full", "physical", "identity", "imports: [nested.yml, { path: nested.yml }]\n"))!;

        Assert.That(exception.Diagnostic!.Location!.YamlPath, Is.EqualTo("imports[1]"));
    }

    [Test]
    public void ValidatePortableImport_UsesIndexedImportLocationAndChain()
    {
        var parser = new ArchitecturePolicySourceParser();
        ArchitecturePolicySource source = parser.Parse(
            Descriptor(ArchitecturePolicyDocumentRole.Fragment), "full", "physical", "identity",
            "imports: [one.yml, two.yml, nested\\policy.yml]\n");

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => ArchitecturePolicySourceParser.ValidatePortableImport("nested\\policy.yml", source, 2))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Category, Is.EqualTo(ArchitecturePolicyImportErrorCategory.PortablePath));
            Assert.That(exception.Diagnostic!.Location!.YamlPath, Is.EqualTo("imports[2]"));
            Assert.That(exception.Diagnostic.ImportChain, Is.EqualTo(new[]
            {
                "architecture/root.yml", "architecture/fragments/policy.yml", "nested\\policy.yml"
            }));
        });
    }

    [Test]
    public void ContainsImports_DistinguishesEmptyAndImportedDocuments()
    {
        var parser = new ArchitecturePolicySourceParser();

        Assert.Multiple(() =>
        {
            Assert.That(parser.ContainsImports(string.Empty), Is.False);
            Assert.That(parser.ContainsImports("imports: []\n"), Is.True);
        });
    }

    private static ArchitecturePolicySourceDescriptor Descriptor(ArchitecturePolicyDocumentRole role)
    {
        return role == ArchitecturePolicyDocumentRole.Root
            ? new ArchitecturePolicySourceDescriptor(
                "architecture/root.yml", "architecture/root.yml", role, 0, null, null,
                ["architecture/root.yml"])
            : new ArchitecturePolicySourceDescriptor(
                "architecture/root.yml", "architecture/fragments/policy.yml", role, 1,
                "architecture/root.yml", "fragments/policy.yml",
                ["architecture/root.yml", "architecture/fragments/policy.yml"]);
    }
}
