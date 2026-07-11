using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Scanning;
using ContextualContractTestFixtures;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Unit tests for ArchitectureContextSelectorMatcher's four-operator metadata grammar (exact, in,
// any, not-equal-to-source), per tasks.md 1.3. See design.md Decision 2 and
// specs/contextual-dependency-contracts/spec.md "Contextual metadata constraints support four
// deterministic operators".
[TestFixture]
public sealed class ArchitectureContextSelectorMatcherTests
{
    private static ArchitectureRoleIndex CreateRoleIndex()
    {
        var classification = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = "ContextualContractTestFixtures.ContextDomainMarkerAttribute",
                    Role = "DomainLayer",
                    Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" }
                },
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = "ContextualContractTestFixtures.ContextDomainlessMarkerAttribute",
                    Role = "DomainLayer"
                },
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = "ContextualContractTestFixtures.ContextSharedKernelMarkerAttribute",
                    Role = "SharedKernel"
                }
            }
        };

        return new ArchitectureRoleIndex(classification, new ArchitectureTypeIndex(new[] { typeof(SalesOrder).Assembly }));
    }

    private static ArchitectureTypeClassificationResult Descriptor(ArchitectureRoleIndex index, Type type)
    {
        Assert.That(index.TryGetRole(type, out ArchitectureTypeClassificationResult descriptor), Is.True,
            $"Expected {type.Name} to resolve a role.");
        return descriptor;
    }

    // --- role matching ---

    [Test]
    public void Matches_RoleMismatch_ReturnsFalse()
    {
        ArchitectureRoleIndex index = CreateRoleIndex();
        var selector = new ArchitectureContextSelector { Role = "SharedKernel" };

        Assert.That(ArchitectureContextSelectorMatcher.Matches(selector, typeof(SalesOrder), index, null), Is.False);
    }

    [Test]
    public void Matches_UnclassifiedCandidate_ReturnsFalse()
    {
        ArchitectureRoleIndex index = CreateRoleIndex();
        var selector = new ArchitectureContextSelector { Role = "DomainLayer" };

        Assert.That(ArchitectureContextSelectorMatcher.Matches(selector, typeof(PlainUnclassifiedType), index, null), Is.False);
    }

    // --- exact operator ---

    [Test]
    public void Matches_ExactOperator_MatchesEqualLiteral()
    {
        ArchitectureRoleIndex index = CreateRoleIndex();
        var selector = new ArchitectureContextSelector
        {
            Role = "DomainLayer",
            Metadata = new Dictionary<string, object> { ["domain"] = "Sales" }
        };

        Assert.That(ArchitectureContextSelectorMatcher.Matches(selector, typeof(SalesOrder), index, null), Is.True);
    }

    [Test]
    public void Matches_ExactOperator_MismatchedLiteral_ReturnsFalse()
    {
        ArchitectureRoleIndex index = CreateRoleIndex();
        var selector = new ArchitectureContextSelector
        {
            Role = "DomainLayer",
            Metadata = new Dictionary<string, object> { ["domain"] = "Sales" }
        };

        Assert.That(ArchitectureContextSelectorMatcher.Matches(selector, typeof(InventoryStockItem), index, null), Is.False);
    }

    // --- in operator ---

    [Test]
    public void Matches_InOperator_MatchesAnyListedEntry()
    {
        ArchitectureRoleIndex index = CreateRoleIndex();
        var selector = new ArchitectureContextSelector
        {
            Role = "DomainLayer",
            Metadata = new Dictionary<string, object> { ["domain"] = new List<object> { "Sales", "Inventory" } }
        };

        Assert.That(ArchitectureContextSelectorMatcher.Matches(selector, typeof(SalesOrder), index, null), Is.True);
        Assert.That(ArchitectureContextSelectorMatcher.Matches(selector, typeof(InventoryStockItem), index, null), Is.True);
    }

    [Test]
    public void Matches_InOperator_UnlistedValue_ReturnsFalse()
    {
        ArchitectureRoleIndex index = CreateRoleIndex();
        var selector = new ArchitectureContextSelector
        {
            Role = "DomainLayer",
            Metadata = new Dictionary<string, object> { ["domain"] = new List<object> { "Billing", "Marketing" } }
        };

        Assert.That(ArchitectureContextSelectorMatcher.Matches(selector, typeof(SalesOrder), index, null), Is.False);
    }

    // --- any operator ---

    [Test]
    public void Matches_AnyOperator_PresentKey_MatchesRegardlessOfValue()
    {
        ArchitectureRoleIndex index = CreateRoleIndex();
        var selector = new ArchitectureContextSelector
        {
            Role = "DomainLayer",
            Metadata = new Dictionary<string, object> { ["domain"] = "*" }
        };

        Assert.That(ArchitectureContextSelectorMatcher.Matches(selector, typeof(SalesOrder), index, null), Is.True);
        Assert.That(ArchitectureContextSelectorMatcher.Matches(selector, typeof(InventoryStockItem), index, null), Is.True);
    }

    [Test]
    public void Matches_AnyOperator_AbsentKey_ReturnsFalse()
    {
        ArchitectureRoleIndex index = CreateRoleIndex();
        var selector = new ArchitectureContextSelector
        {
            Role = "DomainLayer",
            Metadata = new Dictionary<string, object> { ["domain"] = "*" }
        };

        Assert.That(ArchitectureContextSelectorMatcher.Matches(selector, typeof(DomainlessTargetType), index, null), Is.False);
    }

    // --- not-equal-to-source operator ---

    [Test]
    public void Matches_NotEqualToSourceOperator_DifferingValue_ReturnsTrue()
    {
        ArchitectureRoleIndex index = CreateRoleIndex();
        ArchitectureTypeClassificationResult sourceDescriptor = Descriptor(index, typeof(SalesOrder)); // domain=Sales
        var selector = new ArchitectureContextSelector
        {
            Role = "DomainLayer",
            Metadata = new Dictionary<string, object> { ["domain"] = "!{source.metadata.domain}" }
        };

        Assert.That(
            ArchitectureContextSelectorMatcher.Matches(selector, typeof(InventoryStockItem), index, sourceDescriptor),
            Is.True);
    }

    [Test]
    public void Matches_NotEqualToSourceOperator_EqualValue_ReturnsFalse()
    {
        ArchitectureRoleIndex index = CreateRoleIndex();
        ArchitectureTypeClassificationResult sourceDescriptor = Descriptor(index, typeof(SalesOrder)); // domain=Sales
        var selector = new ArchitectureContextSelector
        {
            Role = "DomainLayer",
            Metadata = new Dictionary<string, object> { ["domain"] = "!{source.metadata.domain}" }
        };

        Assert.That(
            ArchitectureContextSelectorMatcher.Matches(selector, typeof(SalesOrderLine), index, sourceDescriptor),
            Is.False);
    }

    [Test]
    public void Matches_NotEqualToSourceOperator_MissingSourceKey_ReturnsFalse()
    {
        ArchitectureRoleIndex index = CreateRoleIndex();
        ArchitectureTypeClassificationResult sourceDescriptor = Descriptor(index, typeof(DomainlessSourceType)); // no domain key
        var selector = new ArchitectureContextSelector
        {
            Role = "DomainLayer",
            Metadata = new Dictionary<string, object> { ["domain"] = "!{source.metadata.domain}" }
        };

        Assert.That(
            ArchitectureContextSelectorMatcher.Matches(selector, typeof(InventoryStockItem), index, sourceDescriptor),
            Is.False);
    }

    [Test]
    public void Matches_NotEqualToSourceOperator_NullSourceDescriptor_ReturnsFalse()
    {
        ArchitectureRoleIndex index = CreateRoleIndex();
        var selector = new ArchitectureContextSelector
        {
            Role = "DomainLayer",
            Metadata = new Dictionary<string, object> { ["domain"] = "!{source.metadata.domain}" }
        };

        Assert.That(
            ArchitectureContextSelectorMatcher.Matches(selector, typeof(InventoryStockItem), index, sourceDescriptor: null),
            Is.False);
    }

    // --- missing constrained metadata key on the candidate ---

    [Test]
    public void Matches_CandidateMissingConstrainedKey_ReturnsFalse()
    {
        ArchitectureRoleIndex index = CreateRoleIndex();
        var selector = new ArchitectureContextSelector
        {
            Role = "DomainLayer",
            Metadata = new Dictionary<string, object> { ["domain"] = "Sales" }
        };

        Assert.That(ArchitectureContextSelectorMatcher.Matches(selector, typeof(DomainlessTargetType), index, null), Is.False);
    }
}
