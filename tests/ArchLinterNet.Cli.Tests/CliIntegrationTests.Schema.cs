using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public partial class CliIntegrationTests
{
    [Test]
    public void SchemaList_UsesThePackagedReleaseRegistry()
    {
        var (exitCode, stdout, stderr) = RunCli("schema", "list");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stderr, Is.Empty);
            Assert.That(stdout, Does.Contain("policy-root\tv1\thttps://archlinternet.dev/schema/0.5.1/"));
            Assert.That(stdout, Does.Contain("baseline\tv2\t"));
            Assert.That(stdout, Does.Contain("analysis-build-state\tv1\t"));
        });
    }

    [Test]
    public void SchemaPrint_WritesTheExactNamedSchema()
    {
        var (exitCode, stdout, stderr) = RunCli("schema", "print", "api-snapshot");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stderr, Is.Empty);
            Assert.That(stdout, Does.Contain("https://archlinternet.dev/schema/0.5.1/api-snapshot.schema.json"));
        });
    }

    [Test]
    public void SchemaPrint_UnknownLogicalId_ReturnsUsageError()
    {
        var (exitCode, stdout, stderr) = RunCli("schema", "print", "missing");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stdout, Is.Empty);
            Assert.That(stderr, Does.Contain("Unknown packaged schema 'missing'"));
        });
    }
}
