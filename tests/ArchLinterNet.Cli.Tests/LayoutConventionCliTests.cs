using ArchLinterNet.Cli.Commands.Validate;
using ArchLinterNet.Cli.Infrastructure;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

// CLI-level end-to-end tests for the layout_conventions family (strict_layout_conventions),
// covering the Services/Interfaces matching-counterpart scenario and a CEL files_matching.when
// predicate narrowing which declared types get checked, per issue #169's acceptance criteria.
// Driven through the real production ValidateCommandHandler + CliRuntime + FileSystem, mirroring
// ContextualContractCliTests.cs and PortLayoutCliTests.cs.
//
// Layout convention selectors require real on-disk source-file facts (folder segment, file name),
// so each test writes matching .cs files under the temp policy directory and sets
// analysis.source_roots: ["."] - the repository root resolves to the policy file's own directory
// (ArchitectureRepositoryRootResolver.ResolveFrom), exactly as LayoutConventionContractTests.cs
// does at the Core level via ArchitectureAnalysisContext directly.
//
// This assembly runs with [assembly: Parallelizable(ParallelScope.All)], so each test creates and
// tears down its own uniquely-named temp directory locally instead of relying on shared
// [SetUp]/[TearDown] instance state.
[TestFixture]
public sealed class LayoutConventionCliTests
{
    private static string CreateTempDir()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-cli-layout-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private static void WriteSourceFile(string tempDir, string relativePath, string content)
    {
        string fullPath = Path.Combine(tempDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private static string WritePolicy(string tempDir, string contractsYaml)
    {
        string policyPath = Path.Combine(tempDir, "dependencies.arch.yml");
        File.WriteAllText(policyPath, $$"""
            version: 1
            name: Layout Convention CLI Test Policy

            layers: {}

            analysis:
              target_assemblies:
                - ArchLinterNet.Cli.Tests
              source_roots: ["."]

            contracts:
            {{contractsYaml}}
            """);
        return policyPath;
    }

    private static (int ExitCode, string StdOut, string StdErr) RunValidate(string policyPath, string mode, string format)
    {
        var runtime = new CliRuntime();
        var console = new RecordingConsole();
        var handler = new ValidateCommandHandler(runtime, console);

        int exitCode = handler.Execute(new ValidateCommandOptions(
            policyPath, mode, format, Array.Empty<string>(), null,
            TimingsEnabled: false, BaselinePath: null, ShowHelp: false, ShowVersion: false));

        return (exitCode, console.OutputText, console.ErrorText);
    }

    [Test]
    public void Validate_StrictLayoutConventions_MissingInterfaceCounterpartFails_MatchedPairPasses()
    {
        string tempDir = CreateTempDir();
        try
        {
            WriteSourceFile(tempDir, "Services/PortLayoutOrderService.cs",
                "namespace PortLayoutCliFixtures.Services { public sealed class PortLayoutOrderService { } }");
            WriteSourceFile(tempDir, "Services/PortLayoutPaymentService.cs",
                "namespace PortLayoutCliFixtures.Services { public sealed class PortLayoutPaymentService { } }");
            WriteSourceFile(tempDir, "Interfaces/IPortLayoutOrderService.cs",
                "namespace PortLayoutCliFixtures.Interfaces { public interface IPortLayoutOrderService { } }");

            string policyPath = WritePolicy(tempDir, """
                  strict_layout_conventions:
                    - id: application-services-have-matching-interfaces
                      name: application-services-folder-requires-concrete-services-with-interfaces
                      files_matching:
                        folder_segment: Services
                      require_type_kind: class
                      required_name_suffix: Service
                      require_matching_interface:
                        name_prefix: I
                      reason: Every Application service class needs a matching interface.
                """);

            (int exitCode, string stdOut, string stdErr) = RunValidate(policyPath, "strict", "human");

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.Not.EqualTo(0), "A service class with no matching interface must fail the build.");
                Assert.That(stdOut, Does.Contain("PortLayoutPaymentService"));
                Assert.That(stdOut, Does.Contain("IPortLayoutPaymentService"),
                    "The diagnostic must name the expected counterpart interface.");
                Assert.That(stdOut, Does.Not.Contain("PortLayoutOrderService"),
                    "The service with a matching interface must not be reported as a violation.");
                Assert.That(stdErr, Is.Empty);
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Validate_StrictLayoutConventions_WhenPredicateNarrowsCheckedTypes()
    {
        string tempDir = CreateTempDir();
        try
        {
            WriteSourceFile(tempDir, "Handlers/PortLayoutWhenNarrowedTarget.cs",
                "namespace PortLayoutCliFixtures.Handlers { public sealed class PortLayoutWhenNarrowedTarget { } }");
            WriteSourceFile(tempDir, "Handlers/PortLayoutWhenIgnoredSibling.cs",
                "namespace PortLayoutCliFixtures.Handlers { public sealed class PortLayoutWhenIgnoredSibling { } }");

            string policyPath = WritePolicy(tempDir, """
                  strict_layout_conventions:
                    - id: handlers-forbid-when-narrowed-class
                      name: handlers-when-narrowed-class-is-forbidden
                      files_matching:
                        folder_segment: Handlers
                        when: subject.simpleName == "PortLayoutWhenNarrowedTarget"
                      forbid_type_kind: class
                      reason: Demonstrates a CEL when predicate narrowing which declared types are checked.
                """);

            (int exitCode, string stdOut, _) = RunValidate(policyPath, "strict", "human");

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.Not.EqualTo(0));
                Assert.That(stdOut, Does.Contain("PortLayoutWhenNarrowedTarget"));
                Assert.That(stdOut, Does.Not.Contain("PortLayoutWhenIgnoredSibling"),
                    "The when predicate must exclude the sibling type from every expectation check.");
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Validate_JsonFormat_LayoutConventionViolation_IncludesEvidenceFields()
    {
        string tempDir = CreateTempDir();
        try
        {
            WriteSourceFile(tempDir, "Handlers/PortLayoutWhenNarrowedTarget.cs",
                "namespace PortLayoutCliFixtures.Handlers { public sealed class PortLayoutWhenNarrowedTarget { } }");
            WriteSourceFile(tempDir, "Handlers/PortLayoutWhenIgnoredSibling.cs",
                "namespace PortLayoutCliFixtures.Handlers { public sealed class PortLayoutWhenIgnoredSibling { } }");

            string policyPath = WritePolicy(tempDir, """
                  strict_layout_conventions:
                    - id: handlers-forbid-when-narrowed-class-json
                      name: handlers-when-narrowed-class-is-forbidden-json
                      files_matching:
                        folder_segment: Handlers
                        when: subject.simpleName == "PortLayoutWhenNarrowedTarget"
                      forbid_type_kind: class
                      reason: JSON evidence check for a when-narrowed layout violation.
                """);

            (_, string stdOut, _) = RunValidate(policyPath, "strict", "json");

            Assert.Multiple(() =>
            {
                Assert.That(stdOut, Does.Contain("PortLayoutWhenNarrowedTarget"));
                Assert.That(stdOut, Does.Not.Contain("PortLayoutWhenIgnoredSibling"));
                Assert.That(stdOut, Does.Contain("\"matched_file_path\""));
                Assert.That(stdOut, Does.Contain("\"actual_type_kind\""));
                Assert.That(stdOut, Does.Contain("handlers-forbid-when-narrowed-class-json"));
                Assert.That(stdOut, Does.Contain("\"when_expressions\""));
                Assert.That(stdOut, Does.Contain("subject.simpleName == \\u0022PortLayoutWhenNarrowedTarget\\u0022"));
                Assert.That(stdOut, Does.Contain("\"result\":\"matched\""));
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    private sealed class RecordingConsole : ArchLinterNet.Cli.Abstractions.ICliConsole
    {
        private readonly StringWriter _out = new();
        private readonly StringWriter _error = new();

        public TextWriter Out => _out;

        public TextWriter Error => _error;

        public string OutputText => _out.ToString();

        public string ErrorText => _error.ToString();
    }
}
