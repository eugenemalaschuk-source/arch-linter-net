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
