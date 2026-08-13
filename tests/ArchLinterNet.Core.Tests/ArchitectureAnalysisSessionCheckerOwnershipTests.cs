using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Self-policy for the session/checker ownership boundary established by issue #452.
///
/// <para>
/// The rule these tests enforce is: adding a new contract family must not require putting
/// family-specific checking back into <c>ArchitectureAnalysisSession</c>. The session owns
/// lifecycle (contract selection, rule-input-coverage deferral, execution-context creation,
/// unmatched-ignore collection and baseline-candidate publication); the family's own scanning and
/// finding construction live in <c>ArchLinterNet.Core.Execution.Checkers</c> and reach session
/// facts only through <c>ArchitectureCheckerContext</c>.
/// </para>
///
/// <para>
/// These are source-level tests on purpose: the boundary is about where code is written, which
/// compiled metadata cannot express (a fat session method and a thin one have the same signature).
/// </para>
/// </summary>
[TestFixture]
public sealed class ArchitectureAnalysisSessionCheckerOwnershipTests
{
    // A lifecycle wrapper is: the selection/deferral guard, the execution-context creation, the
    // single delegation, the unmatched-ignore collection, and the return. The bound is deliberately
    // loose enough for the few families that also publish baseline candidates or append a
    // separately-ordered finding set, and far too tight for any real family algorithm.
    private const int MaxLifecycleStatements = 10;

    // Coverage is explicitly out of scope for the #452 extraction: it is not contract-family
    // checking over declared types but a policy-inventory report over the whole document, and the
    // issue keeps coverage/policy-consistency behavior where it is. Extracting it is separate work
    // with its own semantics, not an omission hidden behind this allowlist.
    private static readonly HashSet<string> _sessionOwnedEntryPoints = new(StringComparer.Ordinal)
    {
        "CheckCoverageContract",
    };

    [Test]
    public void EveryContractFamilyEntryPoint_IsAThinLifecycleWrapperOverACheckerComponent()
    {
        List<MethodDeclarationSyntax> entryPoints = ContractFamilyEntryPoints().ToList();

        // Guards the test itself: a rename or a moved partial must not silently reduce this to
        // asserting nothing.
        Assert.That(entryPoints, Has.Count.GreaterThan(20),
            "Expected to find the session's contract-family entry points; the discovery pattern is probably stale.");

        foreach (MethodDeclarationSyntax method in entryPoints)
        {
            string name = method.Identifier.ValueText;
            if (_sessionOwnedEntryPoints.Contains(name))
            {
                continue;
            }

            IReadOnlyList<StatementSyntax> statements = BodyStatements(method);

            Assert.That(statements, Has.Count.LessThanOrEqualTo(MaxLifecycleStatements),
                $"{name} has {statements.Count} statements. Contract-family checking belongs in a "
                + "checker under ArchLinterNet.Core.Execution.Checkers, not in ArchitectureAnalysisSession; "
                + "the session entry point should only do selection, execution-context lifecycle and delegation.");

            Assert.That(DelegatesToACheckerComponent(method), Is.True,
                $"{name} does not delegate to a *Checker component. Add the family's checking to a checker "
                + "under ArchLinterNet.Core.Execution.Checkers and call it from here.");
        }
    }

    [Test]
    public void CheckerComponents_ReachSessionStateOnlyThroughTheCheckerContext()
    {
        List<string> offenders = new();

        foreach (string file in CheckerSourceFiles())
        {
            string source = File.ReadAllText(file);
            if (source.Contains("ArchitectureAnalysisSession", StringComparison.Ordinal))
            {
                offenders.Add(Path.GetFileName(file));
            }
        }

        Assert.That(offenders, Is.Empty,
            "Checkers must depend on ArchitectureCheckerContext, never on ArchitectureAnalysisSession directly — "
            + "otherwise the narrow fact/index access port is bypassed and family code regains the whole session. "
            + $"Offending files: {string.Join(", ", offenders)}");
    }

    [Test]
    public void FindingIdentityAttribution_IsOwnedByADedicatedComponent()
    {
        string attributor = Path.Combine(ExecutionDirectory(), "ArchitectureFindingIdentityAttributor.cs");
        Assert.That(File.Exists(attributor), Is.True,
            "Canonical finding-identity attribution must stay in its own component, testable without a session.");

        // The session partial keeps the candidate log and the cursor; the algorithm must not migrate
        // back into it.
        string sessionPartial = File.ReadAllText(
            Path.Combine(ExecutionDirectory(), "ArchitectureAnalysisSession.FindingIdentities.cs"));
        Assert.That(sessionPartial, Does.Contain("ArchitectureFindingIdentityAttributor.Attach"));
        Assert.That(BodyStatementCountOf(sessionPartial, "AttachFindingIdentities"), Is.LessThanOrEqualTo(2),
            "AttachFindingIdentities must stay a delegation to ArchitectureFindingIdentityAttributor.");
    }

    private static IEnumerable<MethodDeclarationSyntax> ContractFamilyEntryPoints()
    {
        foreach (string file in Directory.EnumerateFiles(ExecutionDirectory(), "ArchitectureAnalysisSession*.cs"))
        {
            CompilationUnitSyntax root = ParseFile(file);

            foreach (MethodDeclarationSyntax method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                string name = method.Identifier.ValueText;
                if (name.StartsWith("Check", StringComparison.Ordinal)
                    && name.EndsWith("Contract", StringComparison.Ordinal)
                    && method.Modifiers.Any(SyntaxKind.PublicKeyword))
                {
                    yield return method;
                }
            }
        }
    }

    private static bool DelegatesToACheckerComponent(MethodDeclarationSyntax method)
    {
        return method.DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .Any(access => access.Expression is IdentifierNameSyntax identifier
                && identifier.Identifier.ValueText.EndsWith("Checker", StringComparison.Ordinal));
    }

    private static IReadOnlyList<StatementSyntax> BodyStatements(MethodDeclarationSyntax method)
    {
        return method.Body?.Statements.ToList() ?? new List<StatementSyntax>();
    }

    private static int BodyStatementCountOf(string source, string methodName)
    {
        MethodDeclarationSyntax method = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(m => m.Identifier.ValueText == methodName);
        return BodyStatements(method).Count;
    }

    private static IEnumerable<string> CheckerSourceFiles()
    {
        return Directory.EnumerateFiles(Path.Combine(ExecutionDirectory(), "Checkers"), "*.cs");
    }

    private static CompilationUnitSyntax ParseFile(string path)
    {
        return CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetCompilationUnitRoot();
    }

    private static string ExecutionDirectory()
    {
        return Path.Combine(RepositoryRoot(), "src", "ArchLinterNet.Core", "Execution");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null && directory.GetFiles("ArchLinterNet.slnx").Length == 0)
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find repo root");
    }
}
