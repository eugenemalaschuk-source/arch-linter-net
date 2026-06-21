namespace ExternalDependencyContractTestsFixtures.Core
{
    public sealed class PureCoreType;

    public sealed class CoreTypeWithMethodCall
    {
        public void DoWork()
        {
            new ExternalDependencyContractTestsFixtures.VendorSdk.Client();
        }
    }

    public sealed class CoreTypeWithConstructorCall
    {
        public CoreTypeWithConstructorCall()
        {
            new ExternalDependencyContractTestsFixtures.VendorSdk.Client();
        }
    }

    public sealed class CoreTypeWithPropertyAccess
    {
        private ExternalDependencyContractTestsFixtures.VendorSdk.Client? _client;

        public void Init()
        {
            _client = new ExternalDependencyContractTestsFixtures.VendorSdk.Client();
            _ = _client.ToString();
        }
    }

    public sealed class CoreTypeWithGenericReference
    {
        public List<ExternalDependencyContractTestsFixtures.VendorSdk.Client> GetClients()
        {
            return new List<ExternalDependencyContractTestsFixtures.VendorSdk.Client>();
        }
    }

    public sealed class CoreTypeWithGenericOnlyInBody
    {
        public void DoWork()
        {
            var clients = new List<ExternalDependencyContractTestsFixtures.VendorSdk.Client>();
            _ = clients.Count;
        }
    }
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

namespace ExternalDependencyContractTestsFixtures.UnityStyle
{
    public sealed class CoreTypeWithUnityMethodBody
    {
        public void LogSomething()
        {
            UnityEngine.Debug.Log("test");
        }
    }
}

namespace ExternalDependencyContractTestsFixtures.VendorSdk
{
    public static class Debug
    {
        public static void Log(string message) { }
    }
}

namespace UnityEngine
{
    public static class Debug
    {
        public static void Log(object message) { }
    }
}
