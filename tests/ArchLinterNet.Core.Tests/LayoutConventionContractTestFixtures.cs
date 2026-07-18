namespace LayoutConventionContractTestFixtures.Services
{
    public sealed class OrderService;

    public sealed class PaymentService;

    public interface IWronglyPlacedService;
}

namespace LayoutConventionContractTestFixtures.Interfaces
{
    public interface IOrderService;

    public sealed class WronglyPlacedClass;
}

namespace LayoutConventionContractTestFixtures.MismatchedFileName
{
    public sealed class ActualTypeName;
}

namespace LayoutConventionContractTestFixtures.WhenRefinement
{
    public sealed class IncludedByWhen;

    public sealed class ExcludedByWhen;
}

namespace LayoutConventionContractTestFixtures.MixedNamespaceFile
{
    public sealed class ServiceInMatchingNamespace;
}

namespace LayoutConventionContractTestFixtures.MixedNamespaceFileOther
{
    public interface IEscapingInterface;
}

// Deliberately has NO corresponding WriteFixtureFile call in LayoutConventionContractTests.SetUp,
// so this type's ArchitectureDeclaredTypeFact always has a null SourceFilePath even when other
// fixtures in the same run are source-enriched (partial-enrichment regression fixture).
namespace LayoutConventionContractTestFixtures.UnfiledNamespace
{
    public sealed class NoSourceFileType;
}

// Declared as a real record (compiles to a class at the CLR level - reflection alone can never
// distinguish it from an ordinary class). Deliberately has NO corresponding WriteFixtureFile call,
// so its fact never gets Roslyn-accurate TypeKind enrichment even under partial source enrichment.
namespace LayoutConventionContractTestFixtures.RecordKind
{
    public sealed record UnresolvedRecord;
}

// Two distinct written files below both declare this exact namespace+type name, producing a
// partial-class ambiguity (null SourceFilePath) whose one candidate declaration path sits under
// "Services" - regression fixture for folder-based rules escaping via ambiguous source mapping.
namespace LayoutConventionContractTestFixtures.AmbiguousFolder
{
    public sealed class PartialOffender;
}

// An abstract class is itself an extension point, not a leaf implementation - require_matching_interface
// must not demand an I-prefixed counterpart for it.
namespace LayoutConventionContractTestFixtures.AbstractServices
{
    public abstract class AbstractBaseService;
}
