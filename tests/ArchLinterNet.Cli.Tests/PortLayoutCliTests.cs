using ArchLinterNet.Cli.Commands.Validate;
using ArchLinterNet.Cli.Infrastructure;
using NUnit.Framework;
using PortLayoutCliFixtures;

namespace ArchLinterNet.Cli.Tests;

// CLI-level end-to-end tests for the port_boundary family (strict_port_boundaries/
// audit_port_boundaries), covering the approved-seam, forbidden-direct-reference, and
// anti-corruption-layer scenarios documented in docs/contracts/port-boundary.md and required by
// issue #169's acceptance criteria. Driven through the real production ValidateCommandHandler +
// CliRuntime + FileSystem, mirroring ContextualContractCliTests.cs.
//
// This assembly runs with [assembly: Parallelizable(ParallelScope.All)], so each test creates and
// tears down its own uniquely-named temp directory locally instead of relying on shared
// [SetUp]/[TearDown] instance state.
[TestFixture]
public sealed class PortLayoutCliTests
{
    private const string CatalogPortBoundaryContract = """
          strict_port_boundaries:
            - id: sales-to-catalog-through-port
              name: sales-may-reach-catalog-only-through-port
              source:
                role: ApplicationLayer
                metadata:
                  module: Sales
              target_context:
                metadata:
                  module: Catalog
              allowed_seams:
                - role: Port
                  metadata:
                    module: Catalog
              forbidden:
                - role: DomainLayer
                  metadata:
                    module: Catalog
                - role: Adapter
                  metadata:
                    module: Catalog
              reason: Sales must reach Catalog only through the reviewed Catalog port.
        """;

    private const string LegacyCrmAclContract = """
          strict_port_boundaries:
            - id: legacy-crm-through-acl
              name: legacy-crm-must-use-anti-corruption-layer
              source:
                role: DomainLayer
                metadata:
                  module: LegacyCrm
              target_context:
                metadata:
                  module: LegacyCrm
              allowed_seams:
                - role: AntiCorruptionLayer
                  metadata:
                    module: LegacyCrm
              forbidden:
                - role: Adapter
                  metadata:
                    module: LegacyCrm
              reason: LegacyCrm must reach the legacy database only through the reviewed anti-corruption layer.
        """;

    private static string CreateTempDir()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-cli-port-layout-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private static string WritePolicy(string tempDir, string contractsYaml)
    {
        string policyPath = Path.Combine(tempDir, "dependencies.arch.yml");
        File.WriteAllText(policyPath, $$"""
            version: 1
            name: Port Boundary CLI Test Policy

            classification:
              attributes:
                - attribute: PortLayoutCliFixtures.PortLayoutDomainMarkerAttribute
                  role: DomainLayer
                  metadata:
                    module: constructor[0]
                - attribute: PortLayoutCliFixtures.PortLayoutApplicationMarkerAttribute
                  role: ApplicationLayer
                  metadata:
                    module: constructor[0]
                - attribute: PortLayoutCliFixtures.PortLayoutPortMarkerAttribute
                  role: Port
                  metadata:
                    module: constructor[0]
                - attribute: PortLayoutCliFixtures.PortLayoutAdapterMarkerAttribute
                  role: Adapter
                  metadata:
                    module: constructor[0]
                - attribute: PortLayoutCliFixtures.PortLayoutAclMarkerAttribute
                  role: AntiCorruptionLayer
                  metadata:
                    module: constructor[0]

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
    public void Validate_StrictPortBoundaries_ApprovedCatalogSeamPasses_AndDirectDomainReferenceFails()
    {
        string tempDir = CreateTempDir();
        try
        {
            string policyPath = WritePolicy(tempDir, CatalogPortBoundaryContract);

            (int exitCode, string stdOut, string stdErr) = RunValidate(policyPath, "strict", "human");

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.Not.EqualTo(0), "A direct forbidden Catalog reference must fail the build.");
                Assert.That(stdOut, Does.Contain(nameof(PortLayoutSalesReferencesCatalogDomainDirectly)));
                Assert.That(stdOut, Does.Not.Contain(nameof(PortLayoutSalesUsesCatalogPort)),
                    "A Sales type reaching Catalog only through the approved port must not be reported.");
                Assert.That(stdErr, Is.Empty);
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Validate_StrictPortBoundaries_AclScenario_ApprovedAclPasses_AndDirectDatabaseReferenceFails()
    {
        string tempDir = CreateTempDir();
        try
        {
            string policyPath = WritePolicy(tempDir, LegacyCrmAclContract);

            (int exitCode, string stdOut, _) = RunValidate(policyPath, "strict", "human");

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.Not.EqualTo(0), "A direct database adapter reference must fail the build.");
                Assert.That(stdOut, Does.Contain(nameof(PortLayoutLegacyCrmReferencesDatabaseDirectly)));
                Assert.That(stdOut, Does.Not.Contain(nameof(PortLayoutLegacyCrmUsesAcl)),
                    "A LegacyCrm type reaching the database only through the approved ACL must not be reported.");
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Validate_AuditPortBoundaries_ViolationIsReportedUnderAuditButNotStrict()
    {
        string tempDir = CreateTempDir();
        try
        {
            string policyPath = WritePolicy(tempDir, CatalogPortBoundaryContract.Replace(
                "strict_port_boundaries:", "audit_port_boundaries:"));

            (int auditExitCode, string auditStdOut, _) = RunValidate(policyPath, "audit", "human");
            (int strictExitCode, string strictStdOut, _) = RunValidate(policyPath, "strict", "human");

            Assert.Multiple(() =>
            {
                Assert.That(auditExitCode, Is.EqualTo(1), "Audit findings are still reported as exit code 1; CI opts out via continue-on-error.");
                Assert.That(auditStdOut, Does.Contain(nameof(PortLayoutSalesReferencesCatalogDomainDirectly)));

                Assert.That(strictExitCode, Is.EqualTo(0));
                Assert.That(strictStdOut, Does.Not.Contain(nameof(PortLayoutSalesReferencesCatalogDomainDirectly)));
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Validate_JsonFormat_PortBoundaryViolation_IncludesEvidenceFields()
    {
        string tempDir = CreateTempDir();
        try
        {
            string policyPath = WritePolicy(tempDir, CatalogPortBoundaryContract);

            (_, string stdOut, _) = RunValidate(policyPath, "strict", "json");

            Assert.Multiple(() =>
            {
                Assert.That(stdOut, Does.Contain(nameof(PortLayoutSalesReferencesCatalogDomainDirectly)));
                Assert.That(stdOut, Does.Contain("\"source_role\""));
                Assert.That(stdOut, Does.Contain("\"target_role\""));
                Assert.That(stdOut, Does.Contain("\"expected_seam\""));
                Assert.That(stdOut, Does.Contain("DomainLayer"));
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
