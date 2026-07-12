using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ProjectMetadataConfigurationTests
{
    private static ArchitectureAnalysisContext CreateContext(ProjectDiscoveryResult? projectDiscovery = null)
    {
        return new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(ProjectMetadataConfigurationTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>(),
            projectDiscovery: projectDiscovery);
    }

    [Test]
    public void CheckConfiguration_MissingDiscoveredProject_ReturnsViolation()
    {
        ArchitectureContractDocument document = new()
        {
            Version = 1,
            Name = "Test",
            Contracts = new ArchitectureContractGroups
            {
                StrictProjectMetadata = new List<ArchitectureProjectMetadataContract>
                {
                    new()
                    {
                        Id = "project-metadata",
                        Name = "project-metadata",
                        Projects = new List<string> { "src/MyApp/MyApp.csproj" },
                        RequiredProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Nullable"] = "enable"
                        }
                    }
                }
            }
        };

        ArchitectureContractRunner runner = new(CreateContext(projectDiscovery: null), document);
        List<ArchitectureViolation> violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v =>
            v.ContractId == "project-metadata" && v.ForbiddenNamespace == "no project metadata discovered"), Is.True);
    }

    [Test]
    public void CheckConfiguration_WithContractSelection_DoesNotFlagUnselectedContractForMissingProject()
    {
        ArchitectureContractDocument document = new()
        {
            Version = 1,
            Name = "Test",
            Contracts = new ArchitectureContractGroups
            {
                StrictProjectMetadata = new List<ArchitectureProjectMetadataContract>
                {
                    new()
                    {
                        Id = "project-metadata",
                        Name = "project-metadata",
                        Projects = new List<string> { "src/MyApp/MyApp.csproj" },
                        RequiredProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Nullable"] = "enable"
                        }
                    }
                }
            }
        };

        ArchitectureContractRunner runner = new(
            CreateContext(projectDiscovery: null),
            document,
            selectedContractIds: new HashSet<string> { "some-other-contract" });

        List<ArchitectureViolation> violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v =>
            v.ContractId == "project-metadata" && v.ForbiddenNamespace == "no project metadata discovered"), Is.False,
            "CheckConfiguration must not report missing-project diagnostics for contracts that are not selected.");
    }

    [Test]
    public void PolicyLoader_ProjectMetadataContractWithoutProjects_Throws()
    {
        string policyPath = Path.Combine(Path.GetTempPath(), $"arch-linter-project-metadata-policy-{Guid.NewGuid():N}.yml");
        File.WriteAllText(policyPath, """
            version: 1
            name: Test
            layers: {}
            analysis:
              target_assemblies: []
            contracts:
              strict_project_metadata:
                - name: project-metadata
                  required_properties:
                    Nullable: enable
            """);

        try
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

            Assert.That(ex.Message, Does.Contain("declares no usable 'projects'"));
        }
        finally
        {
            File.Delete(policyPath);
        }
    }

    [Test]
    public void PolicyLoader_ProjectMetadataContractWithoutExpectations_Throws()
    {
        string policyPath = Path.Combine(Path.GetTempPath(), $"arch-linter-project-metadata-policy-{Guid.NewGuid():N}.yml");
        File.WriteAllText(policyPath, """
            version: 1
            name: Test
            layers: {}
            analysis:
              target_assemblies: []
            contracts:
              strict_project_metadata:
                - name: project-metadata
                  projects:
                    - src/MyApp/MyApp.csproj
            """);

        try
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

            Assert.That(ex.Message, Does.Contain("declares no metadata expectation"));
        }
        finally
        {
            File.Delete(policyPath);
        }
    }
}
