using ArchLinterNet.Cli.Commands.Validate;
using ArchLinterNet.Cli.Infrastructure;
using ContextualContractCliFixtures;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

// CLI-level end-to-end tests for the context_dependencies/context_allow_only families (tasks.md
// 7.4), driven through the real production ValidateCommandHandler + CliRuntime + FileSystem
// (rather than the fakes used by CliArchitectureTests/CliHandlerCoverageTests, which only exercise
// command dispatch/composition). Both classes are `internal` in ArchLinterNet.Cli but visible here
// via [InternalsVisibleTo("ArchLinterNet.Cli.Tests")] in ArchLinterNet.Cli.csproj.
//
// The policy YAML targets "ArchLinterNet.Cli.Tests" itself as target_assemblies: the running test
// process's own entry assembly is already loaded, so the real ArchitectureAssemblyLoader resolves it
// by name without needing a build/copy step, and ContextualContractCliFixtures.cs's marker-attributed
// types are classified and scanned exactly as they would be for a real consuming project.
//
// This assembly runs with [assembly: Parallelizable(ParallelScope.All)], so each test creates and
// tears down its own uniquely-named temp directory locally instead of relying on shared
// [SetUp]/[TearDown] instance state, which would race across concurrently-running test methods.
[TestFixture]
public sealed class ContextualContractCliTests
{
    private static string CreateTempDir()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-cli-context-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private static string WritePolicy(string tempDir, string contractsYaml)
    {
        string policyPath = Path.Combine(tempDir, "dependencies.arch.yml");
        File.WriteAllText(policyPath, $$"""
            version: 1
            name: Contextual CLI Test Policy

            classification:
              attributes:
                - attribute: ContextualContractCliFixtures.CliContextDomainMarkerAttribute
                  role: DomainLayer
                  metadata:
                    domain: constructor[0]

            layers: {}

            analysis:
              target_assemblies:
                - ArchLinterNet.Cli.Tests

            contracts:
            {{contractsYaml}}
            """);
        return policyPath;
    }

    private static (int ExitCode, string StdOut, string StdErr) RunValidate(string policyPath, string mode, string format)
    {
        var runtime = new CliRuntime();
        var console = new RecordingConsole();
        var fileSystem = new FileSystem();
        var handler = new ValidateCommandHandler(runtime, console, fileSystem);

        int exitCode = handler.Execute(new ValidateCommandOptions(
            policyPath, mode, format, Array.Empty<string>(), null,
            TimingsEnabled: false, BaselinePath: null, ShowHelp: false, ShowVersion: false));

        return (exitCode, console.OutputText, console.ErrorText);
    }

    [Test]
    public void Validate_StrictContextDependencies_CrossDomainReference_FailsWithNonZeroExitCode()
    {
        string tempDir = CreateTempDir();
        try
        {
            string policyPath = WritePolicy(tempDir, """
                  strict_context_dependencies:
                    - id: sales-no-inventory
                      name: sales-must-not-depend-on-inventory
                      source:
                        role: DomainLayer
                        metadata:
                          domain: Sales
                      forbidden:
                        - role: DomainLayer
                          metadata:
                            domain: Inventory
                      reason: Bounded contexts must not depend on each other.
                """);

            (int exitCode, string stdOut, string stdErr) = RunValidate(policyPath, "strict", "human");

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.Not.EqualTo(0), "A strict_context_dependencies violation must fail the build.");
                Assert.That(stdOut, Does.Contain("CliSalesCheckout"));
                Assert.That(stdErr, Is.Empty);
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Validate_AuditContextDependencies_CrossDomainReference_IsReportedUnderAuditMode()
    {
        // Per docs/usage/exit-codes.md, `--mode audit` still exits 1 when audit findings exist -
        // "expected for failing audit checks if run manually"; the strict/audit distinction is that
        // CI treats an audit run as non-blocking (e.g. `continue-on-error: true`), not that the
        // process itself reports success. What this test actually verifies is that an
        // audit_context_dependencies contract is picked up and reported under `--mode audit` at all
        // (a contract declared only under the audit group must not run, or fail to run, under strict).
        string tempDir = CreateTempDir();
        try
        {
            string policyPath = WritePolicy(tempDir, """
                  audit_context_dependencies:
                    - id: sales-no-inventory-audit
                      name: audit-sales-inventory-coupling
                      source:
                        role: DomainLayer
                        metadata:
                          domain: Sales
                      forbidden:
                        - role: DomainLayer
                          metadata:
                            domain: Inventory
                      reason: Discover Sales/Inventory coupling.
                """);

            (int auditExitCode, string auditStdOut, _) = RunValidate(policyPath, "audit", "human");
            (int strictExitCode, string strictStdOut, _) = RunValidate(policyPath, "strict", "human");

            Assert.Multiple(() =>
            {
                Assert.That(auditExitCode, Is.EqualTo(1), "Audit findings are still reported as exit code 1; CI opts out via continue-on-error.");
                Assert.That(auditStdOut, Does.Contain("CliSalesCheckout"));

                // The contract is declared only under audit_context_dependencies, so a strict-mode
                // run must not pick it up at all.
                Assert.That(strictExitCode, Is.EqualTo(0));
                Assert.That(strictStdOut, Does.Not.Contain("CliSalesCheckout"));
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Validate_StrictContextAllowOnly_ReferenceOutsideAllowedSelectors_FailsWithEvidence()
    {
        string tempDir = CreateTempDir();
        try
        {
            string policyPath = WritePolicy(tempDir, """
                  strict_context_allow_only:
                    - id: sales-allow-only
                      name: sales-may-depend-only-on-own-context
                      source:
                        role: DomainLayer
                        metadata:
                          domain: Sales
                      allowed:
                        - role: DomainLayer
                          metadata:
                            domain: Sales
                      reason: Sales may depend only on its own context.
                """);

            (int exitCode, string stdOut, _) = RunValidate(policyPath, "strict", "json");

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.Not.EqualTo(0));
                Assert.That(stdOut, Does.Contain("CliSalesCheckout"));
                Assert.That(stdOut, Does.Contain("\"source_role\""));
                Assert.That(stdOut, Does.Contain("\"target_role\""));
                Assert.That(stdOut, Does.Contain("DomainLayer"));
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Validate_JsonFormat_ContextDependencyViolation_IncludesSelectorEvidence()
    {
        string tempDir = CreateTempDir();
        try
        {
            string policyPath = WritePolicy(tempDir, """
                  strict_context_dependencies:
                    - id: sales-no-inventory-json
                      name: sales-must-not-depend-on-inventory-json
                      source:
                        role: DomainLayer
                        metadata:
                          domain: Sales
                      forbidden:
                        - role: DomainLayer
                          metadata:
                            domain: Inventory
                      reason: JSON evidence check.
                """);

            (_, string stdOut, _) = RunValidate(policyPath, "strict", "json");

            Assert.Multiple(() =>
            {
                Assert.That(stdOut, Does.Contain("\"matched_selector\""));
                Assert.That(stdOut, Does.Contain("\"target_metadata\""));
                Assert.That(stdOut, Does.Contain("Inventory"));
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
