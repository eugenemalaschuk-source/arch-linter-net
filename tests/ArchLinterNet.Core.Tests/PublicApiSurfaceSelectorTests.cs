using System.Reflection;
using System.Reflection.Emit;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Scanning;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;
using Fixtures = PublicApiSurfaceSelectorTestFixtures;

namespace ArchLinterNet.Core.Tests;

// Covers issue #525's surface_selector: the 12 required validation scenarios plus semantic-role
// preservation. Mirrors the session-level test pattern already used by PublicApiSurfaceContractTests
// and PublicApiSnapshotContractTests — construct a document, run it through ArchitectureContractRunner,
// and drive the same Core seam both CLI and ArchLinterNet.Testing sit on.
[TestFixture]
public sealed class PublicApiSurfaceSelectorTests
{
    private static string AssemblyName => typeof(PublicApiSurfaceSelectorTests).Assembly.GetName().Name!;

    private static ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(PublicApiSurfaceSelectorTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static ArchitectureClassificationConfiguration RoleClassification()
    {
        return new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = typeof(Fixtures.ValueObjectRoleAttribute).FullName!,
                    Role = "ValueObject",
                },
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = typeof(Fixtures.ApiContractRoleAttribute).FullName!,
                    Role = "ApiContract",
                },
            },
        };
    }

    private static ArchitectureContractDocument CreateDocument(
        ArchitecturePublicApiSurfaceContract contract, bool withRoleClassification = false)
    {
        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { AssemblyName } },
            Classification = withRoleClassification ? RoleClassification() : new ArchitectureClassificationConfiguration(),
            Contracts = new ArchitectureContractGroups
            {
                StrictPublicApiSurface = new List<ArchitecturePublicApiSurfaceContract> { contract },
            },
        };
    }

    private static ArchitectureContractRunner CreateRunner(
        ArchitecturePublicApiSurfaceContract contract, bool withRoleClassification = false)
    {
        return new ArchitectureContractRunner(CreateContext(), CreateDocument(contract, withRoleClassification));
    }

    private static HashSet<string> ViolationSourceTypes(IEnumerable<ArchitectureViolation> violations) =>
        violations.Select(v => v.SourceType).ToHashSet(StringComparer.Ordinal);

    // Scenario 1 + 2: an orthogonal attribute marker selects a small surface; selected types retain
    // a non-ApiContract role, and selection itself needs no role mapping at all (RoleClassification
    // is intentionally NOT wired into this document).
    [Test]
    public void HasAttributeSelector_SelectsOnlyMarkedType_NoRoleMappingRequired()
    {
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "surface",
            Assemblies = new List<string> { AssemblyName },
            SurfaceSelector = new ArchitecturePublicApiSurfaceSelector
            {
                HasAttribute = typeof(Fixtures.PublicApiContractAttribute).FullName!,
            },
        };
        var runner = CreateRunner(contract);

        HashSet<string> governed = ViolationSourceTypes(runner.Session.CheckPublicApiSurfaceContract(contract));

        Assert.Multiple(() =>
        {
            Assert.That(governed, Does.Contain(typeof(Fixtures.SelectedByAttribute).FullName));
            Assert.That(governed, Does.Not.Contain(typeof(Fixtures.IncidentalType).FullName));
        });
    }

    // Scenario 3: the semantic-role selector path, used with no structural marker at all.
    [Test]
    public void RoleSelector_SelectsTypeByExistingSemanticRole()
    {
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "surface",
            Assemblies = new List<string> { AssemblyName },
            SurfaceSelector = new ArchitecturePublicApiSurfaceSelector { Role = "ApiContract" },
        };
        var runner = CreateRunner(contract, withRoleClassification: true);

        HashSet<string> governed = ViolationSourceTypes(runner.Session.CheckPublicApiSurfaceContract(contract));

        Assert.Multiple(() =>
        {
            Assert.That(governed, Does.Contain(typeof(Fixtures.SelectedByRole).FullName));
            Assert.That(governed, Does.Not.Contain(typeof(Fixtures.IncidentalType).FullName));
        });
    }

    // Scenario 4: at least one non-attribute structural matcher end to end (base_type here;
    // implements_interface and namespace are covered by the two tests immediately below).
    [Test]
    public void BaseTypeSelector_SelectsDerivedType()
    {
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "surface",
            Assemblies = new List<string> { AssemblyName },
            SurfaceSelector = new ArchitecturePublicApiSurfaceSelector { BaseType = typeof(Fixtures.ApiBase).FullName! },
        };
        var runner = CreateRunner(contract);

        HashSet<string> governed = ViolationSourceTypes(runner.Session.CheckPublicApiSurfaceContract(contract));

        Assert.That(governed, Does.Contain(typeof(Fixtures.SelectedByBaseType).FullName));
        Assert.That(governed, Does.Not.Contain(typeof(Fixtures.IncidentalType).FullName));
    }

    [Test]
    public void ImplementsInterfaceSelector_SelectsImplementingType()
    {
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "surface",
            Assemblies = new List<string> { AssemblyName },
            SurfaceSelector = new ArchitecturePublicApiSurfaceSelector
            {
                ImplementsInterface = typeof(Fixtures.IApiMarker).FullName!,
            },
        };
        var runner = CreateRunner(contract);

        HashSet<string> governed = ViolationSourceTypes(runner.Session.CheckPublicApiSurfaceContract(contract));

        Assert.That(governed, Does.Contain(typeof(Fixtures.SelectedByInterface).FullName));
    }

    [Test]
    public void NamespaceSelector_SelectsTypeUnderMatchingNamespace()
    {
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "surface",
            Assemblies = new List<string> { AssemblyName },
            SurfaceSelector = new ArchitecturePublicApiSurfaceSelector
            {
                Namespace = "PublicApiSurfaceSelectorTestFixtures.PublicSurface",
            },
        };
        var runner = CreateRunner(contract);

        HashSet<string> governed = ViolationSourceTypes(runner.Session.CheckPublicApiSurfaceContract(contract));

        Assert.That(governed, Does.Contain(typeof(Fixtures.PublicSurface.SelectedByNamespace).FullName));
        Assert.That(governed, Does.Not.Contain(typeof(Fixtures.IncidentalType).FullName));
    }

    // Scenario 5: exact API delta still works within the selected surface.
    [Test]
    public void ExactMode_WithSelector_ReportsRemovalWithinSelectedSurface()
    {
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "surface",
            Assemblies = new List<string> { AssemblyName },
            SurfaceSelector = new ArchitecturePublicApiSurfaceSelector
            {
                HasAttribute = typeof(Fixtures.PublicApiContractAttribute).FullName!,
            },
            ApiSnapshot = "surface.txt",
            ApiComparison = PublicApiComparisonModes.Exact,
            ResolvedSnapshotEntries = new[]
            {
                new PublicApiSnapshotEntry(
                    AssemblyName,
                    $"method {typeof(Fixtures.SelectedByAttribute).FullName}.ThisMemberWasRemoved(): System.Void"),
            },
        };
        var runner = CreateRunner(contract);

        List<PublicApiSurfacePayload?> payloads = runner.Session.CheckPublicApiSurfaceContract(contract)
            .Select(v => v.Payload as PublicApiSurfacePayload)
            .ToList();

        Assert.That(payloads.Any(p => p?.ApiDeltaKind == "removed"
            && p.UndeclaredApiSignature!.Contains("ThisMemberWasRemoved", StringComparison.Ordinal)), Is.True);
    }

    // Scenario 6: adding/removing selector evidence deterministically moves a type into/out of the
    // governed (reviewed) surface — proven as the selector's fundamental membership boundary, which
    // is what the capture/diff/update lifecycle reports as an addition/removal when evidence changes.
    [Test]
    public void SelectorEvidenceBoundary_TypeWithoutMarker_IsExcluded_TypeWithMarker_IsIncluded()
    {
        var withMarker = new ArchitecturePublicApiSurfaceContract
        {
            Name = "surface",
            Assemblies = new List<string> { AssemblyName },
            SurfaceSelector = new ArchitecturePublicApiSurfaceSelector
            {
                HasAttribute = typeof(Fixtures.PublicApiContractAttribute).FullName!,
            },
        };
        var withoutMarker = new ArchitecturePublicApiSurfaceContract
        {
            Name = "surface",
            Assemblies = new List<string> { AssemblyName },
            SurfaceSelector = new ArchitecturePublicApiSurfaceSelector { HasAttribute = "No.Such.Attribute.Anywhere" },
        };

        HashSet<string> governedWithMarker =
            ViolationSourceTypes(CreateRunner(withMarker).Session.CheckPublicApiSurfaceContract(withMarker));

        Assert.That(governedWithMarker, Does.Contain(typeof(Fixtures.SelectedByAttribute).FullName));

        // withoutMarker's selector matches zero types, which is its own fail-closed violation
        // (scenario 7) rather than a normal empty surface — asserted separately below.
        List<ArchitectureViolation> zeroMatch =
            CreateRunner(withoutMarker).Session.CheckPublicApiSurfaceContract(withoutMarker);
        Assert.That(zeroMatch.Select(v => (v.Payload as PublicApiSurfacePayload)?.ApiDeltaKind),
            Has.Some.EqualTo("selector-zero-match"));
    }

    // Scenario 7: a required selector matching nothing fails closed instead of passing green.
    [Test]
    public void Selector_MatchingNothing_FailsClosedWithZeroMatchViolation()
    {
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "surface",
            Assemblies = new List<string> { AssemblyName },
            SurfaceSelector = new ArchitecturePublicApiSurfaceSelector { HasAttribute = "No.Such.Attribute.Anywhere" },
        };
        var runner = CreateRunner(contract);

        List<ArchitectureViolation> violations = runner.Session.CheckPublicApiSurfaceContract(contract);

        Assert.That(violations, Has.Count.EqualTo(1));
        var payload = violations[0].Payload as PublicApiSurfacePayload;
        Assert.That(payload?.ApiDeltaKind, Is.EqualTo("selector-zero-match"));
    }

    // Scenario 8 + 9: a selected member referencing an unselected first-party type fails closed;
    // BCL-typed members of a selected type (e.g. int) never require selection evidence.
    [Test]
    public void SelectedMember_ReferencingUnselectedFirstPartyType_FailsClosed()
    {
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "surface",
            Assemblies = new List<string> { AssemblyName },
            SurfaceSelector = new ArchitecturePublicApiSurfaceSelector
            {
                HasAttribute = typeof(Fixtures.PublicApiContractAttribute).FullName!,
            },
        };
        var runner = CreateRunner(contract);

        List<ArchitectureViolation> violations = runner.Session.CheckPublicApiSurfaceContract(contract);

        ArchitectureViolation? escape = violations.FirstOrDefault(v =>
            v.SourceType == typeof(Fixtures.SelectedWithEscapingDependency).FullName
            && (v.Payload as PublicApiSurfacePayload)?.UnselectedFirstPartyDependency
                == typeof(Fixtures.IncidentalType).FullName);
        Assert.That(escape, Is.Not.Null);

        // SelectedByAttribute.Value: System.Int32 must not itself trigger an escape violation — int
        // is BCL, not a first-party type declared in the contract's own assemblies.
        Assert.That(violations.Any(v =>
            v.SourceType == typeof(Fixtures.SelectedByAttribute).FullName
            && (v.Payload as PublicApiSurfacePayload)?.UnselectedFirstPartyDependency != null), Is.False);
    }

    // Regression (PR #529 review): the escape check's first-party/selected sets used to be keyed on
    // bare type full name. In a multi-assembly contract, a same-named type SELECTED in one assembly
    // could mask an unselected same-named type in a different assembly, turning a real escape into a
    // false green. The check must be keyed on (assembly, type name), not name alone.
    [Test]
    public void SelectedMember_ReferencingUnselectedFirstPartyType_IsNotMaskedBySameNamedSelectedTypeInAnotherAssembly()
    {
        Assembly decoyAssembly = BuildDecoyAssemblyWithSameNamedSelectedType();
        string decoyAssemblyName = decoyAssembly.GetName().Name!;

        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "surface",
            Assemblies = new List<string> { AssemblyName, decoyAssemblyName },
            SurfaceSelector = new ArchitecturePublicApiSurfaceSelector
            {
                HasAttribute = typeof(Fixtures.PublicApiContractAttribute).FullName!,
            },
        };
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { AssemblyName, decoyAssemblyName },
            },
            Classification = new ArchitectureClassificationConfiguration(),
            Contracts = new ArchitectureContractGroups
            {
                StrictPublicApiSurface = new List<ArchitecturePublicApiSurfaceContract> { contract },
            },
        };
        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(PublicApiSurfaceSelectorTests).Assembly, decoyAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());
        var runner = new ArchitectureContractRunner(context, document);

        List<ArchitectureViolation> violations = runner.Session.CheckPublicApiSurfaceContract(contract);

        bool escapeStillReported = violations.Any(v =>
            v.SourceType == typeof(Fixtures.SelectedWithEscapingDependency).FullName
            && (v.Payload as PublicApiSurfacePayload)?.UnselectedFirstPartyDependency
                == typeof(Fixtures.IncidentalType).FullName);
        Assert.That(escapeStillReported, Is.True,
            "a same-named selected type in a different assembly must not mask an unselected first-party escape");
    }

    // Builds a second, independent assembly declaring a public type under the identical full name as
    // Fixtures.IncidentalType, itself carrying the selector's marker attribute (so it IS selected in
    // its own assembly) — the exact shape needed to reproduce the name-only masking bug.
    private static AssemblyBuilder BuildDecoyAssemblyWithSameNamedSelectedType()
    {
        AssemblyName assemblyName = new($"PublicApiSurfaceSelectorDecoy-{Guid.NewGuid():N}");
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
        TypeBuilder typeBuilder = moduleBuilder.DefineType(
            typeof(Fixtures.IncidentalType).FullName!, TypeAttributes.Public | TypeAttributes.Sealed);

        ConstructorInfo attributeCtor = typeof(Fixtures.PublicApiContractAttribute).GetConstructor(Type.EmptyTypes)!;
        typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(attributeCtor, Array.Empty<object>()));

        typeBuilder.CreateType();
        return assemblyBuilder;
    }

    // Regression (PR #529 review): a first-party type referenced only through a generic method
    // constraint (`where T : HiddenExported`), never through an ordinary parameter/return/field/
    // property/event type, must still fail closed — the exact public API grammar already treats
    // generic constraints as part of a member's signature (ArchitecturePublicApiSignatureDetails), so
    // the escape check must walk them too.
    [Test]
    public void SelectedMember_GenericMethodConstraintReferencingUnselectedFirstPartyType_FailsClosed()
    {
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "surface",
            Assemblies = new List<string> { AssemblyName },
            SurfaceSelector = new ArchitecturePublicApiSurfaceSelector
            {
                HasAttribute = typeof(Fixtures.PublicApiContractAttribute).FullName!,
            },
        };
        var runner = CreateRunner(contract);

        List<ArchitectureViolation> violations = runner.Session.CheckPublicApiSurfaceContract(contract);

        ArchitectureViolation? escape = violations.FirstOrDefault(v =>
            v.SourceType == typeof(Fixtures.SelectedWithGenericConstraintEscape).FullName
            && (v.Payload as PublicApiSurfacePayload)?.UnselectedFirstPartyDependency
                == typeof(Fixtures.UnselectedConstraintTarget).FullName);
        Assert.That(escape, Is.Not.Null);
    }

    // Scenario 10: backward compatibility — no selector governs everything, exactly as before #525.
    [Test]
    public void NoSelector_GovernsEveryExportedType_UnchangedFromPriorBehavior()
    {
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "surface",
            Assemblies = new List<string> { AssemblyName },
        };
        var runner = CreateRunner(contract);

        HashSet<string> governed = ViolationSourceTypes(runner.Session.CheckPublicApiSurfaceContract(contract));

        Assert.Multiple(() =>
        {
            Assert.That(governed, Does.Contain(typeof(Fixtures.IncidentalType).FullName));
            Assert.That(governed, Does.Contain(typeof(Fixtures.SelectedByAttribute).FullName));
        });
    }

    // Scenario 11: capture and strict validation resolve the identical effective selected surface —
    // the structural proof that CLI (capture/diff/update/migrate) and ArchLinterNet.Testing (which
    // drives the same CheckPublicApiSurfaceContract path as strict `validate`) can never diverge,
    // because both bottom out in the same two Core call sites filtered by the same predicate.
    [Test]
    public void Selector_CaptureAndStrictValidation_ResolveSameEffectiveSurface()
    {
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "surface",
            Assemblies = new List<string> { AssemblyName },
            SurfaceSelector = new ArchitecturePublicApiSurfaceSelector
            {
                HasAttribute = typeof(Fixtures.PublicApiContractAttribute).FullName!,
            },
        };
        var runner = CreateRunner(contract);

        IReadOnlyList<PublicApiSnapshotEntry> captured = runner.Session.CapturePublicApiSurface(
            contract, out IReadOnlyList<string> missing, out IReadOnlyList<ArchitectureViolation> safety);
        Assert.That(missing, Is.Empty);

        // This selector also matches the escape-scenario fixtures elsewhere in this file (they share
        // this test assembly and the same marker attribute), so capture's own selector-safety check
        // is expected to surface those same escapes here — every safety violation's SourceType must
        // still be part of the captured (governed) type set, never a type outside it.
        HashSet<string> capturedTypeNames = captured
            .Select(entry => PublicApiSignatureIdentity.DeclaringTypeName(entry.Signature))
            .ToHashSet(StringComparer.Ordinal);

        // Empty declared_api: every governed entry becomes an undeclared-addition violation, whose
        // SourceType set is therefore exactly the strict-validation side's selected type universe.
        HashSet<string> validatedTypeNames =
            ViolationSourceTypes(runner.Session.CheckPublicApiSurfaceContract(contract));

        Assert.Multiple(() =>
        {
            Assert.That(validatedTypeNames, Is.EqualTo(capturedTypeNames));
            Assert.That(safety.Select(v => v.SourceType), Is.SubsetOf(capturedTypeNames));
        });
    }

    // Scenario 12: a large modular consumer replaces a whole-assembly snapshot with a materially
    // smaller intentional one. This test assembly already carries dozens of incidental public fixture
    // types across many other test files, standing in for a real modular assembly's incidental
    // CLR-public implementation/domain/configuration surface.
    [Test]
    public void LargeAssembly_SelectorRestrictedCapture_IsMaterialySmallerThanWholeAssemblyCapture()
    {
        var wholeAssembly = new ArchitecturePublicApiSurfaceContract
        {
            Name = "surface",
            Assemblies = new List<string> { AssemblyName },
        };
        var selected = new ArchitecturePublicApiSurfaceContract
        {
            Name = "surface",
            Assemblies = new List<string> { AssemblyName },
            SurfaceSelector = new ArchitecturePublicApiSurfaceSelector
            {
                HasAttribute = typeof(Fixtures.PublicApiContractAttribute).FullName!,
            },
        };

        IReadOnlyList<PublicApiSnapshotEntry> wholeCapture =
            CreateRunner(wholeAssembly).Session.CapturePublicApiSurface(wholeAssembly, out _, out _);
        IReadOnlyList<PublicApiSnapshotEntry> selectedCapture =
            CreateRunner(selected).Session.CapturePublicApiSurface(selected, out _, out _);

        Assert.That(selectedCapture.Count, Is.LessThan(wholeCapture.Count / 10),
            "the selected snapshot should be an order of magnitude smaller, not merely smaller");
    }

    // Semantic role preservation (issue #525 core invariant): selecting a type via an orthogonal
    // has_attribute marker must never change, or require changing, its existing winning role.
    [Test]
    public void SelectingViaHasAttribute_DoesNotChangeExistingSemanticRole()
    {
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "surface",
            Assemblies = new List<string> { AssemblyName },
            SurfaceSelector = new ArchitecturePublicApiSurfaceSelector
            {
                HasAttribute = typeof(Fixtures.PublicApiContractAttribute).FullName!,
            },
        };
        var runner = CreateRunner(contract, withRoleClassification: true);

        HashSet<string> governed = ViolationSourceTypes(runner.Session.CheckPublicApiSurfaceContract(contract));
        Assert.That(governed, Does.Contain(typeof(Fixtures.SelectedByAttribute).FullName));

        bool found = runner.Session.RoleIndex.TryGetRole(
            typeof(Fixtures.SelectedByAttribute), out ArchitectureTypeClassificationResult descriptor);
        Assert.That(found, Is.True);
        Assert.That(descriptor.Role, Is.EqualTo("ValueObject"));
    }

    [Test]
    public void RoleSelector_NeedsNoSeparateAttributeMapping_TypeCarriesOnlyTheRoleAttribute()
    {
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "surface",
            Assemblies = new List<string> { AssemblyName },
            SurfaceSelector = new ArchitecturePublicApiSurfaceSelector { Role = "ApiContract" },
        };
        var runner = CreateRunner(contract, withRoleClassification: true);

        bool found = runner.Session.RoleIndex.TryGetRole(
            typeof(Fixtures.SelectedByRole), out ArchitectureTypeClassificationResult descriptor);
        Assert.That(found, Is.True);
        Assert.That(descriptor.Role, Is.EqualTo("ApiContract"));

        HashSet<string> governed = ViolationSourceTypes(runner.Session.CheckPublicApiSurfaceContract(contract));
        Assert.That(governed, Does.Contain(typeof(Fixtures.SelectedByRole).FullName));
    }
}
