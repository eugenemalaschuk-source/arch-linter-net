namespace Synthetic.ApiSurfaceSelector.Domain;

// Starts as an incidental exported type with no API-membership marker. The gate test adds and
// then removes [PublicApiContract] on this type to prove selector-membership changes are
// review-visible snapshot deltas, not silent.
public sealed class InternalDiscountPolicy
{
    public decimal Apply(decimal amount) => amount;
}
