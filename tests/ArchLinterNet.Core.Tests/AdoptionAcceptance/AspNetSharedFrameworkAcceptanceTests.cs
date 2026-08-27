using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using ArchLinterNet.Core.Change;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Discovery.Abstractions;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Reproduces issue #441: reflecting over a consumer assembly that references the ASP.NET Core
// shared framework fails through the documented --ensure-built entrypoint, because the isolated
// post-build load scope has no probing path for Microsoft.AspNetCore.App. These tests exercise the
// real post-build resolution pipeline (ArchitectureRunnerSetupService.PrepareRunner +
// MaterializePreparedRunner) against a real, separately compiled ASP.NET Core fixture assembly.
[TestFixture]
public sealed class AspNetSharedFrameworkAcceptanceTests
{
    private sealed class FixedDiscoveryService : IArchitectureProjectDiscoveryService
    {
        public ProjectDiscoveryResult Result { get; set; } = ProjectDiscoveryResult.Empty;

        public ProjectDiscoveryResult ResolveAndApply(
            ArchitectureContractDocument document, string repositoryRoot, bool resolveAssemblyOutputs,
            CancellationToken cancellationToken = default) => Result;
    }

    private AdoptionAcceptanceFixture _fixture = null!;
    private string _assemblyPath = null!;

    [OneTimeSetUp]
    public void BuildFixture()
    {
        _fixture = AdoptionAcceptanceFixture.Create("aspnet-host");
        _fixture.Build();
        _assemblyPath = Path.Combine(_fixture.Root, "bin", "Debug", "net10.0", "Synthetic.AspNetHost.dll");
        Assert.That(File.Exists(_assemblyPath), Is.True, _assemblyPath);
    }

    [OneTimeTearDown]
    public void DisposeFixture()
    {
        _fixture.Dispose();
    }

    [Test]
    public void SharedFrameworkConfigured_BaseTypeFromAspNetCoreResolvesThroughIsolatedLoadScope()
    {
        ArchitectureRunnerSetup setup = MaterializeFixture(sharedFrameworks: new List<string> { "Microsoft.AspNetCore.App" });
        try
        {
            Type controllerType = ResolveControllerType(setup);

            Assert.That(() => controllerType.BaseType, Throws.Nothing);
            Assert.That(controllerType.BaseType, Is.Not.Null);
        }
        finally
        {
            setup.Runner.Session.Context.Dispose();
        }
    }

    [Test]
    public void SharedFrameworkNotConfigured_BaseTypeFromAspNetCoreFailsToResolve()
    {
        ArchitectureRunnerSetup setup = MaterializeFixture(sharedFrameworks: new List<string>());
        try
        {
            // Resolving the type itself already needs the ASP.NET Core assembly that defines its
            // base type (Microsoft.AspNetCore.Mvc.ControllerBase); it never gets far enough for a
            // separate BaseType access to be the observable failure point.
            Assert.That(() => ResolveControllerType(setup), Throws.InstanceOf<Exception>());
        }
        finally
        {
            setup.Runner.Session.Context.Dispose();
        }
    }

    [Test]
    public void UnresolvableSharedFrameworkName_ThrowsActionableDiagnosticBeforeLoading()
    {
        var discovery = new FixedDiscoveryService
        {
            Result = ProjectDiscoveryResult.Empty with
            {
                ResolvedAssemblyPaths = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Synthetic.AspNetHost"] = _assemblyPath,
                },
            },
        };
        var service = CreateService(discovery);
        ArchitectureContractDocument document = CreateDocument(new List<string> { "Definitely.Not.An.Installed.Framework" });

