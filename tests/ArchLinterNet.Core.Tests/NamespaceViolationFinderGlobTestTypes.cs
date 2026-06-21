namespace ReviewTest.Modules.Sales.Internal
{
    public sealed class SalesService;
}

namespace ReviewTest.Modules.Billing.Internal
{
    public sealed class BillingService;
}

namespace ReviewTest.SharedKernel
{
    public sealed class SharedKernelType
    {
        public ReviewTest.Modules.Sales.Internal.SalesService Sales { get; } = new();

        public ReviewTest.Modules.Billing.Internal.BillingService Billing { get; } = new();
    }
}
