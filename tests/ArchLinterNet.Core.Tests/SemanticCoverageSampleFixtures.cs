using AttributeRoleExtractionTestFixtures;

namespace SemanticCoverageSampleFixtures.Sales
{
    [DomainMarker("Sales")]
    public sealed class Order;
}

namespace SemanticCoverageSampleFixtures.Inventory
{
    [DomainMarker("Inventory")]
    public sealed class StockItem;
}

namespace SemanticCoverageSampleFixtures.SharedKernel
{
    public sealed class Clock;
}

namespace SemanticCoverageSampleFixtures.Unity.Client
{
    public sealed class ClientBehaviour;
}
