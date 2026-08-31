using System.Globalization;
using System.Xml.Linq;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class CoreTestSelectionRunSettingsTests
{
    [Test]
    public void CoreProject_DisablesNUnitCountBasedSelectionFallback_AndMainKeepsHangDetection()
    {
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        string projectDirectory = Path.Combine(repositoryRoot, "tests", "ArchLinterNet.Core.Tests");
        string projectPath = Path.Combine(projectDirectory, "ArchLinterNet.Core.Tests.csproj");
        string runSettingsPath = Path.Combine(projectDirectory, "core-test-selection.runsettings");
        string workflowPath = Path.Combine(repositoryRoot, ".github", "workflows", "main-quality.yml");

        string project = File.ReadAllText(projectPath);
        XDocument runSettings = XDocument.Load(runSettingsPath);
        string? assemblySelectLimit = runSettings.Root?
            .Element("NUnit")?
            .Element("AssemblySelectLimit")?
            .Value;
        string workflow = File.ReadAllText(workflowPath);

        Assert.Multiple(() =>
        {
            Assert.That(project, Does.Contain(
                "<RunSettingsFilePath>$(MSBuildThisFileDirectory)core-test-selection.runsettings</RunSettingsFilePath>"));
            Assert.That(assemblySelectLimit,
                Is.EqualTo(int.MaxValue.ToString(CultureInfo.InvariantCulture)));
            Assert.That(workflow, Does.Contain(
                "TEST_COVERAGE_DIAGNOSTICS: --blame-hang --blame-hang-timeout 5m"));
        });
    }
}
