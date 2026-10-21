using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Xml.Linq;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Exercises the shipped annotations package through the same SDK/NuGet path used by consumers.
/// The package project is intentionally discovered at runtime so this test remains useful while the
/// package is being assembled and does not create a project reference from the Core test project.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class AnnotationsPackageConsumerTests
{
    private static readonly string[] _expectedSourceFiles =
    [
        "ContextAttributes.cs",
        "CqrsMessagingAttributes.cs",
        "DddAttributes.cs",
        "LayeredCleanAttributes.cs",
        "PortsInfrastructureAttributes.cs",
        "AnnotationCatalogIdentityAttribute.cs",
        "RoleAnnotationBaseAttribute.cs",
        "UiAttributes.cs",
        "UnityAttributes.cs",
        "WebApiAttributes.cs"
    ];

    private string _root = null!;
    private string _feed = null!;
    private string _config = null!;
    private string _packages = null!;
    private string _packagePath = null!;
    private string _packageVersion = null!;

    [OneTimeSetUp]
    public void PackAnnotationsPackage()
    {
        string repositoryRoot = FindRepositoryRoot();
        string packageProject = Path.Combine(repositoryRoot, "src", "ArchLinterNet.Annotations", "ArchLinterNet.Annotations.csproj");
        if (!File.Exists(packageProject))
        {
            Assert.Ignore("ArchLinterNet.Annotations.csproj is not available yet; packed-consumer proof is deferred until the package project is ready.");
        }

        _root = Path.Combine(Path.GetTempPath(), $"arch-linter-annotations-package-{Guid.NewGuid():N}");
        _feed = Path.Combine(_root, "feed");
        _packages = Path.Combine(_root, "packages");
        Directory.CreateDirectory(_feed);
        Directory.CreateDirectory(_packages);

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(repositoryRoot, "architecture", "semantic-annotations.v1.json")));
        _packageVersion = manifest.RootElement.GetProperty("package").GetProperty("version").GetString()!;

        _ = RunDotnet(repositoryRoot, "pack", packageProject,
            "--configuration", "Release", "--output", _feed, "--nologo", "--verbosity", "quiet",
            "-p:PackageVersion=" + _packageVersion, "-p:Version=" + _packageVersion);
        string[] packages = Directory.GetFiles(_feed, "ArchLinterNet.Annotations.*.nupkg");
        Assert.That(packages, Has.Length.EqualTo(1), string.Join(Environment.NewLine, packages));
        Assert.That(Directory.GetFiles(_feed, "ArchLinterNet.Annotations.*.snupkg"), Is.Empty,
            "The source-only package does not have a symbol package.");
        _packagePath = packages[0];

        _config = Path.Combine(_root, "NuGet.Config");
        File.WriteAllText(_config, $"""
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="annotations" value="{_feed}" />
  </packageSources>
</configuration>
""");
    }

    [OneTimeTearDown]
    public void DeleteTemporaryPackageFeed()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Test]
    public void PackedPackage_HasOnlyApprovedCompileContentAndExactCatalogMarker()
    {
        using ZipArchive package = ZipFile.OpenRead(_packagePath);
        ZipArchiveEntry nuspecEntry = package.GetEntry("ArchLinterNet.Annotations.nuspec")
            ?? throw new AssertionException("The packed annotations package has no nuspec.");
        XDocument nuspec = XDocument.Load(nuspecEntry.Open());

        Assert.That(package.GetEntry("README.md"), Is.Not.Null, "The package must carry its source-only usage guidance.");
        XElement description = nuspec.Descendants().Single(element => element.Name.LocalName == "description");
        Assert.That(description.Value, Does.Contain("Optional source-only"));
        Assert.That(description.Value, Does.Contain("No runtime assembly"));

        Assert.That(nuspec.Descendants().Any(element => element.Name.LocalName == "dependency"), Is.False,
            "Source-only annotations must not introduce package dependencies.");
        XElement[] contentFiles = nuspec.Descendants().Where(element => element.Name.LocalName == "files").ToArray();
        Assert.That(contentFiles, Has.Length.EqualTo(_expectedSourceFiles.Length + 1));
        string[] nuspecSourceFiles = contentFiles
            .Select(element => element.Attribute("include")?.Value ?? string.Empty)
            .Select(path => path.StartsWith("cs/any/", StringComparison.Ordinal) ? path["cs/any/".Length..] : path)
            .ToArray();
        Assert.That(nuspecSourceFiles, Is.EquivalentTo(_expectedSourceFiles.Append("AnnotationCatalogIdentityAssemblyAttribute.cs")));
        Assert.Multiple(() =>
        {
            Assert.That(contentFiles.All(element => element.Attribute("include")?.Value.StartsWith("cs/any/", StringComparison.Ordinal) == true), Is.True);
            Assert.That(contentFiles.All(element => element.Attribute("buildAction")?.Value == "Compile"), Is.True);
            Assert.That(contentFiles.All(element => element.Attribute("copyToOutput")?.Value == "false"), Is.True);
            Assert.That(nuspec.Descendants().Any(element => element.Name.LocalName is "dependency" or "frameworkAssembly"), Is.False);
        });

        string[] sourceEntries = package.Entries
            .Where(entry => entry.FullName.StartsWith("contentFiles/cs/any/", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.FullName["contentFiles/cs/any/".Length..])
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.That(sourceEntries.Count(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)), Is.EqualTo(_expectedSourceFiles.Length + 1));
        string[] expectedEntries = sourceEntries.Where(path => _expectedSourceFiles.Contains(path, StringComparer.OrdinalIgnoreCase)).ToArray();
        Assert.That(expectedEntries, Is.EquivalentTo(_expectedSourceFiles));
        string markerPath = sourceEntries.Single(path => !_expectedSourceFiles.Contains(path, StringComparer.OrdinalIgnoreCase));
        string markerSource = ReadEntry(package, "contentFiles/cs/any/" + markerPath);
        string identityTypeSource = ReadEntry(package, "contentFiles/cs/any/AnnotationCatalogIdentityAttribute.cs");
        Assert.Multiple(() =>
        {
            Assert.That(markerSource, Does.Contain("AnnotationCatalogIdentityAttribute"));
            Assert.That(markerSource, Does.Contain("(1, \"" + _packageVersion + "\")"));
            Assert.That(identityTypeSource, Does.Contain("internal sealed class AnnotationCatalogIdentityAttribute"));
        });

        string[] forbiddenRoots = ["lib/", "ref/", "runtimes/", "native/", "analyzers/", "build/", "buildTransitive/", "tools/"];
        Assert.That(package.Entries.Any(entry => forbiddenRoots.Any(root => entry.FullName.StartsWith(root, StringComparison.OrdinalIgnoreCase))), Is.False);

        XDocument packageDocument = XDocument.Load(nuspecEntry.Open());
        Assert.That(packageDocument.Descendants().Single(element => element.Name.LocalName == "version").Value, Is.EqualTo(_packageVersion));
    }

    [Test]
    [CancelAfter(60_000)]
    public void PackageReference_CompilesTwoIndependentConsumersWithoutRuntimeOrPublicAnnotationTypes()
    {
        ConsumerBuild first = BuildConsumer("consumer-a", """
using ArchLinterNet.Annotations;

[assembly: DomainLayer(BoundedContext = "Commerce", Module = "Orders")]

namespace ConsumerA
{
    [AggregateRoot(BoundedContext = "Commerce", Module = "Orders")]
    public sealed class Order { }

    [Domain("Commerce")]
    [BoundedContext("Orders")]
    public sealed class OrderContext { }
}
""");
        ConsumerBuild second = BuildConsumer("consumer-b", """
using ArchLinterNet.Annotations;

[assembly: UnityRuntime(Platform = "Unity", Runtime = "Client")]

namespace ConsumerB
{
    [Command(Domain = "Commerce", BoundedContext = "Orders")]
    public sealed class CreateOrder { }
}
""");

        Guid firstModule = AssertConsumerAssembly(first, "ConsumerA.Order", "AggregateRootAttribute", _packageVersion);
        Guid secondModule = AssertConsumerAssembly(second, "ConsumerB.CreateOrder", "CommandAttribute", _packageVersion);
        Assert.That(firstModule, Is.Not.EqualTo(secondModule), "Each opting-in project must compile its own annotation definitions.");
    }

    [Test]
    [CancelAfter(60_000)]
    public void TransitivePackageDependency_DoesNotInjectAnnotationSources()
    {
        string dependencyDirectory = Path.Combine(_root, "transitive-dependency");
        Directory.CreateDirectory(dependencyDirectory);
        string dependencyProject = Path.Combine(dependencyDirectory, "TransitiveDependency.csproj");
        File.WriteAllText(dependencyProject, $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <PackageId>ArchLinterNet.Annotations.TransitiveProbe</PackageId>
    <PackageVersion>1.0.0</PackageVersion>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <IncludeSymbols>false</IncludeSymbols>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="ArchLinterNet.Annotations" Version="{_packageVersion}" />
  </ItemGroup>
</Project>
""");
        _ = RunDotnet(dependencyDirectory, "restore", dependencyProject, "--configfile", _config, "--packages", _packages, "--nologo", "--verbosity", "quiet");
        _ = RunDotnet(dependencyDirectory, "pack", dependencyProject, "--no-restore", "--output", _feed, "--nologo", "--verbosity", "quiet");

        string consumerDirectory = Path.Combine(_root, "transitive-consumer");
        Directory.CreateDirectory(consumerDirectory);
        string consumerProject = Path.Combine(consumerDirectory, "TransitiveConsumer.csproj");
        File.WriteAllText(consumerProject, """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>9.0</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="ArchLinterNet.Annotations.TransitiveProbe" Version="1.0.0" />
  </ItemGroup>
</Project>
""");
        File.WriteAllText(Path.Combine(consumerDirectory, "Consumer.cs"), "using ArchLinterNet.Annotations; namespace TransitiveConsumer { [Entity] public sealed class Order { } }");
        _ = RunDotnet(consumerDirectory, "restore", consumerProject, "--configfile", _config, "--packages", _packages, "--nologo", "--verbosity", "quiet");
        string output = RunDotnetExpectingFailure(consumerDirectory,
            "build", consumerProject, "--no-restore", "--configuration", "Release", "--nologo", "--verbosity", "quiet");

        Assert.That(output, Does.Contain("CS0246"), "Only a direct package reference should add annotation source files.");
    }

    [Test]
    [CancelAfter(60_000)]
    public void RemovingPackageReference_CleanRebuildLeavesNoPackageArtifacts()
    {
        ConsumerBuild consumer = BuildConsumer("removable-consumer", """
using ArchLinterNet.Annotations;

namespace RemovableConsumer
{
    [Entity]
    public sealed class Order { }
}
""");

        File.WriteAllText(consumer.ProjectPath, """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>9.0</LangVersion>
  </PropertyGroup>
</Project>
""");
        File.WriteAllText(consumer.SourcePath, "namespace RemovableConsumer { public sealed class Order { } }");
        _ = RunDotnet(consumer.Directory, "restore", consumer.ProjectPath, "--configfile", _config, "--packages", _packages, "--nologo", "--verbosity", "quiet");
        _ = RunDotnet(consumer.Directory, "clean", consumer.ProjectPath, "--nologo", "--verbosity", "quiet");
        _ = RunDotnet(consumer.Directory, "build", consumer.ProjectPath, "--no-restore", "--configuration", "Release", "--nologo", "--verbosity", "quiet");

        Assert.That(Directory.GetFiles(consumer.OutputDirectory, "*", SearchOption.AllDirectories)
            .Any(path => Path.GetFileName(path).Equals("ArchLinterNet.Annotations.dll", StringComparison.OrdinalIgnoreCase)), Is.False);
        string assetsPath = Path.Combine(consumer.Directory, "obj", "project.assets.json");
        using JsonDocument assets = JsonDocument.Parse(File.ReadAllText(assetsPath));
        Assert.That(assets.RootElement.GetProperty("libraries").EnumerateObject()
            .Any(library => library.Name.StartsWith("ArchLinterNet.Annotations/", StringComparison.Ordinal)), Is.False);
        Assert.That(assets.RootElement.GetProperty("targets").EnumerateObject()
            .SelectMany(target => target.Value.EnumerateObject())
            .Any(library => library.Name.StartsWith("ArchLinterNet.Annotations/", StringComparison.Ordinal)), Is.False);
    }

    private ConsumerBuild BuildConsumer(string name, string source)
    {
        string directory = Path.Combine(_root, name);
        Directory.CreateDirectory(directory);
        string projectPath = Path.Combine(directory, name + ".csproj");
        string sourcePath = Path.Combine(directory, "Consumer.cs");
        File.WriteAllText(projectPath, """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>9.0</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="ArchLinterNet.Annotations" />
  </ItemGroup>
</Project>
""");
        File.WriteAllText(sourcePath, source);
        File.WriteAllText(Path.Combine(directory, "Directory.Packages.props"), $"""
<Project>
  <PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup>
  <ItemGroup><PackageVersion Include="ArchLinterNet.Annotations" Version="{_packageVersion}" /></ItemGroup>
</Project>
""");
        _ = RunDotnet(directory, "restore", projectPath, "--configfile", _config, "--packages", _packages, "--nologo", "--verbosity", "quiet");
        _ = RunDotnet(directory, "build", projectPath, "--no-restore", "--configuration", "Release", "--nologo", "--verbosity", "quiet");
        return new ConsumerBuild(directory, projectPath, sourcePath);
    }

    private static Guid AssertConsumerAssembly(ConsumerBuild consumer, string annotatedTypeName, string roleAttributeName, string expectedVersion)
    {
        Assert.That(File.Exists(consumer.AssemblyPath), Is.True, consumer.AssemblyPath);
        string[] outputFiles = Directory.GetFiles(consumer.OutputDirectory, "*", SearchOption.AllDirectories);
        Assert.That(outputFiles.Any(path => Path.GetFileName(path).Equals("ArchLinterNet.Annotations.dll", StringComparison.OrdinalIgnoreCase)), Is.False);
        string depsPath = Directory.GetFiles(consumer.OutputDirectory, "*.deps.json").Single();
        Assert.That(File.ReadAllText(depsPath), Does.Not.Contain("ArchLinterNet.Annotations"));

        AssemblyLoadContext loadContext = new($"annotations-consumer-{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            using FileStream assemblyStream = File.OpenRead(consumer.AssemblyPath);
            Assembly assembly = loadContext.LoadFromStream(assemblyStream);
            Type markerType = assembly.GetType("ArchLinterNet.Annotations.AnnotationCatalogIdentityAttribute", throwOnError: true)!;
            CustomAttributeData[] markers = assembly.GetCustomAttributesData()
                .Where(attribute => attribute.AttributeType.FullName == markerType.FullName)
                .ToArray();
            Assert.That(markers, Has.Length.EqualTo(1));
            Assert.That(markers[0].ConstructorArguments.Select(argument => argument.Value).ToArray(), Is.EqualTo(new object?[] { 1, expectedVersion }));
            Assert.That(assembly.GetExportedTypes().Any(type => type.Namespace == "ArchLinterNet.Annotations"), Is.False);
            Assert.That(assembly.GetReferencedAssemblies().Any(name => name.Name == "ArchLinterNet.Annotations"), Is.False);

            Type annotatedType = assembly.GetType(annotatedTypeName, throwOnError: true)!;
            Assert.That(annotatedType.GetCustomAttributesData().Any(attribute => attribute.AttributeType.Name == roleAttributeName), Is.True);
            return assembly.ManifestModule.ModuleVersionId;
        }
        finally
        {
            loadContext.Unload();
        }
    }

    private static string ReadEntry(ZipArchive package, string path)
    {
        ZipArchiveEntry entry = package.GetEntry(path) ?? throw new AssertionException($"Missing package entry '{path}'.");
        using StreamReader reader = new(entry.Open());
        return reader.ReadToEnd();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArchLinterNet.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Unable to locate ArchLinterNet repository root.");
    }

    private static string RunDotnet(string workingDirectory, params string[] arguments)
    {
        return RunDotnetExpectingExitCode(workingDirectory, 0, arguments);
    }

    private static string RunDotnetExpectingFailure(string workingDirectory, params string[] arguments)
    {
        return RunDotnetExpectingExitCode(workingDirectory, 1, arguments);
    }

    private static string RunDotnetExpectingExitCode(string workingDirectory, int expectedExitCode, params string[] arguments)
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start dotnet.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        string combined = output.GetAwaiter().GetResult() + Environment.NewLine + error.GetAwaiter().GetResult();
        Assert.That(process.ExitCode, Is.EqualTo(expectedExitCode), combined);
        return combined;
    }

    private sealed class ConsumerBuild
    {
        public ConsumerBuild(string directory, string projectPath, string sourcePath)
        {
            Directory = directory;
            ProjectPath = projectPath;
            SourcePath = sourcePath;
        }

        public string Directory { get; }
        public string ProjectPath { get; }
        public string SourcePath { get; }
        public string OutputDirectory => Path.Combine(Directory, "bin", "Release", "net10.0");
        public string AssemblyPath => Path.Combine(OutputDirectory, Path.GetFileNameWithoutExtension(ProjectPath) + ".dll");
    }
}
