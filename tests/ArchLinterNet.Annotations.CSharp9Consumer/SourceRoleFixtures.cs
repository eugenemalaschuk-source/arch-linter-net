using ArchLinterNet.Annotations;

namespace ArchLinterNet.Annotations.CSharp9Consumer
{
    [Port(Domain = "Commerce", BoundedContext = "Orders", Module = "Checkout")]
    public interface ICheckoutPort
    {
    }

    [ViewModel(Domain = "Commerce", BoundedContext = "Orders", Feature = "Checkout")]
    public sealed class CheckoutViewModel
    {
    }

    [MonoBehaviourAdapter(Platform = "Unity", Runtime = "Client")]
    public sealed class CheckoutMonoBehaviourAdapter
    {
    }
}
