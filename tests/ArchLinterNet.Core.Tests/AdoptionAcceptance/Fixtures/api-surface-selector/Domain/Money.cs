using Synthetic.ApiSurfaceSelector.Architecture;

namespace Synthetic.ApiSurfaceSelector.Domain;

// The intentional, has_attribute-selected compatibility surface. Its existing primary semantic
// role (ValueObject) must survive selection unchanged, and every member signature below is BCL
// only, so it never needs first-party API-membership evidence of its own.
[ValueObjectRole]
[PublicApiContract]
public sealed class Money
{
    public Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public string Format() => $"{Amount} {Currency}";
}
