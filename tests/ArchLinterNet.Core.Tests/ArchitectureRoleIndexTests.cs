using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Scanning;
using AttributeRoleExtractionTestFixtures;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureRoleIndexTests
{
    private static readonly System.Reflection.Assembly[] _targetAssemblies = { typeof(PlainType).Assembly };

    private static ArchitectureRoleIndex CreateIndex(ArchitectureClassificationConfiguration classification)
    {
        return new ArchitectureRoleIndex(classification, new ArchitectureTypeIndex(_targetAssemblies));
    }

    private static ArchitectureClassificationConfiguration DomainAttributeConfiguration()
    {
        return new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute",
                    Role = "DomainLayer",
                    Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" }
                }
            }
        };
    }

    [Test]
    public void TryGetRole_ClassifiedType_ReturnsDescriptorWithRoleMetadataAndSource()
    {
        ArchitectureRoleIndex index = CreateIndex(DomainAttributeConfiguration());

        bool found = index.TryGetRole(typeof(TypeWithConstructorDefault), out ArchitectureTypeClassificationResult descriptor);

        Assert.That(found, Is.True);
        Assert.That(descriptor.Role, Is.EqualTo("DomainLayer"));
        Assert.That(descriptor.Source, Is.EqualTo(ArchitectureClassificationSource.TypeAttribute));
        Assert.That(descriptor.Evidence, Is.EqualTo("AttributeRoleExtractionTestFixtures.DomainMarkerAttribute"));
        Assert.That(descriptor.Metadata["domain"], Is.EqualTo("Sales"));
    }

    [Test]
    public void TryGetRole_UnclassifiedType_ReturnsFalse()
    {
        ArchitectureRoleIndex index = CreateIndex(DomainAttributeConfiguration());

        bool found = index.TryGetRole(typeof(PlainType), out _);

        Assert.That(found, Is.False);
    }

    [Test]
    public void TryGetRole_ClosedConstructedGeneric_FallsBackToGenericTypeDefinition()
    {
        // Regression (#306 review 3.2): the index is built from assembly.GetTypes(), which only ever
        // yields the open generic type definition (OpenGenericDomainType<>). Reflection on a concrete
        // reference/adapter reports the closed constructed type (OpenGenericDomainType<PlainType>)
        // instead, which is a distinct Type object that never equals the open definition as a
        // dictionary key without an explicit fallback.
        ArchitectureRoleIndex index = CreateIndex(DomainAttributeConfiguration());

        bool found = index.TryGetRole(typeof(OpenGenericDomainType<PlainType>), out ArchitectureTypeClassificationResult descriptor);

        Assert.That(found, Is.True);
        Assert.That(descriptor.Role, Is.EqualTo("DomainLayer"));
        Assert.That(descriptor.Metadata["domain"], Is.EqualTo("Sales"));
    }

    [Test]
    public void TryGetRole_UnclassifiedClosedConstructedGeneric_ReturnsFalse()
    {
        ArchitectureRoleIndex index = CreateIndex(DomainAttributeConfiguration());

        bool found = index.TryGetRole(typeof(PlainOpenGenericType<PlainType>), out _);

        Assert.That(found, Is.False);
    }

    [Test]
    public void TryGetRole_TypeAttributeOverridesAssemblyAttribute_DescriptorNamesWinningSource()
    {
        var classification = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute",
                    Role = "DomainLayer"
                }
            },
            AssemblyAttributes =
            {
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = "AttributeRoleExtractionTestFixtures.BoundedContextMarkerAttribute",
                    Role = "ApplicationLayer"
                }
            }
        };

        ArchitectureRoleIndex index = CreateIndex(classification);

        Assert.That(index.TryGetRole(typeof(TypeOverridingAssemblyAttribute), out ArchitectureTypeClassificationResult overridden), Is.True);
        Assert.That(overridden.Source, Is.EqualTo(ArchitectureClassificationSource.TypeAttribute));
        Assert.That(overridden.Evidence, Is.EqualTo("AttributeRoleExtractionTestFixtures.DomainMarkerAttribute"));

        Assert.That(index.TryGetRole(typeof(TypeRelyingOnAssemblyAttribute), out ArchitectureTypeClassificationResult fallback), Is.True);
        Assert.That(fallback.Source, Is.EqualTo(ArchitectureClassificationSource.AssemblyAttribute));
        Assert.That(fallback.Evidence, Is.EqualTo("AttributeRoleExtractionTestFixtures.BoundedContextMarkerAttribute"));
    }

    [Test]
    public void ClassifiedTypes_EnumeratesExactlyTheTypesWithAResolvedRole()
    {
        ArchitectureRoleIndex index = CreateIndex(DomainAttributeConfiguration());

        IReadOnlyCollection<Type> classified = index.ClassifiedTypes();

        Assert.That(classified, Does.Contain(typeof(TypeWithConstructorDefault)));
        Assert.That(classified, Does.Not.Contain(typeof(PlainType)));
    }

    [Test]
    public void ClassifiedTypes_RepeatedCalls_ReuseTheSameCachedPass()
    {
        ArchitectureRoleIndex index = CreateIndex(DomainAttributeConfiguration());

        IReadOnlyCollection<Type> first = index.ClassifiedTypes();
        IReadOnlyCollection<Type> second = index.ClassifiedTypes();

        Assert.That(ReferenceEquals(first, second), Is.True);
    }

    [Test]
    public void Conflicts_MatchesExtractorOutputForTheSameConfiguration()
    {
        var classification = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute",
                    Role = "DomainLayer"
                },
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = "AttributeRoleExtractionTestFixtures.SecondMarkerAttribute",
                    Role = "InfrastructureLayer"
                }
            }
        };

        ArchitectureRoleIndex index = CreateIndex(classification);

        Assert.That(index.Conflicts.Any(c => c.Subject == "AttributeRoleExtractionTestFixtures.TypeWithConflictingEntries"), Is.True);
    }

    [Test]
    public void MetadataFailures_MatchesExtractorOutputForTheSameConfiguration()
    {
        var classification = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute",
                    Role = "DomainLayer",
                    Metadata = new Dictionary<string, object> { ["module"] = "property:Module" }
                }
            }
        };

        ArchitectureRoleIndex index = CreateIndex(classification);

        Assert.That(
            index.MetadataFailures.Any(
                f => f.Subject == "AttributeRoleExtractionTestFixtures.TypeWithUnsuppliedNamedProperty" && f.MetadataKey == "module"),
            Is.True);
    }

    [Test]
    public void EmptyClassificationConfiguration_ShortCircuitsWithoutScanningTypes()
    {
        var countingAssemblies = new EnumerationCountingCollection(_targetAssemblies);
        var index = new ArchitectureRoleIndex(new ArchitectureClassificationConfiguration(), new ArchitectureTypeIndex(countingAssemblies));

        Assert.That(index.ClassifiedTypes(), Is.Empty);
        Assert.That(index.Conflicts, Is.Empty);
        Assert.That(index.MetadataFailures, Is.Empty);
        Assert.That(countingAssemblies.EnumerationCount, Is.EqualTo(0));
    }

    [Test]
    public void Constructor_NullConfiguration_Throws()
    {
        Assert.That(
            () => new ArchitectureRoleIndex(null!, new ArchitectureTypeIndex(_targetAssemblies)),
            Throws.ArgumentNullException);
    }

    [Test]
    public void Constructor_NullTypeIndex_Throws()
    {
        Assert.That(
            () => new ArchitectureRoleIndex(new ArchitectureClassificationConfiguration(), null!),
            Throws.ArgumentNullException);
    }

    private sealed class EnumerationCountingCollection : IReadOnlyCollection<System.Reflection.Assembly>
    {
        private readonly System.Reflection.Assembly[] _items;

        public EnumerationCountingCollection(System.Reflection.Assembly[] items)
        {
            _items = items;
        }

        public int EnumerationCount { get; private set; }

        public int Count => _items.Length;

        public IEnumerator<System.Reflection.Assembly> GetEnumerator()
        {
            EnumerationCount++;
            return _items.AsEnumerable().GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
