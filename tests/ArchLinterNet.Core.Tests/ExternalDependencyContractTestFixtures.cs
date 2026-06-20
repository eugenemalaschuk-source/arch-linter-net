namespace ExternalDependencyContractTestsFixtures.Core
{
    public sealed class PureCoreType;
}

namespace ExternalDependencyContractTestsFixtures.VendorSdk
{
    public sealed class Client;
}

namespace ExternalDependencyContractTestsFixtures.Adapters
{
    public sealed class AdapterUsingVendorSdk
    {
        public ExternalDependencyContractTestsFixtures.VendorSdk.Client Client { get; } = new();
    }
}
