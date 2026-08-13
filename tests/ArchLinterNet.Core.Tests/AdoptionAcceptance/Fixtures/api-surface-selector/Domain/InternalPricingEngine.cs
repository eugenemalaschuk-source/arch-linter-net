namespace Synthetic.ApiSurfaceSelector.Domain;

// Incidental CLR-public implementation type: exported because it is a public class in a governed
// assembly, never because it was intended as a reviewed compatibility contract. Also the escaping
// first-party type referenced by PricingEngineAdapter.
public sealed class InternalPricingEngine
{
    public decimal Resolve(decimal baseAmount) => baseAmount;
}
