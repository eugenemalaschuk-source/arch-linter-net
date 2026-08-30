using System.Reflection;
using System.Reflection.Emit;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ContractSurfaceExposureIndexTests
{
    [Test]
    public void Scan_RecursivelyRecordsVisibleShapesRelationshipsAndMetadataSites()
    {
        ArchitectureContractSurfaceExposureResult result =
            ArchitectureContractSurfaceExposureScanner.Scan(typeof(ContractRoot<>));

        Assert.That(result.IsComplete, Is.True);
        Assert.That(result.IncompleteEvidence, Is.Empty);
        Assert.That(Targets(result), Does.Contain(Target(typeof(Customer))));
        Assert.That(Targets(result), Does.Contain(Target(typeof(BaseContract))));
        Assert.That(Targets(result), Does.Contain(Target(typeof(IContractInterface))));
        Assert.That(Targets(result), Does.Contain(Target(typeof(Envelope<>))));
        Assert.That(Targets(result), Does.Contain(Target(typeof(ExposureMetadataAttribute))));
        Assert.That(Targets(result), Does.Contain(Target(typeof(ExposureKind))));
        Assert.That(Targets(result), Does.Contain(Target(typeof(ContractRoot<>.NestedContract))));

        Assert.Multiple(() =>
        {
            Assert.That(HasPath(result, typeof(Customer), "array_element"), Is.True);
            Assert.That(HasPath(result, typeof(NullableContract), "nullable_underlying"), Is.True);
            Assert.That(HasPath(result, typeof(Customer), "tuple_element"), Is.True);
            Assert.That(HasPath(result, typeof(Customer), "attribute_argument"), Is.True);
            Assert.That(HasPath(result, typeof(BaseContract), "constraint"), Is.True);
            Assert.That(HasPath(result, typeof(BaseContract), "base_type"), Is.True);
            Assert.That(HasPath(result, typeof(IContractInterface), "interface"), Is.True);
            Assert.That(HasPath(result, typeof(ContractRoot<>.NestedContract), "nested_type"), Is.True);
            Assert.That(result.Exposures.Any(exposure =>
                Target(typeof(Customer)).Equals(exposure.ReferencedType)
                && exposure.Path.Segments.Any(segment => segment.Kind == "parameter")), Is.True);
            Assert.That(result.Exposures.Any(exposure =>
                Target(typeof(Customer)).Equals(exposure.ReferencedType)
                && exposure.Path.Segments.Any(segment => segment.Kind == "return")), Is.True);
            Assert.That(result.Exposures.Any(exposure =>
                Target(typeof(Customer)).Equals(exposure.ReferencedType)
                && exposure.Path.Segments.Any(segment => segment.Kind == "generic_parameter")), Is.True);
            Assert.That(result.Exposures.Any(exposure =>
                Target(typeof(Customer)).Equals(exposure.ReferencedType)
                && exposure.Path.Segments.Any(segment => segment.Kind == "delegate_invoke")
                && exposure.Path.Segments.Any(segment => segment.Kind == "return")), Is.True);
            Assert.That(result.Exposures.Any(exposure =>
                Target(typeof(Customer)).Equals(exposure.ReferencedType)
                && exposure.Path.Segments.Any(segment => segment.Kind == "member" && segment.Value.Contains("AsyncMethod", StringComparison.Ordinal))
                && exposure.Path.Segments.Count(segment => segment.Kind == "generic_argument") >= 2), Is.True);
        });

        // The int and string values in ExposureMetadataAttribute are values, not type references.
        Assert.That(result.Exposures.Any(exposure =>
            exposure.Path.Segments.Any(segment => segment.Kind == "attribute_argument")
            && exposure.ReferencedType.FullTypeName is "System.Int32" or "System.String"), Is.False);
    }

    [Test]
    public void Scan_CyclicGenericConstraintTerminatesWithBoundedEvidence()
    {
        ArchitectureContractSurfaceExposureResult result =
            ArchitectureContractSurfaceExposureScanner.Scan(typeof(CyclicContract<>));

        Assert.That(result.IsComplete, Is.True);
        Assert.That(result.Exposures.Count, Is.GreaterThan(0));
        Assert.That(result.Exposures.Count, Is.LessThan(100));
        Assert.That(result.Exposures.Select(exposure => exposure.Path.CanonicalKey).Distinct().Count(),
            Is.EqualTo(result.Exposures.Count));
    }

    [Test]
    public void Index_CachesRootsWithinSessionButNotAcrossSessions()
    {
        ArchitectureContractDocument document = new() { Version = 1, Name = "exposure-tests" };
        ArchitectureAnalysisContext context = new(
            "/tmp", new[] { typeof(ContractRoot<>).Assembly }, Array.Empty<string>(), Array.Empty<string>());
        ArchitectureAnalysisSession session = new(context, document, null, false, null);

        ArchitectureContractSurfaceExposureResult first = session.GetContractSurfaceExposure(typeof(ContractRoot<>));
        ArchitectureContractSurfaceExposureResult second = session.GetContractSurfaceExposure(new[] { typeof(ContractRoot<>) });

        Assert.Multiple(() =>
        {
            Assert.That(second.Exposures, Is.EqualTo(first.Exposures));
            Assert.That(session.ContractSurfaceExposureMaterializationCount, Is.EqualTo(1));
        });

        session.GetContractSurfaceExposure(new[] { typeof(ContractRoot<>), typeof(ContractRoot<>.NestedContract) });
        Assert.That(session.ContractSurfaceExposureMaterializationCount, Is.EqualTo(2));

        ArchitectureAnalysisSession separateSession = new(context, document, null, false, null);
        separateSession.GetContractSurfaceExposure(typeof(ContractRoot<>));
        Assert.That(separateSession.ContractSurfaceExposureMaterializationCount, Is.EqualTo(1));
    }

    [Test]
    public void Index_KeysEvidenceByRequestedVisibleSurfaceShape()
    {
        ArchitectureContractDocument document = new() { Version = 1, Name = "exposure-tests" };
        ArchitectureAnalysisContext context = new(
            "/tmp", new[] { typeof(SurfaceShapeRoot).Assembly }, Array.Empty<string>(), Array.Empty<string>());
        ArchitectureAnalysisSession session = new(context, document, null, false, null);
        ArchitectureContractSurfaceShape internalVisible = new(
            ArchitectureContractSurfaceVisibility.Public | ArchitectureContractSurfaceVisibility.Internal);

        ArchitectureContractSurfaceExposureResult exported =
            session.GetContractSurfaceExposure(typeof(SurfaceShapeRoot));
        ArchitectureContractSurfaceExposureResult configured =
            session.GetContractSurfaceExposure(typeof(SurfaceShapeRoot), internalVisible);
        ArchitectureContractSurfaceExposureResult repeated =
            session.GetContractSurfaceExposure(typeof(SurfaceShapeRoot), internalVisible);

        Assert.Multiple(() =>
        {
            Assert.That(Targets(exported), Does.Not.Contain(Target(typeof(InternalSurfacePayload))));
            Assert.That(Targets(configured), Does.Contain(Target(typeof(InternalSurfacePayload))));
            Assert.That(repeated.Exposures, Is.EqualTo(configured.Exposures));
            Assert.That(session.ContractSurfaceExposureMaterializationCount, Is.EqualTo(2));
        });
    }

    [Test]
    public void Scan_SameFullNameFromAssembliesWithSameSimpleNameRemainsDistinct()
    {
        (Type firstRoot, Type firstTarget) = BuildSyntheticSurface(new Version(1, 0, 0, 0));
        (Type secondRoot, Type secondTarget) = BuildSyntheticSurface(new Version(2, 0, 0, 0));

        ArchitectureContractDocument document = new() { Version = 1, Name = "exposure-tests" };
        ArchitectureAnalysisContext context = new(
            "/tmp", new[] { firstRoot.Assembly, secondRoot.Assembly }, Array.Empty<string>(), Array.Empty<string>());
        ArchitectureAnalysisSession session = new(context, document, null, false, null);
        ArchitectureContractSurfaceExposureResult result =
            session.GetContractSurfaceExposure(new[] { firstRoot, secondRoot });
        ArchitectureContractExposureTarget firstIdentity = Target(firstTarget);
        ArchitectureContractExposureTarget secondIdentity = Target(secondTarget);
        HashSet<ArchitectureContractExposureTarget> duplicateTargets = result.Exposures
            .Where(exposure => exposure.ReferencedType.FullTypeName == firstTarget.FullName)
            .Select(exposure => exposure.ReferencedType)
            .ToHashSet();

        Assert.Multiple(() =>
        {
            Assert.That(firstIdentity.FullTypeName, Is.EqualTo(secondIdentity.FullTypeName));
            Assert.That(firstTarget.Assembly.GetName().Name, Is.EqualTo(secondTarget.Assembly.GetName().Name));
            Assert.That(firstIdentity.AssemblyName, Is.Not.EqualTo(secondIdentity.AssemblyName));
            Assert.That(duplicateTargets, Is.EquivalentTo(new[] { firstIdentity, secondIdentity }));
        });
    }

    [Test]
    public void Scan_UnloadableVisibleSignaturePublishesIncompleteEvidence()
    {
        using UnloadableFieldFixture fixture = UnloadableFieldFixture.Create();

        ArchitectureContractSurfaceExposureResult result =
            ArchitectureContractSurfaceExposureScanner.Scan(fixture.SourceType);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsComplete, Is.False);
            Assert.That(result.IncompleteEvidence.Any(evidence =>
                evidence.Reason == "field-type-unavailable"
                && evidence.Path.Segments.Any(segment => segment.Kind == "field_type")), Is.True);
        });
    }

    [Test]
    public void Scan_EmptyEnumArrayAttributeArgumentRecordsDeclaredEnumType()
    {
        ArchitectureContractSurfaceExposureResult result =
            ArchitectureContractSurfaceExposureScanner.Scan(typeof(EmptyEnumArrayAttributeRoot));

        Assert.That(result.Exposures.Any(exposure =>
            Target(typeof(ExposureKind)).Equals(exposure.ReferencedType)
            && exposure.Path.Segments.Any(segment => segment.Kind == "attribute_argument")), Is.True);
    }

    [Test]
    public void Scan_ReorderedAllowMultipleAttributesUseStableOccurrencePaths()
    {
        ArchitectureContractSurfaceExposureResult forward =
            ArchitectureContractSurfaceExposureScanner.Scan(typeof(ForwardRepeatedAttributes));
        ArchitectureContractSurfaceExposureResult reverse =
            ArchitectureContractSurfaceExposureScanner.Scan(typeof(ReverseRepeatedAttributes));

        Assert.Multiple(() =>
        {
            Assert.That(AttributeOccurrence(forward, typeof(Customer)),
                Is.EqualTo(AttributeOccurrence(reverse, typeof(Customer))));
            Assert.That(AttributeOccurrence(forward, typeof(AlternateCustomer)),
                Is.EqualTo(AttributeOccurrence(reverse, typeof(AlternateCustomer))));
        });
    }

    [Test]
    public void Scan_NonSpecialAccessorNamedMethodRecordsItsSignatureExposure()
    {
        ArchitectureContractSurfaceExposureResult result =
            ArchitectureContractSurfaceExposureScanner.Scan(typeof(AccessorNamedMethodRoot));

        Assert.That(result.Exposures.Any(exposure =>
            Target(typeof(Customer)).Equals(exposure.ReferencedType)
            && exposure.Path.Segments.Any(segment =>
                segment.Kind == "member" && segment.Value.Contains("get_Current", StringComparison.Ordinal))), Is.True);
    }

    [Test]
    public void Scan_SelectedOuterDoesNotTraverseUnselectedNestedMembers()
    {
        ArchitectureContractSurfaceExposureResult result =
            ArchitectureContractSurfaceExposureScanner.Scan(typeof(SelectedOuterRoot));

        Assert.Multiple(() =>
        {
            Assert.That(Targets(result), Does.Contain(Target(typeof(SelectedOuterRoot.UnselectedNested))));
            Assert.That(Targets(result), Does.Not.Contain(Target(typeof(NestedOnlyPayload))));
        });
    }

    [Test]
    public void Scan_EventVisibilityMatchesPublicApiAddAccessorSemantics()
    {
        (Type excludedRoot, Type excludedTarget) = BuildEventSurface(MethodAttributes.Private, MethodAttributes.Public);
        (Type includedRoot, Type includedTarget) = BuildEventSurface(MethodAttributes.Public, MethodAttributes.Private);

        ArchitectureContractSurfaceExposureResult excluded =
            ArchitectureContractSurfaceExposureScanner.Scan(excludedRoot);
        ArchitectureContractSurfaceExposureResult included =
            ArchitectureContractSurfaceExposureScanner.Scan(includedRoot);

        Assert.Multiple(() =>
        {
            Assert.That(Targets(excluded), Does.Not.Contain(Target(excludedTarget)));
            Assert.That(Targets(included), Does.Contain(Target(includedTarget)));
            Assert.That(ArchitecturePublicApiSurfaceScanner.GetExportedSurface(excludedRoot.Assembly)
                .Any(entry => entry.Signature.StartsWith("event ", StringComparison.Ordinal)), Is.False);
            Assert.That(ArchitecturePublicApiSurfaceScanner.GetExportedSurface(includedRoot.Assembly)
                .Any(entry => entry.Signature.StartsWith("event ", StringComparison.Ordinal)), Is.True);
        });
    }

    [Test]
    public void Scan_VisibleAccessorMetadataRecordsTypeReferencesWithoutIncludingHiddenAccessorMetadata()
    {
        ArchitectureContractSurfaceExposureResult result =
            ArchitectureContractSurfaceExposureScanner.Scan(typeof(AccessorMetadataRoot));

        Assert.Multiple(() =>
        {
            Assert.That(HasAccessorPath(result, typeof(GetterMethodMetadataPayload), "get"), Is.True);
            Assert.That(HasAccessorPath(result, typeof(GetterReturnMetadataPayload), "get", "return"), Is.True);
            Assert.That(HasAccessorPath(result, typeof(SetterMethodMetadataPayload), "set"), Is.True);
            Assert.That(HasAccessorPath(result, typeof(SetterParameterMetadataPayload), "set", "parameter"), Is.True);
            Assert.That(HasAccessorPath(result, typeof(EventAddMethodMetadataPayload), "add"), Is.True);
            Assert.That(HasAccessorPath(result, typeof(EventAddParameterMetadataPayload), "add", "parameter"), Is.True);
            Assert.That(HasAccessorPath(result, typeof(EventRemoveMethodMetadataPayload), "remove"), Is.True);
            Assert.That(HasAccessorPath(result, typeof(EventRemoveParameterMetadataPayload), "remove", "parameter"), Is.True);
            Assert.That(Targets(result), Does.Not.Contain(Target(typeof(HiddenSetterParameterMetadataPayload))));
        });
    }

    private static bool HasPath(
        ArchitectureContractSurfaceExposureResult result, Type target, string segmentKind) =>
        result.Exposures.Any(exposure => Target(target).Equals(exposure.ReferencedType)
            && exposure.Path.Segments.Any(segment => segment.Kind == segmentKind));

    private static bool HasAccessorPath(
        ArchitectureContractSurfaceExposureResult result,
        Type target,
        string accessor,
        string? nestedSegmentKind = null) =>
        result.Exposures.Any(exposure =>
            Target(target).Equals(exposure.ReferencedType)
            && exposure.Path.Segments.Any(segment => segment.Kind == "accessor" && segment.Value == accessor)
            && (nestedSegmentKind == null || exposure.Path.Segments.Any(segment => segment.Kind == nestedSegmentKind)));

    private static HashSet<ArchitectureContractExposureTarget> Targets(
        ArchitectureContractSurfaceExposureResult result) =>
        result.Exposures.Select(exposure => exposure.ReferencedType).ToHashSet();

    private static ArchitectureContractExposureTarget Target(Type type) =>
        new(type.Assembly.FullName!, type.FullName!);

    private static string AttributeOccurrence(ArchitectureContractSurfaceExposureResult result, Type target) =>
        result.Exposures.Single(exposure =>
            Target(target).Equals(exposure.ReferencedType)
            && exposure.Path.Segments.Any(segment => segment.Kind == "attribute_argument" && segment.Value == "constructor:0"))
            .Path.Segments.Single(segment => segment.Kind == "attribute").Value;

    private static (Type Root, Type Target) BuildSyntheticSurface(Version version)
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("ContractExposureDuplicate") { Version = version }, AssemblyBuilderAccess.Run);
        ModuleBuilder module = assembly.DefineDynamicModule("Main");
        TypeBuilder target = module.DefineType("Duplicate.Target", TypeAttributes.Public | TypeAttributes.Class);
        Type targetType = target.CreateType()!;
        TypeBuilder root = module.DefineType("Duplicate.Root", TypeAttributes.Public | TypeAttributes.Class);
        MethodBuilder method = root.DefineMethod(
            "Accept", MethodAttributes.Public | MethodAttributes.Static, typeof(void), new[] { targetType });
        method.GetILGenerator().Emit(OpCodes.Ret);
        return (root.CreateType()!, targetType);
    }

    private static (Type Root, Type Target) BuildEventSurface(
        MethodAttributes addVisibility,
        MethodAttributes removeVisibility)
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"ContractExposureEvent-{Guid.NewGuid():N}"), AssemblyBuilderAccess.Run);
        ModuleBuilder module = assembly.DefineDynamicModule("Main");
        TypeBuilder target = module.DefineType("Events.RemoveOnlyPayload", TypeAttributes.Public | TypeAttributes.Class);
        Type targetType = target.CreateType()!;
        Type eventHandlerType = typeof(Action<>).MakeGenericType(targetType);
        TypeBuilder root = module.DefineType("Events.RemoveOnlyEventRoot", TypeAttributes.Public | TypeAttributes.Class);
        EventBuilder @event = root.DefineEvent("Changed", EventAttributes.None, eventHandlerType);
        MethodBuilder add = root.DefineMethod(
            "add_Changed", addVisibility | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            typeof(void), new[] { eventHandlerType });
        add.GetILGenerator().Emit(OpCodes.Ret);
        MethodBuilder remove = root.DefineMethod(
            "remove_Changed", removeVisibility | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            typeof(void), new[] { eventHandlerType });
        remove.GetILGenerator().Emit(OpCodes.Ret);
        @event.SetAddOnMethod(add);
        @event.SetRemoveOnMethod(remove);
        return (root.CreateType()!, targetType);
    }

    public sealed class Customer
    {
    }

    public sealed class AlternateCustomer
    {
    }

    internal sealed class InternalSurfacePayload
    {
    }

    public sealed class SurfaceShapeRoot
    {
        public Customer PublicValue => new();

        internal InternalSurfacePayload InternalValue => new();
    }

    public sealed class AccessorNamedMethodRoot
    {
        // The name intentionally resembles a CLR property accessor without carrying SpecialName.
#pragma warning disable IDE1006 // Naming Styles
        public Customer get_Current() => new();
#pragma warning restore IDE1006 // Naming Styles
    }

    public sealed class SelectedOuterRoot
    {
        public sealed class UnselectedNested
        {
            public NestedOnlyPayload Value => new();
        }
    }

    public sealed class NestedOnlyPayload
    {
    }

    public sealed class GetterMethodMetadataPayload
    {
    }

    public sealed class GetterReturnMetadataPayload
    {
    }

    public sealed class SetterMethodMetadataPayload
    {
    }

    public sealed class SetterParameterMetadataPayload
    {
    }

    public sealed class EventAddMethodMetadataPayload
    {
    }

    public sealed class EventAddParameterMetadataPayload
    {
    }

    public sealed class EventRemoveMethodMetadataPayload
    {
    }

    public sealed class EventRemoveParameterMetadataPayload
    {
    }

    public sealed class HiddenSetterParameterMetadataPayload
    {
    }

    public sealed class AccessorMetadataRoot
    {
        private Customer _value = new();

        public Customer Value
        {
            [ExposureMetadata(typeof(GetterMethodMetadataPayload), ExposureKind.Customer, 10, "getter-method")]
            [return: ExposureMetadata(typeof(GetterReturnMetadataPayload), ExposureKind.Customer, 11, "getter-return")]
            get => _value;

            [ExposureMetadata(typeof(SetterMethodMetadataPayload), ExposureKind.Customer, 12, "setter-method")]
            [param: ExposureMetadata(typeof(SetterParameterMetadataPayload), ExposureKind.Customer, 13, "setter-parameter")]
            set => _value = value;
        }

        public event Action<Customer>? Changed
        {
            [ExposureMetadata(typeof(EventAddMethodMetadataPayload), ExposureKind.Customer, 14, "event-add-method")]
            [param: ExposureMetadata(typeof(EventAddParameterMetadataPayload), ExposureKind.Customer, 15, "event-add-parameter")]
            add { }

            [ExposureMetadata(typeof(EventRemoveMethodMetadataPayload), ExposureKind.Customer, 16, "event-remove-method")]
            [param: ExposureMetadata(typeof(EventRemoveParameterMetadataPayload), ExposureKind.Customer, 17, "event-remove-parameter")]
            remove { }
        }

        public Customer VisibleWithHiddenSetter
        {
            get => _value;

            [param: ExposureMetadata(typeof(HiddenSetterParameterMetadataPayload), ExposureKind.Customer, 18, "hidden-setter")]
            private set => _value = value;
        }
    }

    public class BaseContract
    {
    }

    public interface IContractInterface
    {
    }

    public sealed class Envelope<T>
    {
    }

    public delegate Customer CustomerDelegate(Envelope<Customer> value);

    public struct NullableContract
    {
    }

    public enum ExposureKind
    {
        Customer
    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public sealed class ExposureMetadataAttribute : Attribute
    {
        public ExposureMetadataAttribute(Type referenced, ExposureKind kind, int number, string text)
        {
            Referenced = referenced;
            Kind = kind;
            Number = number;
            Text = text;
        }

        public Type Referenced { get; }

        public ExposureKind Kind { get; }

        public int Number { get; }

        public string Text { get; }

        public Type? NamedType { get; set; }
    }

    [AttributeUsage(AttributeTargets.All)]
    public sealed class EnumArrayMetadataAttribute : Attribute
    {
        public EnumArrayMetadataAttribute(ExposureKind[] values)
        {
            Values = values;
        }

        public ExposureKind[] Values { get; }
    }

    [EnumArrayMetadata(new ExposureKind[0])]
    public sealed class EmptyEnumArrayAttributeRoot
    {
    }

    [ExposureMetadata(typeof(Customer), ExposureKind.Customer, 1, "customer")]
    [ExposureMetadata(typeof(AlternateCustomer), ExposureKind.Customer, 2, "alternate")]
    public sealed class ForwardRepeatedAttributes
    {
    }

    [ExposureMetadata(typeof(AlternateCustomer), ExposureKind.Customer, 2, "alternate")]
    [ExposureMetadata(typeof(Customer), ExposureKind.Customer, 1, "customer")]
    public sealed class ReverseRepeatedAttributes
    {
    }

    [ExposureMetadata(typeof(Customer), ExposureKind.Customer, 1, "value", NamedType = typeof(Customer))]
    public class ContractRoot<T> : BaseContract, IContractInterface where T : BaseContract
    {
        [ExposureMetadata(typeof(Customer), ExposureKind.Customer, 2, "ctor")]
        public ContractRoot([ExposureMetadata(typeof(Customer), ExposureKind.Customer, 3, "parameter")] Envelope<Customer> value)
        {
        }

        [ExposureMetadata(typeof(Customer), ExposureKind.Customer, 4, "method")]
        [return: ExposureMetadata(typeof(Customer), ExposureKind.Customer, 5, "return")]
        public Envelope<Customer> Method<[ExposureMetadata(typeof(Customer), ExposureKind.Customer, 6, "generic")] U>(
            [ExposureMetadata(typeof(Customer), ExposureKind.Customer, 7, "parameter")] U value)
            where U : BaseContract => new();

        public System.Threading.Tasks.Task<Envelope<Customer>> AsyncMethod() =>
            System.Threading.Tasks.Task.FromResult(new Envelope<Customer>());

        public Customer[] Array => System.Array.Empty<Customer>();

        public NullableContract? Nullable => null;

        public (Customer, int) Tuple => (new Customer(), 0);

        public CustomerDelegate Delegate => _ => new Customer();

        public NestedContract Nested => new();

        public sealed class NestedContract
        {
            public Customer Value => new();
        }
    }

    public class CyclicContract<T> where T : CyclicContract<T>
    {
    }
}
