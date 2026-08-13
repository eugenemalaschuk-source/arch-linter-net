namespace Synthetic.ApiSurfaceSelector.Domain;

// Incidental CLR-public implementation type, part of the large exported surface the selected
// snapshots must exclude.
public sealed class InternalTaxCalculator
{
    public decimal Calculate(decimal amount, decimal rate) => amount * rate;
}
