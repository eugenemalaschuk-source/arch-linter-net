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
