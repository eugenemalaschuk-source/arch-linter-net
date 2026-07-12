// Sales/Inventory/SharedKernel-style fixtures for the contextual dependency/allow-only contract
// families (#112), mirroring SelectorContractTestFixtures.cs's convention of plain, reflection-
// scannable C# types carrying classification-attribute evidence and referencing each other via
// properties. ArchitectureReferenceScanner walks these references via reflection (fields,
// properties, methods, constructors) - there is no dynamic Roslyn compilation step here, only
// ordinary compiled fixture types.
namespace ContextualContractTestFixtures
{
    // Maps to role DomainLayer with metadata["domain"] extracted from constructor[0].
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ContextDomainMarkerAttribute : Attribute
    {
        public ContextDomainMarkerAttribute(string domain)
        {
            Domain = domain;
        }

        public string Domain { get; }
    }

    // Maps to role DomainLayer but with no metadata extraction mapping declared for it, so types
    // carrying only this attribute resolve role DomainLayer with an empty metadata dictionary -
    // used to exercise the "missing source/target metadata" scenarios.
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ContextDomainlessMarkerAttribute : Attribute;

    // Maps to role SharedKernel - used for the exclude-selector scenarios.
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ContextSharedKernelMarkerAttribute : Attribute;

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public sealed class ContextPortMarkerAttribute : Attribute
    {
        public ContextPortMarkerAttribute(string domain) => Domain = domain;
        public string Domain { get; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ContextAdapterMarkerAttribute : Attribute
    {
        public ContextAdapterMarkerAttribute(string domain) => Domain = domain;
        public string Domain { get; }
    }

    // --- Sales domain (role DomainLayer, metadata domain=Sales) ---

    [ContextDomainMarker("Sales")]
    public sealed class SalesOrderLine;

    [ContextDomainMarker("Sales")]
    public sealed class SalesOrder
    {
        // Same-domain reference: must not violate a cross-domain context_dependencies contract.
        public SalesOrderLine Line { get; } = null!;
    }

    [ContextDomainMarker("Sales")]
    public sealed class SalesCheckout
    {
        // Cross-domain reference: Sales -> Inventory.
        public InventoryStockItem Stock { get; } = null!;
    }

    [ContextDomainMarker("Sales")]
    public sealed class SalesInvoice
    {
        // References only SharedKernel - exercised by exclude-selector tests.
        public Money Total { get; } = null!;
    }

    [ContextDomainMarker("Sales")]
    public sealed class SalesInvoiceReferencingInventoryAndSharedKernel
    {
        // Excluded (SharedKernel) reference alongside a non-excluded forbidden (Inventory) one, so
        // the exclude selector can be shown to suppress only the SharedKernel candidate.
        public Money Total { get; } = null!;
        public InventoryStockItem Reserved { get; } = null!;
    }

    // --- Inventory domain (role DomainLayer, metadata domain=Inventory) ---

    [ContextDomainMarker("Inventory")]
    public sealed class InventoryStockItem;

    [ContextDomainMarker("Inventory")]
    public sealed class InventoryWarehouse
    {
        public InventoryStockItem Stock { get; } = null!;
    }

    // --- SharedKernel (role SharedKernel, no domain metadata) ---

    [ContextSharedKernelMarker]
    public sealed class Money;

    // --- Missing-metadata fixtures (role DomainLayer, no "domain" metadata key) ---

    [ContextDomainlessMarker]
    public sealed class DomainlessSourceType
    {
        public InventoryStockItem Stock { get; } = null!;
    }

    [ContextDomainlessMarker]
    public sealed class DomainlessTargetType;

    [ContextDomainMarker("Sales")]
    public sealed class SourceReferencingDomainlessTarget
    {
        public DomainlessTargetType Target { get; } = null!;
    }

    // References the same target type through two distinct members - used to prove a single
    // source/target pair produces exactly one contextual violation, not one per reflection path
    // ArchitectureReferenceScanner discovers the reference through.
    [ContextDomainMarker("Sales")]
    public sealed class SalesMultiMemberReferenceToInventory
    {
        public InventoryStockItem Primary { get; } = null!;
        public InventoryStockItem Secondary { get; } = null!;
    }

    // Not classified by any attribute mapping used in these tests - must never match any
    // contextual selector regardless of role/metadata declared on the selector.
    public sealed class PlainUnclassifiedType;

    [ContextPortMarker("Inventory")]
    public interface IInventoryPort;

    [ContextPortMarker("Payment")]
    public interface IPaymentPort;

    [ContextPortMarker("Catalog")]
    public interface ICatalogPort;

    [ContextAdapterMarker("Payment")]
    public sealed class StripePaymentAdapter : IPaymentPort;

    [ContextAdapterMarker("Payment")]
    public sealed class MismatchedPaymentAdapter : ICatalogPort;

    [ContextDomainMarker("Sales")]
    public sealed class SalesUsesInventoryPort { public IInventoryPort Port { get; } = null!; }

    // Inventory-domain type classified as Adapter (neither DomainLayer nor Port) - used to prove a
    // target that matches target_context but matches neither `forbidden` nor `allowed_seams` is
    // still reported as a violation (allow-list gap regression coverage).
    [ContextAdapterMarker("Inventory")]
    public sealed class InventoryLegacyAdapter;

    [ContextDomainMarker("Sales")]
    public sealed class SalesReferencesInventoryAdapter { public InventoryLegacyAdapter Adapter { get; } = null!; }

    // Adapter with zero interfaces - used to prove the adapter-binding check does not throw when
    // there is no implemented interface to classify.
    [ContextAdapterMarker("Payment")]
    public sealed class InterfacelessPaymentAdapter;

    // Unclassified interface that sorts alphabetically before ICatalogPort (by full name), so a
    // naive "first interface, ordered alphabetically" pick would surface it as mismatch evidence
    // instead of the classified-but-wrong ICatalogPort.
    public interface IAardvarkUnclassifiedInterface;

    [ContextAdapterMarker("Payment")]
    public sealed class AdapterWithUnclassifiedAndWrongPortInterfaces : IAardvarkUnclassifiedInterface, ICatalogPort;

    // --- Generic port/domain fixtures (#306 review 3.2): RoleIndex is built from
    // assembly.GetTypes(), which only ever yields open generic type definitions
    // (e.g. IGenericPaymentPort<>), while reflection on a concrete reference/adapter reports the
    // closed constructed type (e.g. IGenericPaymentPort<SalesOrder>) instead. ---

    [ContextPortMarker("Payment")]
    public interface IGenericPaymentPort<T>;

    [ContextAdapterMarker("Payment")]
    public sealed class GenericPaymentAdapter : IGenericPaymentPort<SalesOrder>;

    [ContextDomainMarker("Inventory")]
    public sealed class GenericInventoryItem<T>;

    [ContextDomainMarker("Sales")]
    public sealed class SalesReferencesGenericInventoryItem { public GenericInventoryItem<SalesOrder> Item { get; } = null!; }

    [ContextPortMarker("Inventory")]
    public interface IGenericInventoryPort<T>;

    [ContextDomainMarker("Sales")]
    public sealed class SalesUsesGenericInventoryPort { public IGenericInventoryPort<SalesOrder> Port { get; } = null!; }
}
