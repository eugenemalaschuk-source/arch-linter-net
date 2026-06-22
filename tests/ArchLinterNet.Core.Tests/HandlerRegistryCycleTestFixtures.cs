namespace HandlerRegistryCycleFixtures.LayerA
{
    public sealed class ServiceA
    {
        public LayerB.ServiceB B = null!;
    }
}

namespace HandlerRegistryCycleFixtures.LayerB
{
    public sealed class ServiceB
    {
        public LayerA.ServiceA A = null!;
    }
}

namespace HandlerRegistryLayerFixtures.Upper
{
    public sealed class UpperService
    {
        public Lower.LowerService Lower = null!;
    }
}

namespace HandlerRegistryLayerFixtures.Lower
{
    public sealed class LowerService
    {
    }
}
