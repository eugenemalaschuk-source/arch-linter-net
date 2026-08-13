using Synthetic.ApiSurfaceSelector.Architecture;

namespace Synthetic.ApiSurfaceSelector.Domain;

// Governed only by the temporary escaping-selected-api contract: selected via a distinct marker
// so it never joins the fixture's permanent, always-green contract set. Its member signature
// references the unselected first-party InternalPricingEngine, which must fail closed rather than
// silently escape the reviewed surface.
[EscapeDemoApiContract]
public sealed class PricingEngineAdapter
{
    public InternalPricingEngine Resolve() => new();
}
