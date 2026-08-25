using ArchLinterNet.Cli;
using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Scaffold.Application;

internal sealed class ScaffoldCliCommandHandler(ICliConsole console, IFileSystem fileSystem)
{
    private const string CommandContainerNamespace = "ArchLinterNet.Cli.Commands";
    private const string CommandContainerPath = "src/ArchLinterNet.Cli/Commands";
    private const string TestPath = "tests/ArchLinterNet.Cli.Tests/Scaffolded";

    public int Execute(ScaffoldCliCommandOptions options)
    {
        try
        {
            IReadOnlyList<ScaffoldFile> files = CreatePlan(options);
            if (options.DryRun)
            {
                EnsureNoCollisions(files, options.Force);
                WritePlan(files, dryRun: true, force: options.Force);
                return CliExitCodes.Success;
            }

            string lockPath = GetScaffoldLockPath();
            if (!fileSystem.TryCreateNewFile(lockPath))
            {
                throw new InvalidOperationException(
                    $"Scaffold is already running for this repository. Wait for it to finish before creating module '{options.ModuleName}'.");
            }

            bool operationCompleted = false;
            try
            {
                EnsureNoCollisions(files, options.Force);
                WritePlan(files, dryRun: false, force: options.Force);
                operationCompleted = true;
                return CliExitCodes.Success;
            }
            finally
            {
                ReleaseScaffoldLock(lockPath, operationCompleted);
            }
        }
        catch (ArgumentException exception)
        {
            console.Error.WriteLine(exception.Message);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
        catch (InvalidOperationException exception)
        {
            console.Error.WriteLine(exception.Message);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    internal static IReadOnlyList<ScaffoldFile> CreatePlan(ScaffoldCliCommandOptions options)
    {
        if (!string.Equals(options.Profile, "cli-command", StringComparison.Ordinal))
        {
            throw new ArgumentException("Only scaffold profile 'cli-command' is supported.");
        }

        string moduleName = RequirePascalCase(options.ModuleName, "--module");
        string commandToken = RequireCommandToken(options.CommandToken);
        string moduleNamespace = $"{CommandContainerNamespace}.{moduleName}";
        string modulePath = CombineRepositoryPath(CommandContainerPath, moduleName);
        var files = new List<ScaffoldFile>
        {
            new(
                CombineRepositoryPath(modulePath, "EntryPoint", $"{moduleName}CommandModule.cs"),
                $"{moduleNamespace}.EntryPoint",
                EntryPointTemplate(moduleName, commandToken, moduleNamespace)),
            new(
                CombineRepositoryPath(modulePath, "Application", $"{moduleName}CommandHandler.cs"),
                $"{moduleNamespace}.Application",
                ApplicationTemplate(moduleName, moduleNamespace)),
            new(
                CombineRepositoryPath(TestPath, $"{moduleName}CommandScaffoldTests.cs"),
                "ArchLinterNet.Cli.Tests.Scaffolded",
                TestTemplate(moduleName, commandToken, moduleNamespace)),
        };

        AddOptionalModel(files, options.ModelName, modulePath, moduleNamespace);
        AddOptionalAbstraction(files, options.AbstractionName, modulePath, moduleNamespace);
        AddOptionalException(files, options.ExceptionName, modulePath, moduleNamespace);
        return files;
    }

    private void EnsureNoCollisions(IEnumerable<ScaffoldFile> files, bool force)
    {
        if (force)
        {
            return;
        }

        ScaffoldFile? collision = files.FirstOrDefault(file => fileSystem.FileExists(file.Path));
        if (collision != null)
        {
            throw new InvalidOperationException(
                $"Scaffold target already exists: '{collision.Path}'. Re-run with --force only after reviewing the existing file.");
        }
    }

    private static string GetScaffoldLockPath() =>
        CombineRepositoryPath(CommandContainerPath, ".scaffold.lock");

    private void WritePlan(IReadOnlyList<ScaffoldFile> files, bool dryRun, bool force)
    {
        if (dryRun)
        {
            console.Out.WriteLine("Dry run — no files written.");

            foreach (ScaffoldFile file in files.OrderBy(static file => file.Path, StringComparer.Ordinal))
            {
                console.Out.WriteLine($"Would create {file.Path} ({file.Namespace})");
            }

            console.Out.WriteLine("Run 'make lint-architecture' before committing the generated module.");
            return;
        }

        if (force)
        {
            WritePlanWithForce(files);
        }
        else
        {
            WritePlanWithoutForce(files);
        }

        console.Out.WriteLine("Run 'make lint-architecture' before committing the generated module.");
    }

    private void WritePlanWithForce(IReadOnlyList<ScaffoldFile> files)
    {
        foreach (ScaffoldFile file in files.OrderBy(static file => file.Path, StringComparer.Ordinal))
        {
            string temporaryPath = fileSystem.WriteAllTextToTemp(file.Path, file.Contents);
            fileSystem.RenameTempToTarget(temporaryPath, file.Path);
        }

        foreach (ScaffoldFile file in files.OrderBy(static file => file.Path, StringComparer.Ordinal))
        {
            console.Out.WriteLine($"Created {file.Path} ({file.Namespace})");
        }
    }

    private void WritePlanWithoutForce(IReadOnlyList<ScaffoldFile> files)
    {
        var createdFiles = new List<ScaffoldFile>();
        var createdDirectories = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            foreach (ScaffoldFile file in files.OrderBy(static file => file.Path, StringComparer.Ordinal))
            {
                RecordMissingParentDirectories(file.Path, createdDirectories);
                string temporaryPath = fileSystem.WriteAllTextToTemp(file.Path, file.Contents);
                if (!fileSystem.TryRenameTempToNewTarget(temporaryPath, file.Path))
                {
                    DeleteTemporaryFileBestEffort(temporaryPath);
                    throw new InvalidOperationException(
                        $"Scaffold target already exists: '{file.Path}'. Re-run with --force only after reviewing the existing file.");
                }

                createdFiles.Add(file);
            }
        }
        catch
        {
            RollBackCreatedFilesBestEffort(createdFiles);
            RollBackCreatedDirectoriesBestEffort(createdDirectories);
            throw;
        }

        foreach (ScaffoldFile file in createdFiles)
        {
            console.Out.WriteLine($"Created {file.Path} ({file.Namespace})");
        }
    }

    private void RecordMissingParentDirectories(string path, ISet<string> createdDirectories)
    {
        for (string? directory = Path.GetDirectoryName(path);
             !string.IsNullOrEmpty(directory) && !fileSystem.DirectoryExists(directory);
             directory = Path.GetDirectoryName(directory))
        {
            createdDirectories.Add(directory);
        }
    }

    // The repository-scoped scaffold lock serializes ordinary scaffold plans from preflight
    // through rollback. Together with atomic create-if-absent finalization, this prevents one
    // scaffold invocation from observing or deleting another invocation's output.
    private void RollBackCreatedFilesBestEffort(IEnumerable<ScaffoldFile> createdFiles)
    {
        foreach (ScaffoldFile file in createdFiles.Reverse())
        {
            try
            {
                if (string.Equals(fileSystem.ReadAllText(file.Path), file.Contents, StringComparison.Ordinal))
                {
                    fileSystem.DeleteFile(file.Path);
                }
            }
            catch
            {
                // Preserve the original scaffold failure. A file whose ownership cannot be
                // verified is intentionally retained for manual review rather than deleted.
            }
        }
    }

    private void RollBackCreatedDirectoriesBestEffort(IEnumerable<string> createdDirectories)
    {
        foreach (string directory in createdDirectories.OrderByDescending(static directory => directory.Length))
        {
            try
            {
                fileSystem.DeleteDirectoryIfEmpty(directory);
            }
            catch
            {
                // A non-empty or externally changed directory is deliberately retained rather
                // than removed during best-effort rollback.
            }
        }
    }

    private void ReleaseScaffoldLock(string lockPath, bool operationCompleted)
    {
        try
        {
            fileSystem.DeleteFile(lockPath);
        }
        catch (Exception exception)
        {
            string message =
                $"Scaffold lock '{lockPath}' could not be removed. Remove it manually after confirming no scaffold is running.";
            if (operationCompleted)
            {
                throw new InvalidOperationException(message, exception);
            }

            console.Error.WriteLine($"Additionally, {message}");
        }
    }

    private void DeleteTemporaryFileBestEffort(string path)
    {
        try
        {
            fileSystem.DeleteFile(path);
        }
        catch
        {
            // The target collision remains the actionable error; a leftover temporary file can
            // be removed manually if the filesystem rejected cleanup.
        }
    }

    private static void AddOptionalModel(
        ICollection<ScaffoldFile> files, string? modelName, string modulePath, string moduleNamespace)
    {
        if (string.IsNullOrEmpty(modelName))
        {
            return;
        }

        string name = RequirePascalCase(modelName, "--model");
        files.Add(new ScaffoldFile(
            CombineRepositoryPath(modulePath, "Models", $"{name}.cs"),
            $"{moduleNamespace}.Models",
            $"namespace {moduleNamespace}.Models;\n\ninternal sealed record {name};\n"));
    }

    private static void AddOptionalAbstraction(
        ICollection<ScaffoldFile> files, string? abstractionName, string modulePath, string moduleNamespace)
    {
        if (string.IsNullOrEmpty(abstractionName))
        {
            return;
        }

        string name = RequirePascalCase(abstractionName, "--abstraction");
        if (!name.StartsWith('I') || name.Length == 1)
        {
            throw new ArgumentException("--abstraction must be a PascalCase interface name beginning with 'I'.");
        }

        files.Add(new ScaffoldFile(
            CombineRepositoryPath(modulePath, "Abstractions", $"{name}.cs"),
            $"{moduleNamespace}.Abstractions",
            $"namespace {moduleNamespace}.Abstractions;\n\ninternal interface {name};\n"));
    }

    private static void AddOptionalException(
        ICollection<ScaffoldFile> files, string? exceptionName, string modulePath, string moduleNamespace)
    {
        if (string.IsNullOrEmpty(exceptionName))
        {
            return;
        }

        string name = RequirePascalCase(exceptionName, "--exception");
        if (!name.EndsWith("Exception", StringComparison.Ordinal))
        {
            throw new ArgumentException("--exception must be a PascalCase exception name ending with 'Exception'.");
        }

        files.Add(new ScaffoldFile(
            CombineRepositoryPath(modulePath, "Exceptions", $"{name}.cs"),
            $"{moduleNamespace}.Exceptions",
            $"namespace {moduleNamespace}.Exceptions;\n\ninternal sealed class {name} : Exception;\n"));
    }

    private static string RequirePascalCase(string? value, string optionName)
    {
        if (string.IsNullOrEmpty(value)
            || !char.IsUpper(value[0])
            || value.Any(character => !char.IsLetterOrDigit(character)))
        {
            throw new ArgumentException($"{optionName} must be a non-empty PascalCase identifier containing only letters and digits.");
        }

        return value;
    }

    private static string CombineRepositoryPath(params string[] segments) => string.Join('/', segments);

    private static string RequireCommandToken(string? value)
    {
        if (string.IsNullOrEmpty(value)
            || !char.IsLower(value[0])
            || value.Any(character => !(char.IsLower(character) || char.IsDigit(character) || character == '-')))
        {
            throw new ArgumentException("--command must be a lower-case command token containing only letters, digits, and hyphens.");
        }

        return value;
    }

    private static string EntryPointTemplate(string moduleName, string commandToken, string moduleNamespace) =>
        $$"""
          using System.CommandLine;
          using System.Threading;
          using ArchLinterNet.Cli.Abstractions;
          using {{moduleNamespace}}.Application;

          namespace {{moduleNamespace}}.EntryPoint;

          internal sealed class {{moduleName}}CommandModule : ITopLevelCliSubcommandModule
          {
              public string CommandName => "{{commandToken}}";

              public Command CreateCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
              {
                  var command = new Command(CommandName);
                  command.SetAction(_ => new {{moduleName}}CommandHandler().Execute());
                  return command;
              }
          }
          """;

    private static string ApplicationTemplate(string moduleName, string moduleNamespace) =>
        $$"""
          using ArchLinterNet.Cli;

          namespace {{moduleNamespace}}.Application;

          internal sealed class {{moduleName}}CommandHandler
          {
              public int Execute() => CliExitCodes.Success;
          }
          """;

    private static string TestTemplate(string moduleName, string commandToken, string moduleNamespace) =>
        $$"""
          using NUnit.Framework;
          using {{moduleNamespace}}.EntryPoint;

          namespace ArchLinterNet.Cli.Tests.Scaffolded;

          [TestFixture]
          public sealed class {{moduleName}}CommandScaffoldTests
          {
              [Test]
              public void Module_UsesTheExpectedCommandToken()
              {
                  Assert.That(new {{moduleName}}CommandModule().CommandName, Is.EqualTo("{{commandToken}}"));
              }
          }
          """;

    internal sealed record ScaffoldFile(string Path, string Namespace, string Contents);
}
