using System;
using ArchLinterNet.Annotations;

[assembly: AnnotationCatalogIdentity(1, "0.10.0")]

namespace ArchLinterNet.Annotations
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    internal sealed class AnnotationCatalogIdentityAttribute(int catalogGeneration, string packageVersion) : Attribute
    {
        public int CatalogGeneration { get; } = catalogGeneration;

        public string PackageVersion { get; } = packageVersion;
    }
}

namespace ContextualContractCanonicalCliFixtures
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal sealed class CanonicalContextRoleAttribute : Attribute;

    [CanonicalContextRole]
    [BoundedContext("Sales")]
    public sealed class CanonicalSalesCheckout
    {
        public CanonicalInventoryStockItem Stock { get; } = null!;
    }

    [CanonicalContextRole]
    [BoundedContext("Inventory")]
    public sealed class CanonicalInventoryStockItem;
}
