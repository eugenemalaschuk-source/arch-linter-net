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

    [Test]
    public void TryGetRole_DirectInheritanceMatch_AssignsRoleWithBaseTypeEvidence()
    {
        var classification = new ArchitectureClassificationConfiguration
        {
            Inheritance =
            {
                new ArchitectureInheritanceClassificationMapping
                {
                    BaseType = "AttributeRoleExtractionTestFixtures.DomainEntityBase",
                    Role = "DomainLayer"
                }
            }
        };

        ArchitectureRoleIndex index = CreateIndex(classification);

        Assert.That(index.TryGetRole(typeof(DirectlyDerivedEntity), out ArchitectureTypeClassificationResult descriptor), Is.True);
        Assert.That(descriptor.Role, Is.EqualTo("DomainLayer"));
        Assert.That(descriptor.Source, Is.EqualTo(ArchitectureClassificationSource.Inheritance));
        Assert.That(descriptor.Evidence, Is.EqualTo("AttributeRoleExtractionTestFixtures.DomainEntityBase"));
    }

    [Test]
    public void TryGetRole_TransitiveInheritanceMatch_AssignsRole()
    {
        var classification = new ArchitectureClassificationConfiguration
        {
            Inheritance =
            {
                new ArchitectureInheritanceClassificationMapping
                {
                    BaseType = "AttributeRoleExtractionTestFixtures.DomainEntityBase",
                    Role = "DomainLayer"
                }
            }
        };

        ArchitectureRoleIndex index = CreateIndex(classification);

        Assert.That(index.TryGetRole(typeof(TransitivelyDerivedEntity), out ArchitectureTypeClassificationResult descriptor), Is.True);
        Assert.That(descriptor.Role, Is.EqualTo("DomainLayer"));
    }

    [Test]
    public void TryGetRole_InterfaceImplementationMatch_AssignsRole()
    {
        var classification = new ArchitectureClassificationConfiguration
        {
            Inheritance =
            {
                new ArchitectureInheritanceClassificationMapping
                {
                    BaseType = "AttributeRoleExtractionTestFixtures.IRepositoryMarker",
                    Role = "InfrastructureLayer"
                }
            }
        };

        ArchitectureRoleIndex index = CreateIndex(classification);

        Assert.That(index.TryGetRole(typeof(RepositoryImplementation), out ArchitectureTypeClassificationResult descriptor), Is.True);
        Assert.That(descriptor.Role, Is.EqualTo("InfrastructureLayer"));
        Assert.That(descriptor.Source, Is.EqualTo(ArchitectureClassificationSource.Inheritance));
    }

    [Test]
    public void TryGetRole_UnrelatedTypeAgainstInheritanceEntry_ReturnsFalse()
    {
        var classification = new ArchitectureClassificationConfiguration
        {
            Inheritance =
            {
                new ArchitectureInheritanceClassificationMapping
                {
                    BaseType = "AttributeRoleExtractionTestFixtures.DomainEntityBase",
                    Role = "DomainLayer"
                }
            }
        };

        ArchitectureRoleIndex index = CreateIndex(classification);

        Assert.That(index.TryGetRole(typeof(UnrelatedType), out _), Is.False);
    }

    [Test]
    public void TryGetRole_UnresolvedBaseType_SilentlyNoMatchesWithNoDiagnostic()
    {
        var classification = new ArchitectureClassificationConfiguration
        {
            Inheritance =
            {
                new ArchitectureInheritanceClassificationMapping
                {
                    BaseType = "AttributeRoleExtractionTestFixtures.DoesNotExist",
                    Role = "DomainLayer"
                }
            }
        };

        ArchitectureRoleIndex index = CreateIndex(classification);

        Assert.That(index.TryGetRole(typeof(DirectlyDerivedEntity), out _), Is.False);
        Assert.That(index.Conflicts, Is.Empty);
        Assert.That(index.MetadataFailures, Is.Empty);
    }

    [Test]
    public void TryGetRole_TwoInheritanceEntriesMatchOneType_FirstDeclaredWinsAndConflictRecorded()
    {
        var classification = new ArchitectureClassificationConfiguration
        {
            Inheritance =
            {
                new ArchitectureInheritanceClassificationMapping
                {
                    BaseType = "AttributeRoleExtractionTestFixtures.DomainEntityBase",
                    Role = "DomainLayer"
                },
                new ArchitectureInheritanceClassificationMapping
                {
                    BaseType = "AttributeRoleExtractionTestFixtures.IRepositoryMarker",
                    Role = "InfrastructureLayer"
                }
            }
        };

        ArchitectureRoleIndex index = CreateIndex(classification);

        Assert.That(index.TryGetRole(typeof(TypeMatchedByTwoInheritanceEntries), out ArchitectureTypeClassificationResult descriptor), Is.True);
        Assert.That(descriptor.Role, Is.EqualTo("DomainLayer"));
        Assert.That(
            index.Conflicts.Any(c =>
                c.Subject == "AttributeRoleExtractionTestFixtures.TypeMatchedByTwoInheritanceEntries"
                && c.Source == ArchitectureClassificationSource.Inheritance
                && c.WinningRole == "DomainLayer"
                && c.DiscardedRole == "InfrastructureLayer"),
            Is.True);
    }

    [Test]
    public void TryGetRole_InheritanceConstMetadata_ExtractsValue()
    {
        var classification = new ArchitectureClassificationConfiguration
        {
            Inheritance =
            {
                new ArchitectureInheritanceClassificationMapping
                {
                    BaseType = "AttributeRoleExtractionTestFixtures.DomainEntityBase",
                    Role = "DomainLayer",
                    Metadata = new Dictionary<string, object>
                    {
                        ["owner"] = "const:AttributeRoleExtractionTestFixtures.Constants.Owner"
                    }
                }
            }
        };

        ArchitectureRoleIndex index = CreateIndex(classification);

        Assert.That(index.TryGetRole(typeof(DirectlyDerivedEntity), out ArchitectureTypeClassificationResult descriptor), Is.True);
        Assert.That(descriptor.Metadata["owner"], Is.EqualTo("platform-team"));
    }

    [Test]
    public void TryGetRole_InheritanceMetadataConstructorForm_RejectedAsExtractionFailure()
    {
        // Defense in depth: the schema forbids constructor[]/property: on inheritance/namespace
        // entries, but a hand-constructed configuration (as here) can bypass that.
        var classification = new ArchitectureClassificationConfiguration
        {
            Inheritance =
            {
                new ArchitectureInheritanceClassificationMapping
                {
                    BaseType = "AttributeRoleExtractionTestFixtures.DomainEntityBase",
                    Role = "DomainLayer",
                    Metadata = new Dictionary<string, object> { ["domain"] = "constructor[0]" }
                }
            }
        };

        ArchitectureRoleIndex index = CreateIndex(classification);

        Assert.That(index.TryGetRole(typeof(DirectlyDerivedEntity), out ArchitectureTypeClassificationResult descriptor), Is.True);
        Assert.That(descriptor.Metadata.ContainsKey("domain"), Is.False);
        Assert.That(
            index.MetadataFailures.Any(f =>
                f.Subject == "AttributeRoleExtractionTestFixtures.DirectlyDerivedEntity" && f.MetadataKey == "domain"),
            Is.True);
    }

    [Test]
    public void TryGetRole_NamespaceGlobMatch_AssignsRoleWithNamespaceEvidence()
    {
        var classification = new ArchitectureClassificationConfiguration
        {
            Namespace =
            {
                new ArchitectureNamespaceClassificationMapping
                {
                    Namespace = "AttributeRoleExtractionTestFixtures.Domain",
                    Role = "DomainLayer"
                }
            }
        };

        ArchitectureRoleIndex index = CreateIndex(classification);

        Assert.That(
            index.TryGetRole(typeof(AttributeRoleExtractionTestFixtures.Domain.TypeInDomainNamespace), out ArchitectureTypeClassificationResult descriptor),
            Is.True);
        Assert.That(descriptor.Role, Is.EqualTo("DomainLayer"));
        Assert.That(descriptor.Source, Is.EqualTo(ArchitectureClassificationSource.Namespace));
        Assert.That(descriptor.Evidence, Is.EqualTo("AttributeRoleExtractionTestFixtures.Domain"));
    }

    [Test]
    public void TryGetRole_NamespaceGlobMatch_MatchesNestedNamespace()
    {
        var classification = new ArchitectureClassificationConfiguration
        {
            Namespace =
            {
                new ArchitectureNamespaceClassificationMapping
                {
                    Namespace = "AttributeRoleExtractionTestFixtures.Domain",
                    Role = "DomainLayer"
                }
            }
        };

        ArchitectureRoleIndex index = CreateIndex(classification);

        Assert.That(
            index.TryGetRole(typeof(AttributeRoleExtractionTestFixtures.Domain.Nested.TypeInNestedDomainNamespace), out ArchitectureTypeClassificationResult descriptor),
            Is.True);
        Assert.That(descriptor.Role, Is.EqualTo("DomainLayer"));
    }

    [Test]
    public void TryGetRole_NamespaceSuffixOnlyEntry_MatchesSuffix()
    {
        var classification = new ArchitectureClassificationConfiguration
        {
            Namespace =
            {
                new ArchitectureNamespaceClassificationMapping
                {
                    NamespaceSuffix = "Contracts",
                    Role = "PublicContract"
                }
            }
        };

        ArchitectureRoleIndex index = CreateIndex(classification);

        Assert.That(
            index.TryGetRole(typeof(AttributeRoleExtractionTestFixtures.Feature.Contracts.TypeInContractsSuffixNamespace), out ArchitectureTypeClassificationResult descriptor),
            Is.True);
        Assert.That(descriptor.Role, Is.EqualTo("PublicContract"));
        Assert.That(descriptor.Evidence, Is.EqualTo("*.Contracts"));
    }

    [Test]
    public void TryGetRole_NamespaceEntry_UnmatchedNamespace_ReturnsFalse()
    {
        var classification = new ArchitectureClassificationConfiguration
        {
            Namespace =
            {
                new ArchitectureNamespaceClassificationMapping
                {
                    Namespace = "AttributeRoleExtractionTestFixtures.Domain",
                    Role = "DomainLayer"
                }
            }
        };

        ArchitectureRoleIndex index = CreateIndex(classification);

        Assert.That(index.TryGetRole(typeof(TypeInDefaultNamespace), out _), Is.False);
    }

    [Test]
    public void TryGetRole_TwoNamespaceEntriesMatchOneNamespace_FirstDeclaredWinsAndConflictRecorded()
    {
        var classification = new ArchitectureClassificationConfiguration
        {
            Namespace =
            {
                new ArchitectureNamespaceClassificationMapping
                {
                    Namespace = "AttributeRoleExtractionTestFixtures.Domain",
                    Role = "DomainLayer"
                },
                new ArchitectureNamespaceClassificationMapping
                {
                    NamespaceSuffix = "Domain",
                    Role = "OtherLayer"
                }
            }
        };

        ArchitectureRoleIndex index = CreateIndex(classification);

        Assert.That(
            index.TryGetRole(typeof(AttributeRoleExtractionTestFixtures.Domain.TypeInDomainNamespace), out ArchitectureTypeClassificationResult descriptor),
            Is.True);
        Assert.That(descriptor.Role, Is.EqualTo("DomainLayer"));
        Assert.That(
            index.Conflicts.Any(c =>
                c.Source == ArchitectureClassificationSource.Namespace
                && c.WinningRole == "DomainLayer"
                && c.DiscardedRole == "OtherLayer"),
            Is.True);
    }

    [Test]
    public void TryGetRole_InheritanceBeatsNamespace_WhenBothMatch()
    {
        var classification = new ArchitectureClassificationConfiguration
        {
            Inheritance =
            {
                new ArchitectureInheritanceClassificationMapping
                {
                    BaseType = "AttributeRoleExtractionTestFixtures.DomainEntityBase",
                    Role = "DomainLayer"
                }
            },
            Namespace =
            {
                new ArchitectureNamespaceClassificationMapping
                {
                    Namespace = "AttributeRoleExtractionTestFixtures.PrecedenceCases",
                    Role = "OtherLayer"
                }
            }
        };

        ArchitectureRoleIndex index = CreateIndex(classification);

        Assert.That(
            index.TryGetRole(
                typeof(AttributeRoleExtractionTestFixtures.PrecedenceCases.TypeMatchedByInheritanceAndNamespace),
                out ArchitectureTypeClassificationResult descriptor),
            Is.True);
        Assert.That(descriptor.Role, Is.EqualTo("DomainLayer"));
        Assert.That(descriptor.Source, Is.EqualTo(ArchitectureClassificationSource.Inheritance));
    }

    [Test]
    public void TryGetRole_NamespaceDisabledByPrecedence_DoesNotMatch()
    {
        var classification = new ArchitectureClassificationConfiguration
        {
            Precedence = new List<string> { "inheritance" },
            Namespace =
            {
                new ArchitectureNamespaceClassificationMapping
                {
                    Namespace = "AttributeRoleExtractionTestFixtures.Domain",
                    Role = "DomainLayer"
                }
            }
        };

        ArchitectureRoleIndex index = CreateIndex(classification);

        Assert.That(
            index.TryGetRole(typeof(AttributeRoleExtractionTestFixtures.Domain.TypeInDomainNamespace), out _),
            Is.False);
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
