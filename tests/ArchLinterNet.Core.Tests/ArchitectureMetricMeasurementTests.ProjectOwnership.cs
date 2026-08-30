using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitectureMetricMeasurementTests
{
    [Test]
    public void Measure_ExplicitTargetAssemblyWithProjectMetricUsesTheDiscoveredOutputWithoutBuildPreparation()
    {
        const string ProjectPath = "src/MyApp/MyApp.csproj";
        string projectDirectory = Path.Combine(_temporaryDirectory, "src", "MyApp");
        string outputDirectory = Path.Combine(projectDirectory, "bin", "Debug", "net10.0");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(projectDirectory, "MyApp.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>ArchLinterNet.Testing</AssemblyName>
              </PropertyGroup>
            </Project>
            """);
        File.Copy(
            Path.Combine(FindRepositoryRoot(), "src", "ArchLinterNet.Testing", "bin", "Debug", "net10.0", "ArchLinterNet.Testing.dll"),
            Path.Combine(outputDirectory, "ArchLinterNet.Testing.dll"));
        File.WriteAllText(_policyPath, """
            version: 1
            name: Metric project measurement test
            analysis:
              target_assemblies: [ArchLinterNet.Testing]
              projects: [src/MyApp/MyApp.csproj]
            topology:
              mode: partial
              subject_kind: type
              scope:
                selectors:
                  - namespace: ArchLinterNet.Testing
              nodes:
                - id: application
                  mappings:
                    - namespace: ArchLinterNet.Testing
            metrics:
              - id: application-project-footprint
                kind: component_footprint_count
                topology_node: application
                unit: project
            contracts: {}
            """);

        using ArchitectureEngine engine = new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();
        ArchitectureMetricMeasurement measurement = engine.Measure(new ArchitectureMetricMeasurementRequest
        {
            PolicyPath = _policyPath,
        }).Measurements.Single();

        Assert.Multiple(() =>
        {
            Assert.That(measurement.IsEvaluable, Is.True);
            Assert.That(measurement.Value, Is.EqualTo(1));
            Assert.That(measurement.Contributors, Is.EqualTo(new[] { ProjectPath }));
        });
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ArchLinterNet.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
