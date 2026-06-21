namespace AcyclicSiblingFixtures.TwoNode.Auth
{
    public sealed class AuthService
    {
        public Payments.PaymentService Payment = null!;
    }
}

namespace AcyclicSiblingFixtures.TwoNode.Payments
{
    public sealed class PaymentService
    {
        public Auth.AuthService Service = null!;
    }
}

namespace AcyclicSiblingFixtures.ThreeNode.Auth
{
    public sealed class AuthService
    {
        public Payments.PaymentService Payment = null!;
    }
}

namespace AcyclicSiblingFixtures.ThreeNode.Payments
{
    public sealed class PaymentService
    {
        public Billing.BillingService Billing = null!;
    }
}

namespace AcyclicSiblingFixtures.ThreeNode.Billing
{
    public sealed class BillingService
    {
        public Auth.AuthService Auth = null!;
    }
}

namespace AcyclicSiblingFixtures.Descendant.Desc.Controllers
{
    public sealed class HomeController
    {
        public Core.MainService Main = null!;
    }
}

namespace AcyclicSiblingFixtures.Descendant.Desc.Core
{
    public sealed class MainService
    {
        public Controllers.HomeController Home = null!;
    }
}

namespace AcyclicSiblingFixtures.MultiAncestor.ModuleA.Alpha
{
    public sealed class AService
    {
        public Beta.BService B = null!;
    }
}

namespace AcyclicSiblingFixtures.MultiAncestor.ModuleA.Beta
{
    public sealed class BService
    {
        public Alpha.AService A = null!;
    }
}

namespace AcyclicSiblingFixtures.MultiAncestor.ModuleB.Gamma
{
    public sealed class CService
    {
        public Delta.DService D = null!;
    }
}

namespace AcyclicSiblingFixtures.MultiAncestor.ModuleB.Delta
{
    public sealed class DService
    {
        public Gamma.CService C = null!;
    }
}

namespace AcyclicSiblingFixtures.Clean.A
{
    public sealed class ServiceA
    {
        public B.ServiceB B = null!;
    }
}

namespace AcyclicSiblingFixtures.Clean.B
{
    public sealed class ServiceB
    {
    }
}
