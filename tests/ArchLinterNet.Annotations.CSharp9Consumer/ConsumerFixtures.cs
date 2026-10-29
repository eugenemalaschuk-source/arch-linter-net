using ArchLinterNet.Annotations;

[assembly: DomainLayer(BoundedContext = "Commerce", Module = "Orders")]

namespace ArchLinterNet.Annotations.CSharp9Consumer
{
    public sealed class DomainLayerConsumerFixture
    {
        public string Name { get; } = "Orders";
    }

    [AggregateRoot(BoundedContext = "Commerce", Module = "Orders")]
    public sealed class AnnotatedOrder
    {
    }

    [Domain("Commerce")]
    [BoundedContext("Orders")]
    [Module("Checkout")]
    [Feature("OrderEntry")]
    [Platform("Web")]
    [Runtime("Server")]
    public sealed class ContextMetadataConsumerFixture
    {
    }

    [Entity]
    [BoundedContext("Sales")]
    public sealed class SalesCheckout
    {
        public InventoryStockItem Stock { get; } = null!;
    }

    [Entity]
    [BoundedContext("Inventory")]
    public sealed class InventoryStockItem
    {
    }
}