        ArchitectureRunnerPreparation preparation = service.PrepareRunner(document, _fixture.PolicyPath);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => service.MaterializePreparedRunner(document, preparation))!;
        Assert.That(exception.Message, Does.Contain("Definitely.Not.An.Installed.Framework"));
    }

    [Test]
    public void EnsureBuiltPackagedEntrypoint_AnalyzesAspNetHostFixtureWithoutARuntimeConfigWrapper()
    {
        string cliDllPath = Path.Combine(
            new ArchitectureRepositoryRootResolver().Resolve(),
            "src", "ArchLinterNet.Cli", "bin", "Debug", "net10.0", "ArchLinterNet.Cli.dll");
        Assert.That(File.Exists(cliDllPath), Is.True, cliDllPath);

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = _fixture.Root,
        };
        startInfo.Environment["DOTNET_CLI_DISABLE_COLOR"] = "1";
        startInfo.ArgumentList.Add(cliDllPath);
        startInfo.ArgumentList.Add("--policy");
        startInfo.ArgumentList.Add(_fixture.PolicyPath);
        startInfo.ArgumentList.Add("--strict");
        startInfo.ArgumentList.Add("--format");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add("--ensure-built");

        using var process = Process.Start(startInfo)!;
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.That(process.ExitCode, Is.EqualTo(0), () => $"stdout: {stdout}{Environment.NewLine}stderr: {stderr}");
        using JsonDocument result = JsonDocument.Parse(stdout);
        Assert.That(result.RootElement.GetProperty("passed").GetBoolean(), Is.True, stdout);
    }

    [Test]
    public void BaselineVerifyEnsureBuiltPackagedEntrypoint_AnalyzesAspNetHostFixture()
    {
        string cliDllPath = Path.Combine(
            new ArchitectureRepositoryRootResolver().Resolve(),
            "src", "ArchLinterNet.Cli", "bin", "Debug", "net10.0", "ArchLinterNet.Cli.dll");
        Assert.That(File.Exists(cliDllPath), Is.True, cliDllPath);

        string baselinePath = Path.Combine(_fixture.Root, "baseline.arch.yml");
        File.WriteAllText(baselinePath, "version: 2\nbaseline: {}\n");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = _fixture.Root,
        };
        startInfo.Environment["DOTNET_CLI_DISABLE_COLOR"] = "1";
        startInfo.ArgumentList.Add(cliDllPath);
        startInfo.ArgumentList.Add("baseline");
        startInfo.ArgumentList.Add("verify");
        startInfo.ArgumentList.Add("--policy");
        startInfo.ArgumentList.Add(_fixture.PolicyPath);
        startInfo.ArgumentList.Add("--baseline");
        startInfo.ArgumentList.Add(baselinePath);
        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add("strict");
        startInfo.ArgumentList.Add("--format");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add("--ensure-built");
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add("Debug");
        startInfo.ArgumentList.Add("--framework");
        startInfo.ArgumentList.Add("net10.0");

        using var process = Process.Start(startInfo)!;
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.That(process.ExitCode, Is.EqualTo(0), () => $"stdout: {stdout}{Environment.NewLine}stderr: {stderr}");
        using JsonDocument result = JsonDocument.Parse(stdout);
        Assert.That(result.RootElement.GetProperty("inSync").GetBoolean(), Is.True, stdout);
    }

    [Test]
    public void ChangeSnapshotEnsureBuiltPackagedEntrypoint_AnalyzesAspNetHostFixture()
    {
        string cliDllPath = Path.Combine(
            new ArchitectureRepositoryRootResolver().Resolve(),
            "src", "ArchLinterNet.Cli", "bin", "Debug", "net10.0", "ArchLinterNet.Cli.dll");
        Assert.That(File.Exists(cliDllPath), Is.True, cliDllPath);

        string snapshotPath = Path.Combine(_fixture.Root, "snapshot.json");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = _fixture.Root,
        };
        startInfo.Environment["DOTNET_CLI_DISABLE_COLOR"] = "1";
        startInfo.ArgumentList.Add(cliDllPath);
        startInfo.ArgumentList.Add("change");
        startInfo.ArgumentList.Add("snapshot");
        startInfo.ArgumentList.Add("--policy");
        startInfo.ArgumentList.Add(_fixture.PolicyPath);
        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add("strict");
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(snapshotPath);
        startInfo.ArgumentList.Add("--ensure-built");
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add("Debug");
        startInfo.ArgumentList.Add("--framework");
        startInfo.ArgumentList.Add("net10.0");

        using var process = Process.Start(startInfo)!;
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.That(process.ExitCode, Is.EqualTo(0), () => $"stdout: {stdout}{Environment.NewLine}stderr: {stderr}");
        Assert.That(File.Exists(snapshotPath), Is.True, snapshotPath);
        ArchitectureChangeSnapshot snapshot = ArchitectureChangeReports.DeserializeSnapshot(File.ReadAllText(snapshotPath));
        Assert.That(snapshot.Mode, Is.EqualTo("strict"));
    }

    [Test]
    public void ChangeSnapshotEnsureBuiltPackagedEntrypoint_PreservesReleaseAndRuntimeOutputContext()
    {
        string cliDllPath = Path.Combine(
            new ArchitectureRepositoryRootResolver().Resolve(),
            "src", "ArchLinterNet.Cli", "bin", "Debug", "net10.0", "ArchLinterNet.Cli.dll");
        Assert.That(File.Exists(cliDllPath), Is.True, cliDllPath);

        string baselinePath = Path.Combine(_fixture.Root, "release-runtime.baseline.arch.yml");
        string snapshotPath = Path.Combine(_fixture.Root, "release-runtime-snapshot.json");
        File.WriteAllText(baselinePath, "version: 2\nbaseline: {}\n");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = _fixture.Root,
        };
        startInfo.Environment["DOTNET_CLI_DISABLE_COLOR"] = "1";
        startInfo.ArgumentList.Add(cliDllPath);
        startInfo.ArgumentList.Add("change");
        startInfo.ArgumentList.Add("snapshot");
        startInfo.ArgumentList.Add("--policy");
        startInfo.ArgumentList.Add(_fixture.PolicyPath);
        startInfo.ArgumentList.Add("--baseline");
        startInfo.ArgumentList.Add(baselinePath);
        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add("strict");
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(snapshotPath);
        startInfo.ArgumentList.Add("--ensure-built");
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("--runtime");
        startInfo.ArgumentList.Add(CurrentRuntimeIdentifier());

        using var process = Process.Start(startInfo)!;
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.That(process.ExitCode, Is.EqualTo(0), () => $"stdout: {stdout}{Environment.NewLine}stderr: {stderr}");
        Assert.That(File.Exists(snapshotPath), Is.True, snapshotPath);
        ArchitectureChangeSnapshot snapshot = ArchitectureChangeReports.DeserializeSnapshot(File.ReadAllText(snapshotPath));
        Assert.That(snapshot.Mode, Is.EqualTo("strict"));
    }

    private ArchitectureRunnerSetup MaterializeFixture(List<string> sharedFrameworks)
    {
        var discovery = new FixedDiscoveryService
        {
            Result = ProjectDiscoveryResult.Empty with
            {
                ResolvedAssemblyPaths = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Synthetic.AspNetHost"] = _assemblyPath,
                },
            },
        };
        var service = CreateService(discovery);
        ArchitectureContractDocument document = CreateDocument(sharedFrameworks);

        ArchitectureRunnerPreparation preparation = service.PrepareRunner(document, _fixture.PolicyPath);
        return service.MaterializePreparedRunner(document, preparation);
    }

    private static Type ResolveControllerType(ArchitectureRunnerSetup setup)
    {
        Assembly assembly = setup.Runner.Session.Context.TargetAssemblies
            .Single(a => a.GetName().Name == "Synthetic.AspNetHost");
        return assembly.GetType("Synthetic.AspNetHost.GreetingsController", throwOnError: true)!;
    }

    private static string CurrentRuntimeIdentifier()
    {
        string operatingSystem = OperatingSystem.IsWindows()
            ? "win"
            : OperatingSystem.IsMacOS()
                ? "osx"
                : "linux";
        string architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException(
                $"The packaged output-context regression does not define a runtime identifier for {RuntimeInformation.ProcessArchitecture}."),
        };

        return $"{operatingSystem}-{architecture}";
    }

    private static ArchitectureContractDocument CreateDocument(List<string> sharedFrameworks) => new()
    {
        Version = 1,
        Name = "Test",
        Analysis = new ArchitectureAnalysisConfiguration
        {
            TargetAssemblies = new List<string> { "Synthetic.AspNetHost" },
            SharedFrameworks = sharedFrameworks,
        },
    };

    private static ArchitectureRunnerSetupService CreateService(IArchitectureProjectDiscoveryService discovery) =>
        new(
            new ArchitecturePolicyDocumentLoader(),
            new ArchitectureBaselineLoadingService(),
            new ArchitectureRepositoryRootResolver(),
            new ConditionSetResolutionService(),
            discovery,
            new ArchitectureAssemblyResolutionService());
}
