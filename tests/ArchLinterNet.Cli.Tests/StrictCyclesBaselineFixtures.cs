namespace CliStrictCyclesBaselineFixtures.Acyclic.LayerA
{
    public sealed class ServiceA
    {
        public LayerB.ServiceB B = null!;
    }
}

namespace CliStrictCyclesBaselineFixtures.Acyclic.LayerB
{
    public sealed class ServiceB
    {
        public LayerC.ServiceC C = null!;
    }
}

namespace CliStrictCyclesBaselineFixtures.Acyclic.LayerC
{
    public sealed class ServiceC
    {
    }
}

namespace CliStrictCyclesBaselineFixtures.Cyclic.LayerA
{
    public sealed class ServiceA
    {
        public LayerB.ServiceB B = null!;
    }
}

namespace CliStrictCyclesBaselineFixtures.Cyclic.LayerB
{
    public sealed class ServiceB
    {
        public LayerC.ServiceC C = null!;
    }
}

namespace CliStrictCyclesBaselineFixtures.Cyclic.LayerC
{
    public sealed class ServiceC
    {
        public LayerA.ServiceA A = null!;
    }
}
