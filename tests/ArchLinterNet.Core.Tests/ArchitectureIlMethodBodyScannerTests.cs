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
