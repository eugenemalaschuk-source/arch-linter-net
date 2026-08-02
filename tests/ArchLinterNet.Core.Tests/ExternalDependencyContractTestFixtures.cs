namespace ExternalDependencyContractTestsFixtures.Core
{
    public sealed class PureCoreType;

    public sealed class CoreTypeWithMethodCall
    {
        public static void DoWork()
        {
            _ = new ExternalDependencyContractTestsFixtures.VendorSdk.Client();
        }
    }

    public sealed class CoreTypeWithConstructorCall
    {
        public CoreTypeWithConstructorCall()
        {
            _ = new ExternalDependencyContractTestsFixtures.VendorSdk.Client();
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
        public static List<ExternalDependencyContractTestsFixtures.VendorSdk.Client> GetClients()
        {
            return new List<ExternalDependencyContractTestsFixtures.VendorSdk.Client>();
        }
    }

    public sealed class CoreTypeWithGenericOnlyInBody
    {
        public static void DoWork()
        {
            var clients = new List<ExternalDependencyContractTestsFixtures.VendorSdk.Client>();
            _ = clients.Count;
        }
    }

    // Method-body tokens inside a generic declaring type resolve against the declaring type's
    // generic arguments, so this fixture exercises a non-empty generic context in IL token
    // resolution (and its cache key).
    public sealed class CoreGenericTypeWithVendorCall<T>
    {
        public static void DoWork()
        {
            _ = new ExternalDependencyContractTestsFixtures.VendorSdk.Client();
        }

        public void UseValue(T value)
        {
            _ = value?.ToString();
        }
    }

    // Same, for a generic method's own arguments.
    public sealed class CoreTypeWithGenericMethodVendorCall
    {
        public static void DoWork<T>()
        {
            _ = new ExternalDependencyContractTestsFixtures.VendorSdk.Client();
            _ = default(T);
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
        public static void LogSomething()
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
