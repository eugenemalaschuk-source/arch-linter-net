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
///
/// <para>
/// Three separate rules are needed, because each closes a different way family logic could creep
/// back in (PR #580 review): a size bound over <em>every</em> nested statement stops logic being
/// written inline (including inside an <c>if</c>/<c>foreach</c>/lambda, which a top-level-only
/// count would miss); a reachability rule stops it being hidden in a session-private helper the
/// entry point calls; and a dispatch rule stops a family being routed to a session method these
/// tests never look at.
/// </para>
/// </summary>
[TestFixture]
public sealed class ArchitectureAnalysisSessionCheckerOwnershipTests
{
    // A lifecycle wrapper is: the selection/deferral guard, any pre-existing precondition guard,
    // the execution-context creation, the delegation, the unmatched-ignore collection, and the
    // return. The bound counts every nested statement, not just top-level ones, so logic cannot be
    // hidden one block deep. It is deliberately loose enough for the few families that also publish
    // baseline candidates or append a separately-ordered finding set (the current maximum is 8),
    // and far too tight for any real family algorithm.
    private const int MaxLifecycleStatements = 10;

    // Coverage is explicitly out of scope for the #452 extraction: it is not contract-family
    // checking over declared types but a policy-inventory report over the whole document, and the
    // issue keeps coverage/policy-consistency behavior where it is. Extracting it is separate work
    // with its own semantics, not an omission hidden behind this allowlist.
    private static readonly HashSet<string> _sessionOwnedEntryPoints = new(StringComparer.Ordinal)
    {
        "CheckCoverageContract",
    };

    // The only session-declared methods a family entry point may call. Every entry is lifecycle or
    // fact access — never family behavior — and adding one is the reviewable moment where a new
    // family would otherwise smuggle checking back into the session:
    //
    //   IsContractSelected                       contract selection (lifecycle)
    //   IsDanglingButCoveredByRuleInputCoverage  rule-input-coverage deferral (lifecycle)
    //   CreateExecutionContext                   per-contract execution context (lifecycle)
    //   AddCycleBaselineCandidates               baseline-candidate publication (session-owned state)
    private static readonly HashSet<string> _allowedSessionCalls = new(StringComparer.Ordinal)
    {
        "IsContractSelected",
        "IsDanglingButCoveredByRuleInputCoverage",
        "CreateExecutionContext",
        "AddCycleBaselineCandidates",
    };

    [Test]
    public void EveryContractFamilyEntryPoint_IsAThinLifecycleWrapperOverACheckerComponent()
    {
        List<MethodDeclarationSyntax> entryPoints = GovernedEntryPoints();

        foreach (MethodDeclarationSyntax method in entryPoints)
        {
            string name = method.Identifier.ValueText;

            // Counts nested statements too: a top-level-only count would pass a method whose whole
            // family algorithm sits inside one `if` or `foreach`.
            int statements = method.DescendantNodes().OfType<StatementSyntax>().Count();

            Assert.That(statements, Is.LessThanOrEqualTo(MaxLifecycleStatements),
                $"{name} contains {statements} statements (nested included). Contract-family checking belongs in a "
                + "checker under ArchLinterNet.Core.Execution.Checkers, not in ArchitectureAnalysisSession; "
                + "the session entry point should only do selection, execution-context lifecycle and delegation.");

            Assert.That(DelegatesToAFamilyAnalysisComponent(method), Is.True,
                $"{name} does not delegate to a family-analysis component. Keep the session as a lifecycle "
                + "facade; the component must in turn delegate family semantics to a checker under "
                + "ArchLinterNet.Core.Execution.Checkers.");
        }
    }

    // Without this rule the size bound is trivially defeated: move the family algorithm into a new
    // private ArchitectureAnalysisSession helper, leave a single FooChecker.Check(...) call behind,
    // and the entry point still looks thin (PR #580 review). Restricting which session-declared
    // methods an entry point may call makes every such helper unreachable from the family's own
    // execution path, so it cannot be written there at all.
    [Test]
    public void ContractFamilyEntryPoints_CallNoSessionMethodOutsideTheLifecycleAllowlist()
    {
        HashSet<string> sessionMethods = SessionDeclaredMethodNames();
        List<string> offenders = new();

        foreach (MethodDeclarationSyntax method in GovernedEntryPoints())
        {
            foreach (string called in SelfInvokedMethodNames(method))
            {
                if (sessionMethods.Contains(called) && !_allowedSessionCalls.Contains(called))
                {
                    offenders.Add($"{method.Identifier.ValueText} -> {called}");
                }
            }
        }

        Assert.That(offenders, Is.Empty,
            "A contract-family entry point may only call session methods that are lifecycle or fact access "
            + $"({string.Join(", ", _allowedSessionCalls.OrderBy(name => name, StringComparer.Ordinal))}). "
            + "Anything else is family behavior living in the session — move it into the family's checker. "
            + $"Offending calls: {string.Join("; ", offenders)}");
    }

