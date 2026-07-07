using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class PublicApiSurfaceContractTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-public-api-surface-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private string WritePolicy(string yaml)
    {
        string path = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(path, yaml);
        return path;
    }

    private static string AssemblyName => typeof(PublicApiSurfaceContractTests).Assembly.GetName().Name!;

    private static ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(PublicApiSurfaceContractTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static ArchitectureContractDocument CreateDocument(
        ArchitecturePublicApiSurfaceContract contract,
        bool audit = false)
    {
        var groups = new ArchitectureContractGroups();
        if (audit)
        {
            groups.AuditPublicApiSurface = new List<ArchitecturePublicApiSurfaceContract> { contract };
        }
        else
        {
            groups.StrictPublicApiSurface = new List<ArchitecturePublicApiSurfaceContract> { contract };
        }

        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { AssemblyName }
            },
            Contracts = groups
        };
    }

    private const string CleanDeclaredTypeName = "PublicApiSurfaceContractTestFixtures.CleanDeclaredType";

    [Test]
    public void CheckPublicApiSurfaceContract_FullyDeclaredType_ProducesNoViolationsForItsOwnMembers()
    {
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "clean-declared-api",
            Assemblies = new List<string> { AssemblyName },
            DeclaredApi = new List<string>
            {
                $"class {CleanDeclaredTypeName}",
                $"ctor {CleanDeclaredTypeName}()",
                $"property {CleanDeclaredTypeName}.Value: System.Int32",
                $"method {CleanDeclaredTypeName}.DoWork(): System.Void"
            }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckPublicApiSurfaceContract(contract);

        Assert.That(violations.Any(v => v.SourceType == CleanDeclaredTypeName), Is.False);
    }

    [Test]
    public void CheckPublicApiSurfaceContract_AccidentalPublicType_ReturnsViolation()
    {
        const string TypeName = "PublicApiSurfaceContractTestFixtures.AccidentalPublicType";
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "no-accidental-types",
            Assemblies = new List<string> { AssemblyName }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckPublicApiSurfaceContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == TypeName && v.UndeclaredApiSignature == $"class {TypeName}"), Is.True);
    }

    [Test]
    public void CheckPublicApiSurfaceContract_AccidentalMembers_ReturnsViolationsForEachMemberKind()
    {
        const string TypeName = "PublicApiSurfaceContractTestFixtures.AccidentalMemberType";
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "no-accidental-members",
            Assemblies = new List<string> { AssemblyName },
            DeclaredApi = new List<string>
            {
                $"class {TypeName}",
                $"ctor {TypeName}()"
            }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckPublicApiSurfaceContract(contract);
        HashSet<string> signatures = violations
            .Where(v => v.SourceType == TypeName)
            .Select(v => v.UndeclaredApiSignature!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.That(signatures, Contains.Item($"field {TypeName}.UndeclaredField: System.Int32"));
        Assert.That(signatures, Contains.Item($"property {TypeName}.UndeclaredProperty: System.Int32"));
        Assert.That(signatures, Contains.Item($"method {TypeName}.UndeclaredMethod(): System.Void"));
        Assert.That(signatures, Contains.Item($"event {TypeName}.UndeclaredEvent: System.EventHandler"));
    }

    [Test]
    public void CheckPublicApiSurfaceContract_ProtectedMember_IsTreatedAsExported()
    {
        const string TypeName = "PublicApiSurfaceContractTestFixtures.ProtectedMemberHolder";
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "protected-is-exported",
            Assemblies = new List<string> { AssemblyName },
            DeclaredApi = new List<string>
            {
                $"class {TypeName}",
                $"ctor {TypeName}()"
            }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckPublicApiSurfaceContract(contract);
        HashSet<string> signatures = violations
            .Where(v => v.SourceType == TypeName)
            .Select(v => v.UndeclaredApiSignature!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.That(signatures, Contains.Item($"field {TypeName}.ProtectedField: System.Int32"));
        Assert.That(signatures, Contains.Item($"method {TypeName}.ProtectedMethod(): System.Void"));
    }

    [Test]
    public void CheckPublicApiSurfaceContract_NestedTypeInsideExportedParent_IsInScope()
    {
        const string ContainerName = "PublicApiSurfaceContractTestFixtures.NestedContainerPublic";
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "nested-in-exported-parent",
            Assemblies = new List<string> { AssemblyName }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckPublicApiSurfaceContract(contract);
        HashSet<string> signatures = violations.Select(v => v.UndeclaredApiSignature!).ToHashSet(StringComparer.Ordinal);

        Assert.That(signatures, Contains.Item($"class {ContainerName}+NestedPublicType"));
        Assert.That(signatures, Contains.Item($"class {ContainerName}+NestedProtectedType"));
    }

    [Test]
    public void CheckPublicApiSurfaceContract_NestedTypeInsideInternalParent_IsOutOfScope()
    {
        const string NestedTypeName =
            "PublicApiSurfaceContractTestFixtures.NestedContainerInternal+NestedPublicInsideInternal";
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "nested-in-internal-parent",
            Assemblies = new List<string> { AssemblyName }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckPublicApiSurfaceContract(contract);

        Assert.That(violations.Any(v => v.SourceType == NestedTypeName), Is.False);
    }

    [Test]
    public void CheckPublicApiSurfaceContract_GenericTypeAndMethod_NormalizeSignaturesPositionally()
    {
        const string TypeName = "PublicApiSurfaceContractTestFixtures.GenericHolder`1";
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "generic-surface",
            Assemblies = new List<string> { AssemblyName }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckPublicApiSurfaceContract(contract);
        HashSet<string> signatures = violations
            .Where(v => v.SourceType == TypeName)
            .Select(v => v.UndeclaredApiSignature!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.That(signatures, Contains.Item($"class {TypeName}"));
        Assert.That(signatures, Contains.Item($"field {TypeName}.Value: !0"));
        Assert.That(signatures, Contains.Item($"method {TypeName}.Map`1(!0): !!0"));
    }

    [Test]
    public void CheckPublicApiSurfaceContract_ArrayRank_ProducesDistinctSignaturesPerRank()
    {
        const string TypeName = "PublicApiSurfaceContractTestFixtures.ArrayRankHolder";
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "array-rank-surface",
            Assemblies = new List<string> { AssemblyName },
            DeclaredApi = new List<string>
            {
                $"class {TypeName}",
                $"ctor {TypeName}()"
            }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckPublicApiSurfaceContract(contract);
        HashSet<string> signatures = violations
            .Where(v => v.SourceType == TypeName)
            .Select(v => v.UndeclaredApiSignature!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.That(signatures, Contains.Item($"method {TypeName}.TakeVector(System.Int32[]): System.Void"));
        Assert.That(signatures, Contains.Item($"method {TypeName}.TakeMatrix(System.Int32[,]): System.Void"));
        Assert.That(signatures, Contains.Item($"method {TypeName}.TakeCube(System.Int32[,,]): System.Void"));
        Assert.That(signatures.Count, Is.EqualTo(3));
    }

    [Test]
    public void CheckPublicApiSurfaceContract_PublicEnum_DoesNotReportBackingFieldButTracksLiterals()
    {
        const string TypeName = "PublicApiSurfaceContractTestFixtures.PublicColor";
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "enum-surface",
            Assemblies = new List<string> { AssemblyName },
            DeclaredApi = new List<string> { $"enum {TypeName}" }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckPublicApiSurfaceContract(contract);
        HashSet<string> signatures = violations
            .Where(v => v.SourceType == TypeName)
            .Select(v => v.UndeclaredApiSignature!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.That(signatures.Any(s => s.Contains("value__", StringComparison.Ordinal)), Is.False);
        Assert.That(signatures, Contains.Item($"const {TypeName}.Red: {TypeName}"));
        Assert.That(signatures, Contains.Item($"const {TypeName}.Green: {TypeName}"));
        Assert.That(signatures, Contains.Item($"const {TypeName}.Blue: {TypeName}"));
    }

    [Test]
    public void CheckPublicApiSurfaceContract_UndeclaredMembers_CarryAssemblyAndVisibility()
    {
        const string TypeName = "PublicApiSurfaceContractTestFixtures.VisibilityHolder";
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "visibility-surface",
            Assemblies = new List<string> { AssemblyName },
            DeclaredApi = new List<string>
            {
                $"class {TypeName}",
                $"ctor {TypeName}()"
            }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckPublicApiSurfaceContract(contract);

        var publicMethodViolation = violations.Single(v =>
            v.UndeclaredApiSignature == $"method {TypeName}.PublicMethod(): System.Void");
        Assert.That(publicMethodViolation.ApiVisibility, Is.EqualTo("public"));
        Assert.That(publicMethodViolation.ApiAssemblyName, Is.EqualTo(AssemblyName));

        var protectedMethodViolation = violations.Single(v =>
            v.UndeclaredApiSignature == $"method {TypeName}.ProtectedMethod(): System.Void");
        Assert.That(protectedMethodViolation.ApiVisibility, Is.EqualTo("protected"));
        Assert.That(protectedMethodViolation.ApiAssemblyName, Is.EqualTo(AssemblyName));
    }

    [Test]
    public void CheckPublicApiSurfaceContract_UndeclaredAndForbiddenConstant_ReportsForbiddenReason()
    {
        const string TypeName = "PublicApiSurfaceContractTestFixtures.ConstantHolder";
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "undeclared-and-forbidden-constant",
            Assemblies = new List<string> { AssemblyName },
            ForbidPublicConstantsUnlessDeclared = true,
            DeclaredApi = new List<string>
            {
                $"class {TypeName}",
                $"ctor {TypeName}()",
                $"const {TypeName}.DeclaredConst: System.String"
            }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckPublicApiSurfaceContract(contract);

        var violation = violations.Single(v =>
            v.UndeclaredApiSignature == $"const {TypeName}.UndeclaredConst: System.String");
        Assert.That(violation.ForbiddenPublicConstant, Is.True);
    }

    [Test]
    public void CheckPublicApiSurfaceContract_UndeclaredConstant_ReturnsViolationByDefault()
    {
        const string TypeName = "PublicApiSurfaceContractTestFixtures.ConstantHolder";
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "constants-default",
            Assemblies = new List<string> { AssemblyName },
            DeclaredApi = new List<string>
            {
                $"class {TypeName}",
                $"ctor {TypeName}()",
                $"const {TypeName}.DeclaredConst: System.String"
            }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckPublicApiSurfaceContract(contract);

        Assert.That(violations.Any(v =>
            v.UndeclaredApiSignature == $"const {TypeName}.UndeclaredConst: System.String"), Is.True);
        Assert.That(violations.Any(v =>
            v.UndeclaredApiSignature == $"const {TypeName}.DeclaredConst: System.String"), Is.False);
    }

    [Test]
    public void CheckPublicApiSurfaceContract_ForbidConstantsUnlessDeclared_StillForbidsDeclaredConstant()
    {
        const string TypeName = "PublicApiSurfaceContractTestFixtures.ConstantHolder";
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "constants-forbidden",
            Assemblies = new List<string> { AssemblyName },
            ForbidPublicConstantsUnlessDeclared = true,
            DeclaredApi = new List<string>
            {
                $"class {TypeName}",
                $"ctor {TypeName}()",
                $"const {TypeName}.DeclaredConst: System.String",
                $"const {TypeName}.UndeclaredConst: System.String"
            }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckPublicApiSurfaceContract(contract);

        var violation = violations.Single(v =>
            v.UndeclaredApiSignature == $"const {TypeName}.DeclaredConst: System.String");
        Assert.That(violation.ForbiddenPublicConstant, Is.True);
    }

    [Test]
    public void CheckPublicApiSurfaceContract_AllowedPublicConstant_IsExempt()
    {
        const string TypeName = "PublicApiSurfaceContractTestFixtures.ConstantHolder";
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "constants-allowed",
            Assemblies = new List<string> { AssemblyName },
            ForbidPublicConstantsUnlessDeclared = true,
            DeclaredApi = new List<string>
            {
                $"class {TypeName}",
                $"ctor {TypeName}()",
                $"const {TypeName}.DeclaredConst: System.String",
                $"const {TypeName}.UndeclaredConst: System.String"
            },
            AllowedPublicConstants = new List<string> { $"{TypeName}.DeclaredConst" }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckPublicApiSurfaceContract(contract);

        Assert.That(violations.Any(v =>
            v.UndeclaredApiSignature == $"const {TypeName}.DeclaredConst: System.String"), Is.False);
    }

    [Test]
    public void CheckPublicApiSurfaceContract_AuditMode_ReportsViolationWithoutFailingStrict()
    {
        var auditContract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "audit-public-api",
            Assemblies = new List<string> { AssemblyName }
        };
        var document = CreateDocument(auditContract, audit: true);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckPublicApiSurfaceContract(auditContract);

        Assert.That(violations, Is.Not.Empty);
        Assert.That(document.Contracts.StrictPublicApiSurface, Is.Empty);
    }

    [Test]
    public void CheckPublicApiSurfaceContract_IgnoredViolation_SuppressesViolation()
    {
        const string TypeName = "PublicApiSurfaceContractTestFixtures.AccidentalPublicType";
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "ignored-accidental-type",
            Assemblies = new List<string> { AssemblyName }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        contract.IgnoredViolations.Add(new ArchitectureIgnoredViolation
        {
            SourceType = TypeName,
            ForbiddenReference = $"class {TypeName}",
            Reason = "test ignore"
        });

        var violations = runner.Session.CheckPublicApiSurfaceContract(contract);

        Assert.That(violations.Any(v => v.UndeclaredApiSignature == $"class {TypeName}"), Is.False);
    }

    [Test]
    public void CheckPublicApiSurfaceContract_UnmatchedIgnoredViolation_IsTracked()
    {
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "unmatched-ignore",
            Id = "unmatched-ignore",
            Assemblies = new List<string> { AssemblyName },
            IgnoredViolations = new List<ArchitectureIgnoredViolation>
            {
                new()
                {
                    SourceType = "Nonexistent.Type",
                    ForbiddenReference = "class Nonexistent.Type",
                    Reason = "stale ignore"
                }
            }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        runner.Session.CheckPublicApiSurfaceContract(contract);

        Assert.That(runner.UnmatchedIgnoredViolations.Any(u => u.SourceType == "Nonexistent.Type"), Is.True);
    }

    [Test]
    public void PublicApiSurface_EmptyAssemblies_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            contracts:
              strict_public_api_surface:
                - name: no-assemblies
                  reason: Missing assemblies list.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("no 'assemblies'"));
        Assert.That(ex.Message, Does.Contain("no-assemblies"));
    }

    [Test]
    public void PublicApiSurface_AssemblyNotDeclaredInTargetAssemblies_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            contracts:
              strict_public_api_surface:
                - name: typoed-assembly-name
                  assemblies: [ArchLinterNet.Core.Typo]
                  reason: Assembly name not present in analysis.target_assemblies.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("ArchLinterNet.Core.Typo"));
        Assert.That(ex.Message, Does.Contain("not declared in 'analysis.target_assemblies'"));
    }

    [Test]
    public void ValidateStrict_PublicApiSurfaceViolation_EndToEndThroughValidationService()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test

            analysis:
              target_assemblies: [{AssemblyName}]

            contracts:
              strict_public_api_surface:
                - id: no-accidental-types
                  name: no-accidental-types
                  assemblies: [{AssemblyName}]
                  declared_api: []
                  reason: Accidental public type must be reported end-to-end.
            """);

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        Assert.That(outcome.Passed, Is.False);
        Assert.That(outcome.Violations.Any(v =>
            v.SourceType == "PublicApiSurfaceContractTestFixtures.AccidentalPublicType"), Is.True);
    }
}
