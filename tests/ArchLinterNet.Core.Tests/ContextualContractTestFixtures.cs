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
}