    // The two rules above only govern methods this fixture can find. A new family whose descriptor
    // pointed at, say, `session.RunPortBoundaryFamily(...)` would be governed by neither, so the
    // registry itself must only ever dispatch into a method matching the entry-point shape.
    [Test]
    public void RegistryDispatchesOnlyIntoDiscoverableContractFamilyEntryPoints()
    {
        CompilationUnitSyntax registry = ParseFile(
            Path.Combine(ExecutionDirectory(), "ArchitectureContractFamilyRegistry.cs"));

        List<string> offenders = registry.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Select(invocation => invocation.Expression)
            .OfType<MemberAccessExpressionSyntax>()
            .Where(access => access.Expression is IdentifierNameSyntax { Identifier.ValueText: "session" })
            .Select(access => access.Name.Identifier.ValueText)
            .Where(name => !IsEntryPointName(name) && !_allowedSessionCalls.Contains(name))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.That(offenders, Is.Empty,
            "Every contract family must dispatch into a Check*Contract session entry point, so the ownership "
            + "rules in this fixture actually govern it. Offending registry calls: "
            + string.Join(", ", offenders));
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

        string service = File.ReadAllText(
            Path.Combine(ExecutionDirectory(), "ArchitectureFindingIdentityService.cs"));
        Assert.That(service, Does.Contain("ArchitectureFindingIdentityAttributor.Attach"));

        // The session keeps the candidate log and cursor only; attribution remains delegated.
        string session = File.ReadAllText(Path.Combine(ExecutionDirectory(), "ArchitectureAnalysisSession.cs"));
        Assert.That(session, Does.Contain("_findingIdentityService.Attach"));
        Assert.That(NestedStatementCountOf(session, "AttachFindingIdentities"), Is.LessThanOrEqualTo(2),
            "AttachFindingIdentities must stay a delegation to ArchitectureFindingIdentityService.");
    }

    private static List<MethodDeclarationSyntax> GovernedEntryPoints()
    {
        List<MethodDeclarationSyntax> entryPoints = ContractFamilyEntryPoints()
            .Where(method => !_sessionOwnedEntryPoints.Contains(method.Identifier.ValueText))
            .ToList();

        // Guards the test itself: a rename or a moved partial must not silently reduce these rules
        // to asserting nothing.
        Assert.That(entryPoints, Has.Count.GreaterThan(20),
            "Expected to find the session's contract-family entry points; the discovery pattern is probably stale.");

        return entryPoints;
    }

    private static IEnumerable<MethodDeclarationSyntax> ContractFamilyEntryPoints()
    {
        foreach (string file in SessionSourceFiles())
        {
            CompilationUnitSyntax root = ParseFile(file);

            foreach (MethodDeclarationSyntax method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (IsEntryPointName(method.Identifier.ValueText) && method.Modifiers.Any(SyntaxKind.PublicKeyword))
                {
                    yield return method;
                }
            }
        }
    }

    private static bool IsEntryPointName(string name)
    {
        return name.StartsWith("Check", StringComparison.Ordinal)
            && name.EndsWith("Contract", StringComparison.Ordinal);
    }

    private static HashSet<string> SessionDeclaredMethodNames()
    {
        HashSet<string> names = new(StringComparer.Ordinal);

        foreach (string file in SessionSourceFiles())
        {
            foreach (MethodDeclarationSyntax method in ParseFile(file).DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                names.Add(method.Identifier.ValueText);
            }
        }

        return names;
    }

    // Invocations against the session instance itself: a bare `Foo(...)` (implicit `this`) or an
    // explicit `this.Foo(...)`. Calls qualified by any other receiver — a checker type, the
    // execution context, a local — are somebody else's method and are not this rule's business.
    private static IEnumerable<string> SelfInvokedMethodNames(MethodDeclarationSyntax method)
    {
        foreach (InvocationExpressionSyntax invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            switch (invocation.Expression)
            {
                case IdentifierNameSyntax identifier:
                    yield return identifier.Identifier.ValueText;
                    break;
                case MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } thisAccess:
                    yield return thisAccess.Name.Identifier.ValueText;
                    break;
            }
        }
    }

    private static bool DelegatesToAFamilyAnalysisComponent(MethodDeclarationSyntax method)
    {
        return method.DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .Any(access => access.Expression is IdentifierNameSyntax identifier
                && (identifier.Identifier.ValueText.EndsWith("Checker", StringComparison.Ordinal)
                    || identifier.Identifier.ValueText.EndsWith("CheckingService", StringComparison.Ordinal)
                    || identifier.Identifier.ValueText.EndsWith("AnalysisService", StringComparison.Ordinal)));
    }

    private static int NestedStatementCountOf(string source, string methodName)
    {
        MethodDeclarationSyntax method = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(m => m.Identifier.ValueText == methodName);
        return method.DescendantNodes().OfType<StatementSyntax>().Count();
    }

    private static IEnumerable<string> SessionSourceFiles()
    {
        return Directory.EnumerateFiles(ExecutionDirectory(), "ArchitectureAnalysisSession*.cs");
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
