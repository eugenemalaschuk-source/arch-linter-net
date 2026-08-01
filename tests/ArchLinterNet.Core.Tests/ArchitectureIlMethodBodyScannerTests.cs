using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureIlMethodBodyScannerTests
{
    private static readonly Assembly[] _fixtureAssembly = [typeof(MethodBodyFixture).Assembly];
    private static readonly string[] _consoleWriteLinePattern = ["Console.WriteLine"];

    [Test]
    public void FindMethodBodyViolations_EmptyNamespace_ReturnsEmpty()
    {
        var scanner = new ArchitectureIlMethodBodyScanner();
        var context = new ArchitectureContractExecutionContext(
            "method-body", "method-body-id", Array.Empty<ArchitectureIgnoredViolation>(), false, null, null);

        Assert.That(scanner.FindMethodBodyViolations(
            _fixtureAssembly,
            "NamespaceThatDoesNotExist",
            _consoleWriteLinePattern,
            context).ToList(), Is.Empty);
    }

    // PR #416 review round 2: this scanner previously accepted no CancellationToken at all, so a
    // large source-type set could be walked (types, methods, IL instructions) to completion with
    // no way to interrupt it — only the surrounding per-contract-family boundary could stop it,
    // which is too coarse for a single expensive contract.
    [Test]
    public void FindMethodBodyViolations_PreCancelledToken_ThrowsOperationCanceledException()
    {
        var scanner = new ArchitectureIlMethodBodyScanner();
        var context = new ArchitectureContractExecutionContext(
            "method-body", "method-body-id", Array.Empty<ArchitectureIgnoredViolation>(), false, null, null);
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => scanner.FindMethodBodyViolations(
            _fixtureAssembly,
            typeof(MethodBodyFixture).Namespace!,
            _consoleWriteLinePattern,
            context,
            cancellationToken: cts.Token).ToList());
    }

    [Test]
    public void FindMatchesForType_ReturnsMemberAndMethodDetails()
    {
        IReadOnlyList<ForbiddenCallPattern> patterns = ArchitectureForbiddenCallMatcher.NormalizePatterns(
            _consoleWriteLinePattern);
        var cache = new Dictionary<string, bool>(StringComparer.Ordinal);

        var details = ArchitectureIlMethodBodyScanner.FindMatchDetailsForType(
            typeof(MethodBodyFixture), patterns, cache).ToList();
        var members = ArchitectureIlMethodBodyScanner.FindMatchesForType(
            typeof(MethodBodyFixture), patterns, cache).ToList();

        Assert.That(details, Is.Not.Empty);
        Assert.That(details[0].SourceMember, Does.Contain("CallsForbiddenMethod"));
        Assert.That(details[0].MatchedMember, Does.Contain("Console.WriteLine"));
        Assert.That(details[0].TargetType, Is.EqualTo(typeof(Console).FullName));
        Assert.That(details[0].TargetAssembly, Is.EqualTo(typeof(Console).Assembly.GetName().Name));
        Assert.That(members, Does.Contain(details[0].MatchedMember));
    }

    private sealed class MethodBodyFixture
    {
        public static void CallsForbiddenMethod()
        {
            Console.WriteLine("fixture");
        }
    }
}
