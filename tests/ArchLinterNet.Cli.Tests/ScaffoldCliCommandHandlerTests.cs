using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Infrastructure;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class ScaffoldCliCommandHandlerTests
{
    [Test]
    public void Execute_DryRun_ListsOnlyTheMinimalFeatureOwnedFiles()
    {
        var console = new RecordingConsole();
        var fileSystem = new RecordingFileSystem();

        int exitCode = new ScaffoldCliCommandHandler(console, fileSystem).Execute(Options(dryRun: true));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.CommittedPaths, Is.Empty);
            Assert.That(console.StdOut, Does.Contain("Dry run — no files written."));
            Assert.That(console.StdOut, Does.Contain("Commands/Inspect/EntryPoint/InspectCommandModule.cs"));
            Assert.That(console.StdOut, Does.Contain("Commands/Inspect/Application/InspectCommandHandler.cs"));
            Assert.That(console.StdOut, Does.Contain("Scaffolded/InspectCommandScaffoldTests.cs"));
            Assert.That(console.StdOut, Does.Not.Contain("Commands/Inspect/Models"));
            Assert.That(console.StdOut, Does.Not.Contain("Program.cs"));
            Assert.That(console.StdErr, Is.Empty);
        });
    }

    [Test]
    public void Execute_RequestedConventionTypes_WritesOnlyTheirDeclaredFolders()
    {
        var console = new RecordingConsole();
        var fileSystem = new RecordingFileSystem();
        ScaffoldCliCommandOptions options = Options(
            modelName: "Inspection",
            abstractionName: "IInspectionReader",
            exceptionName: "InspectionException");

        int exitCode = new ScaffoldCliCommandHandler(console, fileSystem).Execute(options);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.CommittedPaths, Has.Count.EqualTo(6));
            Assert.That(fileSystem.Contents[
                "src/ArchLinterNet.Cli/Commands/Inspect/Models/Inspection.cs"],
                Does.Contain("namespace ArchLinterNet.Cli.Commands.Inspect.Models;"));
            Assert.That(fileSystem.Contents[
                "src/ArchLinterNet.Cli/Commands/Inspect/Abstractions/IInspectionReader.cs"],
                Does.Contain("internal interface IInspectionReader"));
            Assert.That(fileSystem.Contents[
                "src/ArchLinterNet.Cli/Commands/Inspect/Exceptions/InspectionException.cs"],
                Does.Contain("class InspectionException : Exception"));
        });
    }

    [Test]
    public void Execute_CollisionFailsBeforeAnyWriteUnlessForceIsExplicit()
    {
        const string EntryPointPath = "src/ArchLinterNet.Cli/Commands/Inspect/EntryPoint/InspectCommandModule.cs";
        var console = new RecordingConsole();
        var fileSystem = new RecordingFileSystem(EntryPointPath);
        var handler = new ScaffoldCliCommandHandler(console, fileSystem);

        int rejectedExitCode = handler.Execute(Options());
        int forcedExitCode = handler.Execute(Options(force: true));

        Assert.Multiple(() =>
        {
            Assert.That(rejectedExitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.StdErr, Does.Contain(EntryPointPath));
            Assert.That(forcedExitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.CommittedPaths, Has.Count.EqualTo(3));
        });
    }

    [Test]
    public void Execute_InvalidNamesFailBeforeAnyWrite()
    {
        var console = new RecordingConsole();
        var fileSystem = new RecordingFileSystem();

        int exitCode = new ScaffoldCliCommandHandler(console, fileSystem).Execute(
            Options(moduleName: "inspect", commandToken: "Inspect"));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(fileSystem.CommittedPaths, Is.Empty);
            Assert.That(console.StdErr, Does.Contain("--module"));
        });
    }

    [Test]
    public void Execute_TwoGeneratedModules_CompilePassPolicyAndComposeWithoutCentralRegistration()
    {
        var console = new RecordingConsole();
        var fileSystem = new RecordingFileSystem();
        var handler = new ScaffoldCliCommandHandler(console, fileSystem);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Execute(Options(moduleName: "Inspect", commandToken: "inspect")), Is.EqualTo(CliExitCodes.Success));
            Assert.That(handler.Execute(Options(moduleName: "Repair", commandToken: "repair")), Is.EqualTo(CliExitCodes.Success));
        });

        Assembly generatedAssembly = CompileGeneratedModules(fileSystem);
        var analysisContext = new ArchitectureAnalysisContext(
            Path.GetTempPath(),
            new[] { generatedAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());
        var moduleContract = new ArchitectureModuleContainerContract
        {
            Id = "generated-cli-command-modules",
            Name = "Generated CLI command modules",
            Container = "ArchLinterNet.Cli.Commands",
            Profile = "cli_command",
        };
        var policy = new ArchitectureContractDocument
        {
            Name = "Generated modules",
            Contracts = new ArchitectureContractGroups
            {
                StrictModuleContainers = { moduleContract },
            },
        };

        try
        {
            List<ArchitectureViolation> violations = new ArchitectureContractRunner(analysisContext, policy)
                .CheckModuleContainerContract(moduleContract);
            IReadOnlyList<ITopLevelCliSubcommandModule> modules = CliCommandModuleCatalog
                .CreateSubcommandModules(generatedAssembly);

            Assert.Multiple(() =>
            {
                Assert.That(violations, Is.Empty);
                Assert.That(modules.Select(module => module.CommandName), Is.EqualTo(new[] { "inspect", "repair" }));
                Assert.That(console.StdOut, Does.Not.Contain("Program.cs"));
                Assert.That(console.StdOut, Does.Not.Contain("registry"));
            });
        }
        finally
        {
            analysisContext.Dispose();
        }
    }

    private static ScaffoldCliCommandOptions Options(
        string moduleName = "Inspect",
        string commandToken = "inspect",
        bool dryRun = false,
        bool force = false,
        string? modelName = null,
        string? abstractionName = null,
        string? exceptionName = null) =>
        new("cli-command", moduleName, commandToken, dryRun, force, modelName, abstractionName, exceptionName);

    private static Assembly CompileGeneratedModules(RecordingFileSystem fileSystem)
    {
        SyntaxTree[] syntaxTrees = fileSystem.Contents
            .Where(entry => entry.Key.StartsWith("src/", StringComparison.Ordinal))
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => CSharpSyntaxTree.ParseText(entry.Value))
            .ToArray();
        MetadataReference[] references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic
                && !string.IsNullOrEmpty(assembly.Location)
                && !ReferenceEquals(assembly, typeof(ScaffoldCliCommandHandlerTests).Assembly))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .DistinctBy(reference => reference.Display, StringComparer.Ordinal)
            .ToArray();
        CSharpCompilation compilation = CSharpCompilation.Create(
            "ArchLinterNet.Cli.Tests",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();

        Microsoft.CodeAnalysis.Emit.EmitResult emission = compilation.Emit(stream);
        Assert.That(
            emission.Success,
            Is.True,
            string.Join(Environment.NewLine, emission.Diagnostics.Select(diagnostic => diagnostic.ToString())));

        stream.Position = 0;
        return new AssemblyLoadContext($"generated-cli-command-modules-{Guid.NewGuid():N}", isCollectible: true)
            .LoadFromStream(stream);
    }

    private sealed class RecordingConsole : ICliConsole
    {
        private readonly StringBuilder _standardOutput = new();
        private readonly StringBuilder _standardError = new();

        public TextWriter Out => new StringWriter(_standardOutput);

        public TextWriter Error => new StringWriter(_standardError);

        public string StdOut => _standardOutput.ToString();

        public string StdErr => _standardError.ToString();
    }

    private sealed class RecordingFileSystem(params string[] existingPaths) : IFileSystem
    {
        private readonly HashSet<string> _existingPaths = new(existingPaths, StringComparer.Ordinal);
        private readonly Dictionary<string, string> _temporaryContents = new(StringComparer.Ordinal);

        public List<string> CommittedPaths { get; } = new();

        public Dictionary<string, string> Contents { get; } = new(StringComparer.Ordinal);

        public bool FileExists(string path) => _existingPaths.Contains(path);

        public string ReadAllText(string path) => Contents[path];

        public void WriteAllText(string path, string contents)
        {
            Contents[path] = contents;
            _existingPaths.Add(path);
        }

        public string WriteAllTextToTemp(string targetPath, string contents)
        {
            string temporaryPath = targetPath + ".tmp";
            _temporaryContents[temporaryPath] = contents;
            return temporaryPath;
        }

        public void RenameTempToTarget(string tempPath, string targetPath)
        {
            Contents[targetPath] = _temporaryContents[tempPath];
            _existingPaths.Add(targetPath);
            CommittedPaths.Add(targetPath);
        }

        public void DeleteFile(string path)
        {
            Contents.Remove(path);
            _existingPaths.Remove(path);
        }

        public bool CanWriteToDirectory(string path) => true;
    }
}
