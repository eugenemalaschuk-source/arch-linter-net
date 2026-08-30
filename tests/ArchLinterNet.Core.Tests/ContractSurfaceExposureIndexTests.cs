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
    public void Scan_SameFullNameFromDistinctAssembliesRemainsDistinct()
    {
        (Type firstRoot, Type firstTarget) = BuildSyntheticSurface("First");
        (Type secondRoot, Type secondTarget) = BuildSyntheticSurface("Second");

        ArchitectureContractSurfaceExposureResult first = ArchitectureContractSurfaceExposureScanner.Scan(firstRoot);
        ArchitectureContractSurfaceExposureResult second = ArchitectureContractSurfaceExposureScanner.Scan(secondRoot);
        ArchitectureContractExposureTarget firstIdentity = Target(firstTarget);
        ArchitectureContractExposureTarget secondIdentity = Target(secondTarget);

        Assert.Multiple(() =>
        {
            Assert.That(firstIdentity.FullTypeName, Is.EqualTo(secondIdentity.FullTypeName));
            Assert.That(firstIdentity.AssemblyName, Is.Not.EqualTo(secondIdentity.AssemblyName));
            Assert.That(first.Exposures.Any(exposure => exposure.ReferencedType.Equals(firstIdentity)), Is.True);
            Assert.That(second.Exposures.Any(exposure => exposure.ReferencedType.Equals(secondIdentity)), Is.True);
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

    private static bool HasPath(
        ArchitectureContractSurfaceExposureResult result, Type target, string segmentKind) =>
        result.Exposures.Any(exposure => Target(target).Equals(exposure.ReferencedType)
            && exposure.Path.Segments.Any(segment => segment.Kind == segmentKind));

    private static HashSet<ArchitectureContractExposureTarget> Targets(
        ArchitectureContractSurfaceExposureResult result) =>
        result.Exposures.Select(exposure => exposure.ReferencedType).ToHashSet();

    private static ArchitectureContractExposureTarget Target(Type type) =>
        new(type.Assembly.GetName().Name!, type.FullName!);

    private static (Type Root, Type Target) BuildSyntheticSurface(string suffix)
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"ContractExposure-{suffix}-{Guid.NewGuid():N}"), AssemblyBuilderAccess.Run);
        ModuleBuilder module = assembly.DefineDynamicModule("Main");
        TypeBuilder target = module.DefineType("Duplicate.Target", TypeAttributes.Public | TypeAttributes.Class);
        Type targetType = target.CreateType()!;
        TypeBuilder root = module.DefineType("Duplicate.Root", TypeAttributes.Public | TypeAttributes.Class);
        MethodBuilder method = root.DefineMethod(
            "Accept", MethodAttributes.Public | MethodAttributes.Static, typeof(void), new[] { targetType });
        method.GetILGenerator().Emit(OpCodes.Ret);
        return (root.CreateType()!, targetType);
    }

    public sealed class Customer
    {
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
