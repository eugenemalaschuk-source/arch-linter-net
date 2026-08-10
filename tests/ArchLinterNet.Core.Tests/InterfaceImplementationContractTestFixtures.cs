namespace InterfaceImplementationContractTestFixtures.Ports
{
    public interface IPaymentPort;

    public interface IGenericPort<T>;

    // An interface extending a selected port is a contract extension, not an implementation.
    public interface IExtendedPort : IPaymentPort;
}

namespace InterfaceImplementationContractTestFixtures.Ports.Prefixed
{
    public interface IPrefixedPort;
}

// Distinct from IPaymentPort so glob-pattern tests over Modules.* fixtures below don't bleed
// into the closed-world "every IPaymentPort implementer in the assembly" assertions the tests
// above already make.
namespace InterfaceImplementationContractTestFixtures.ModulePorts
{
    public interface IModulePort;
}

namespace InterfaceImplementationContractTestFixtures.Adapters
{
    using InterfaceImplementationContractTestFixtures.Ports;

    public sealed class PaymentAdapter : IPaymentPort;

    public class AdapterBase : IPaymentPort;
}

namespace InterfaceImplementationContractTestFixtures.Domain
{
    using InterfaceImplementationContractTestFixtures.Adapters;
    using InterfaceImplementationContractTestFixtures.Ports;
    using InterfaceImplementationContractTestFixtures.Ports.Prefixed;

    public sealed class DomainPaymentImplementation : IPaymentPort;

    // Implements IPaymentPort only through its base class.
    public sealed class InheritedImplementation : AdapterBase;

    public sealed class GenericPortImplementation : IGenericPort<int>;

    public sealed class PrefixedPortImplementation : IPrefixedPort;

    public sealed class CleanDomainType;
}

// Nested under an extra "module" segment so allowed_only_in_namespaces/forbidden_in_namespaces
// glob patterns such as "...Modules.*.Adapters"/"...Modules.*.Domain" (issue #443) have a middle
// segment to consume.
namespace InterfaceImplementationContractTestFixtures.Modules.Orders.Adapters
{
    using InterfaceImplementationContractTestFixtures.ModulePorts;

    public sealed class OrdersPaymentAdapter : IModulePort;
}

namespace InterfaceImplementationContractTestFixtures.Modules.Orders.Domain
{
    using InterfaceImplementationContractTestFixtures.ModulePorts;

    public sealed class OrdersDomainPaymentImplementation : IModulePort;
}

// Outside every Modules.*.Adapters/Modules.*.Domain glob boundary — the negative control for
// both glob-pattern tests.
namespace InterfaceImplementationContractTestFixtures.Modules.Orders.Other
{
    using InterfaceImplementationContractTestFixtures.ModulePorts;

    public sealed class OrdersOtherImplementation : IModulePort;
}
