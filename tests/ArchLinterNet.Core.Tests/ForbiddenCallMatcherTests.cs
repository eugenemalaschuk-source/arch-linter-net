using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ForbiddenCallMatcherTests
{
    private static readonly string[] _value = { "MyApp.Services.MyService." };
    private static readonly string[] _value1 = { "Microsoft.Extensions.DependencyInjection.IServiceCollection." };
    private static readonly string[] _value2 = { "MyApp.Services.MyService.MyMethod" };
    private static readonly string[] _value3 = { "SomethingElse" };
    private static readonly string[] _foo = { "_foo" };
    private static readonly string[] _fooWithParen = { "_foo(" };
    private static readonly string[] _myAppServices = { "MyApp.Services." };

    [Test]
    public void NormalizePatterns_RemovesTrailingParentheses()
    {
        IReadOnlyList<ForbiddenCallPattern> patterns = ArchitectureForbiddenCallMatcher.NormalizePatterns(
            _fooWithParen);

        Assert.That(patterns.Count, Is.EqualTo(1));
        Assert.That(patterns[0].Normalized, Is.EqualTo("_foo"));
    }

    [Test]
    public void NormalizePatterns_DetectsNamespacePrefix()
    {
        IReadOnlyList<ForbiddenCallPattern> patterns = ArchitectureForbiddenCallMatcher.NormalizePatterns(
            _myAppServices);

        Assert.That(patterns.Count, Is.EqualTo(1));
        Assert.That(patterns[0].IsNamespacePrefix, Is.True);
        Assert.That(patterns[0].Normalized, Is.EqualTo("MyApp.Services"));
    }

    [Test]
    public void TryMatch_ExactNameMatch_ReturnsTrue()
    {
        IReadOnlyList<ForbiddenCallPattern> patterns = ArchitectureForbiddenCallMatcher.NormalizePatterns(
            _foo);
        var descriptor = new SymbolDescriptor("_foo", "Bar", "Baz", "Baz.Bar._foo");
        var cache = new Dictionary<string, bool>();

        bool matched = ArchitectureForbiddenCallMatcher.TryMatch(descriptor, patterns, cache, out string raw);

        Assert.That(matched, Is.True);
        Assert.That(raw, Is.EqualTo("_foo"));
    }

    [Test]
    public void TryMatch_NamespacePrefixMatch_ReturnsTrue()
    {
        IReadOnlyList<ForbiddenCallPattern> patterns = ArchitectureForbiddenCallMatcher.NormalizePatterns(
            _myAppServices);
        var descriptor = new SymbolDescriptor("MyMethod", "MyService", "MyApp.Services", "MyApp.Services.MyService.MyMethod");
        var cache = new Dictionary<string, bool>();

        bool matched = ArchitectureForbiddenCallMatcher.TryMatch(descriptor, patterns, cache, out string raw);

        Assert.That(matched, Is.True);
        Assert.That(raw, Is.EqualTo("MyApp.Services."));
    }

    [Test]
    public void TryMatch_TrailingDotPattern_MatchesFullyQualifiedTypePrefix()
    {
        IReadOnlyList<ForbiddenCallPattern> patterns = ArchitectureForbiddenCallMatcher.NormalizePatterns(
            _value);
        var descriptor = new SymbolDescriptor(
            "MyMethod",
            "MyService",
            "MyApp.Services",
            "MyApp.Services.MyService.MyMethod");
        var cache = new Dictionary<string, bool>();

        bool matched = ArchitectureForbiddenCallMatcher.TryMatch(descriptor, patterns, cache, out string raw);

        Assert.That(matched, Is.True);
        Assert.That(raw, Is.EqualTo("MyApp.Services.MyService."));
    }

    [Test]
    public void TryMatch_TrailingDotPattern_MatchesExtensionReceiverType()
    {
        IReadOnlyList<ForbiddenCallPattern> patterns = ArchitectureForbiddenCallMatcher.NormalizePatterns(
            _value1);
        var descriptor = new SymbolDescriptor(
            "AddSingleton",
            "ServiceCollectionServiceExtensions",
            "Microsoft.Extensions.DependencyInjection",
            "Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton",
            "Microsoft.Extensions.DependencyInjection.IServiceCollection");
        var cache = new Dictionary<string, bool>();

        bool matched = ArchitectureForbiddenCallMatcher.TryMatch(descriptor, patterns, cache, out string raw);

        Assert.That(matched, Is.True);
        Assert.That(raw, Is.EqualTo("Microsoft.Extensions.DependencyInjection.IServiceCollection."));
    }

    [Test]
    public void TryMatch_FullyQualifiedMatch_ReturnsTrue()
    {
        IReadOnlyList<ForbiddenCallPattern> patterns = ArchitectureForbiddenCallMatcher.NormalizePatterns(
            _value2);
        var descriptor = new SymbolDescriptor("MyMethod", "MyService", "MyApp.Services", "MyApp.Services.MyService.MyMethod");
        var cache = new Dictionary<string, bool>();

        bool matched = ArchitectureForbiddenCallMatcher.TryMatch(descriptor, patterns, cache, out string raw);

        Assert.That(matched, Is.True);
        Assert.That(raw, Is.EqualTo("MyApp.Services.MyService.MyMethod"));
    }

    [Test]
    public void TryMatch_NoMatch_ReturnsFalse()
    {
        IReadOnlyList<ForbiddenCallPattern> patterns = ArchitectureForbiddenCallMatcher.NormalizePatterns(
            _value3);
        var descriptor = new SymbolDescriptor("_foo", "Bar", "Baz", "Baz.Bar._foo");
        var cache = new Dictionary<string, bool>();

        bool matched = ArchitectureForbiddenCallMatcher.TryMatch(descriptor, patterns, cache, out _);

        Assert.That(matched, Is.False);
    }

    [Test]
    public void TryMatch_CachesResults()
    {
        IReadOnlyList<ForbiddenCallPattern> patterns = ArchitectureForbiddenCallMatcher.NormalizePatterns(
            _foo);
        var descriptor = new SymbolDescriptor("_foo", "Bar", "Baz", "Baz.Bar._foo");
        var cache = new Dictionary<string, bool>();

        ArchitectureForbiddenCallMatcher.TryMatch(descriptor, patterns, cache, out _);
        ArchitectureForbiddenCallMatcher.TryMatch(descriptor, patterns, cache, out _);

        Assert.That(cache.Count, Is.EqualTo(1));
    }
}
