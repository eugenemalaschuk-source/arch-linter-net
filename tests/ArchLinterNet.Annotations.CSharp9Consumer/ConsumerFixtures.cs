using ArchLinterNet.Annotations;

[assembly: DomainLayer(BoundedContext = "Commerce", Module = "Orders")]

namespace ArchLinterNet.Annotations.CSharp9Consumer
{
    [AggregateRoot(BoundedContext = "Commerce", Module = "Orders")]
    public sealed class AnnotatedOrder
    {
    }

    public sealed class DomainLayerConsumerFixture
    {
    }
}
