using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ConditionSetConfigTests
{
    private static readonly string[] Dot = { "." };
    private static readonly string[] ConsoleWriteLine = { "System.Console.WriteLine" };
    private static readonly string[] DebugWriteLine = { "System.Diagnostics.Debug.WriteLine" };
    private static readonly string[] Debug = { "DEBUG" };
    private static readonly string[] UnityEditor = { "UNITY_EDITOR" };
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-test-{Guid.NewGuid():N}");
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

    [Test]
    public void DefaultConditionSet_ResolvesToEmptySymbols_WhenNothingConfigured()
    {
        var analysis = new ArchitectureAnalysisConfiguration();
        Assert.That(analysis.ConditionSets, Has.Count.Zero);
        Assert.That(analysis.DefaultConditionSet, Is.Empty);
    }

    [Test]
    public void PolicyWithConditionSets_LoadsSuccessfully()
    {
        var analysis = new ArchitectureAnalysisConfiguration
        {
            ConditionSets = new Dictionary<string, List<string>>
            {
                ["runtime"] = new(),
                ["editor"] = new() { "UNITY_EDITOR" }
            },
            DefaultConditionSet = "runtime"
        };

        Assert.Multiple(() =>
        {
            Assert.That(analysis.ConditionSets, Contains.Key("runtime"));
            Assert.That(analysis.ConditionSets, Contains.Key("editor"));
            Assert.That(analysis.DefaultConditionSet, Is.EqualTo("runtime"));
            Assert.That(analysis.ConditionSets["editor"], Contains.Item("UNITY_EDITOR"));
        });
    }

    [Test]
    public void RuntimeSymbolSet_ExcludesIfDefConditionalBlocks()
    {
        string sourceFile = Path.Combine(_tempDir, "RuntimeClass.cs");
        File.WriteAllText(sourceFile, @"
namespace TestNamespace;
public class RuntimeClass
{
    public void Run()
    {
#if DEBUG
        System.Console.WriteLine(""debug only"");
#endif
    }
}
");

        var executionContext = new ArchitectureContractExecutionContext(
            "test-runtime", null, Array.Empty<ArchitectureIgnoredViolation>(), false, null, null);
        IReadOnlyList<ArchitectureViolation> violations = new ArchitectureSourceScanner()
            .FindMethodBodyViolations(
                _tempDir, "TestNamespace",
                ConsoleWriteLine,
                executionContext,
                sourceRoots: Dot,
                preprocessorSymbols: Array.Empty<string>())
            .ToList();

        Assert.That(violations, Is.Empty,
            "Runtime mode (no symbols) should exclude #if DEBUG blocks");
    }

    [Test]
    public void DebugSymbolSet_IncludesIfDefDebugBlocks()
    {
        string sourceFile = Path.Combine(_tempDir, "DebugClass.cs");
        File.WriteAllText(sourceFile, @"
namespace TestNamespace;
public class DebugClass
{
    public void Debug()
    {
#if DEBUG
        System.Console.WriteLine(""debug only"");
#endif
    }
}
");

        var executionContext = new ArchitectureContractExecutionContext(
            "test-debug", null, Array.Empty<ArchitectureIgnoredViolation>(), false, null, null);
        IReadOnlyList<ArchitectureViolation> violations = new ArchitectureSourceScanner()
            .FindMethodBodyViolations(
                _tempDir, "TestNamespace",
                ConsoleWriteLine,
                executionContext,
                sourceRoots: Dot,
                preprocessorSymbols: Debug)
            .ToList();

        Assert.That(violations, Is.Not.Empty,
            "With DEBUG defined, #if DEBUG blocks should be included in analysis");
    }

    [Test]
    public void NegationFlips_WhenSymbolDefined_ExcludesIfNotDebugBlock()
    {
        string sourceFile = Path.Combine(_tempDir, "NonDebugClass.cs");
        File.WriteAllText(sourceFile, @"
namespace TestNamespace;
public class NonDebugClass
{
    public void Run()
    {
#if !DEBUG
        System.Console.WriteLine(""not debug"");
#endif
    }
}
");

        var executionContext = new ArchitectureContractExecutionContext(
            "test-no-debug", null, Array.Empty<ArchitectureIgnoredViolation>(), false, null, null);
        IReadOnlyList<ArchitectureViolation> violations = new ArchitectureSourceScanner()
            .FindMethodBodyViolations(
                _tempDir, "TestNamespace",
                ConsoleWriteLine,
                executionContext,
                sourceRoots: Dot,
                preprocessorSymbols: Debug)
            .ToList();

        Assert.That(violations, Is.Empty,
            "When DEBUG is defined, #if !DEBUG blocks should be excluded");
    }

    [Test]
    public void Validator_UnknownDefaultConditionSet_ThrowsInvalidOperation()
    {
        string yaml = """
                      version: 1
                      name: InvalidDefaultTest
                      layers:
                        core:
                          namespace: TestNamespace
                      analysis:
                        target_assemblies:
                          - TestAssembly
                        default_condition_set: non_existent_set
                      contracts: {}
                      """;
        string policyPath = Path.Combine(_tempDir, "invalid-default.arch.yml");
        File.WriteAllText(policyPath, yaml);

        var ex = Assert.Throws<InvalidOperationException>(() => ArchitectureValidator.Validate(policyPath));
        Assert.That(ex!.Message, Does.Contain("non_existent_set"));
    }

    [Test]
    public void ValidatorDefaultConditionSet_ResolvesFromPolicyDefault()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "TestNamespace" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "TestAssembly" },
                ConditionSets = new Dictionary<string, List<string>>
                {
                    ["editor"] = new() { "UNITY_EDITOR" }
                },
                DefaultConditionSet = "editor"
            },
            Contracts = new ArchitectureContractGroups()
        };

        bool resolved = ConditionSetResolver.TryResolve(
            document, null, out IReadOnlyList<string> symbols, out string? error);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.True);
            Assert.That(error, Is.Null);
            Assert.That(symbols, Is.EquivalentTo(UnityEditor));
        });
    }

    [Test]
    public void ValidatorExplicitSymbols_OverrideDefault()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["core"] = new() { Namespace = "TestNamespace" }
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "TestAssembly" },
                ConditionSets = new Dictionary<string, List<string>>
                {
                    ["editor"] = new() { "UNITY_EDITOR" },
                    ["debug"] = new() { "DEBUG" }
                },
                DefaultConditionSet = "editor"
            },
            Contracts = new ArchitectureContractGroups()
        };

        bool resolved = ConditionSetResolver.TryResolve(
            document, "debug", out IReadOnlyList<string> symbols, out string? error);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.True);
            Assert.That(error, Is.Null);
            Assert.That(symbols, Is.EquivalentTo(Debug));
        });
    }

    [Test]
    public void MultipleSymbols_MatchCorrectBranches()
    {
        string sourceFile = Path.Combine(_tempDir, "DebugClass.cs");
        File.WriteAllText(sourceFile, @"
namespace TestNamespace;
public class DebugClass
{
    public void Run()
    {
#if DEBUG
        System.Diagnostics.Debug.WriteLine(""debug only"");
#endif
    }
}
");

        IReadOnlyList<ArchitectureViolation> violationsWithDebug = new ArchitectureSourceScanner()
            .FindMethodBodyViolations(
                _tempDir, "TestNamespace",
                DebugWriteLine,
                new ArchitectureContractExecutionContext(
                    "test-debug", null, Array.Empty<ArchitectureIgnoredViolation>(), false, null, null),
                sourceRoots: Dot,
                preprocessorSymbols: new[] { "UNITY_EDITOR", "DEBUG" })
            .ToList();

        Assert.That(violationsWithDebug, Is.Not.Empty,
            "When DEBUG is defined, #if DEBUG blocks should be visible");

        IReadOnlyList<ArchitectureViolation> violationsWithoutDebug = new ArchitectureSourceScanner()
            .FindMethodBodyViolations(
                _tempDir, "TestNamespace",
                DebugWriteLine,
                new ArchitectureContractExecutionContext(
                    "test-no-debug", null, Array.Empty<ArchitectureIgnoredViolation>(), false, null, null),
                sourceRoots: Dot,
                preprocessorSymbols: UnityEditor)
            .ToList();

        Assert.That(violationsWithoutDebug, Is.Empty,
            "Without DEBUG, #if DEBUG blocks should be excluded");
    }
}
