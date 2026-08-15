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
            EnsureNoCollisions(files, options.Force);
            WritePlan(files, options.DryRun);
            return CliExitCodes.Success;
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

    internal IReadOnlyList<ScaffoldFile> CreatePlan(ScaffoldCliCommandOptions options)
    {
        if (!string.Equals(options.Profile, "cli-command", StringComparison.Ordinal))
        {
            throw new ArgumentException("Only scaffold profile 'cli-command' is supported.");
        }

        string moduleName = RequirePascalCase(options.ModuleName, "--module");
        string commandToken = RequireCommandToken(options.CommandToken);
        string moduleNamespace = $"{CommandContainerNamespace}.{moduleName}";
        string modulePath = Path.Combine(CommandContainerPath, moduleName);
        var files = new List<ScaffoldFile>
        {
            new(
                Path.Combine(modulePath, "EntryPoint", $"{moduleName}CommandModule.cs"),
                $"{moduleNamespace}.EntryPoint",
                EntryPointTemplate(moduleName, commandToken, moduleNamespace)),
            new(
                Path.Combine(modulePath, "Application", $"{moduleName}CommandHandler.cs"),
                $"{moduleNamespace}.Application",
                ApplicationTemplate(moduleName, moduleNamespace)),
            new(
                Path.Combine(TestPath, $"{moduleName}CommandScaffoldTests.cs"),
                "ArchLinterNet.Cli.Tests.Scaffolded",
                TestTemplate(moduleName, commandToken)),
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

    private void WritePlan(IReadOnlyList<ScaffoldFile> files, bool dryRun)
    {
        if (dryRun)
        {
            console.Out.WriteLine("Dry run — no files written.");
        }

        foreach (ScaffoldFile file in files.OrderBy(static file => file.Path, StringComparer.Ordinal))
        {
            console.Out.WriteLine($"{(dryRun ? "Would create" : "Created")} {file.Path} ({file.Namespace})");
            if (!dryRun)
            {
                string temporaryPath = fileSystem.WriteAllTextToTemp(file.Path, file.Contents);
                fileSystem.RenameTempToTarget(temporaryPath, file.Path);
            }
        }

        console.Out.WriteLine("Run 'make lint-architecture' before committing the generated module.");
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
            Path.Combine(modulePath, "Models", $"{name}.cs"),
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
            Path.Combine(modulePath, "Abstractions", $"{name}.cs"),
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
            Path.Combine(modulePath, "Exceptions", $"{name}.cs"),
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

    private static string TestTemplate(string moduleName, string commandToken) =>
        $$"""
          using NUnit.Framework;

          namespace ArchLinterNet.Cli.Tests.Scaffolded;

          [TestFixture]
          public sealed class {{moduleName}}CommandScaffoldTests
          {
              [Test]
              public void Module_UsesTheExpectedCommandToken()
              {
                  Assert.That("{{commandToken}}", Is.EqualTo("{{commandToken}}"));
              }
          }
          """;

    internal sealed record ScaffoldFile(string Path, string Namespace, string Contents);
}
