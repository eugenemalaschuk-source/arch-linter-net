// Fake service-locator/DI-registration surface used to exercise composition contracts without
// depending on real BCL/ASP.NET/container members (which are not guaranteed to be forbidden-list
// friendly targets and would couple these tests to external assemblies). The IL scanner matches by
// member/type name and namespace prefix, so a local fake with the same shape is sufficient.
namespace CompositionContractTestFixtures.Fakes
{
    public interface IFakeServiceProvider
    {
        object? GetService(System.Type serviceType);
    }

    public interface IFakeServiceCollection
    {
        void AddSingleton(System.Type serviceType);
    }

    public interface IFakeAspNetServiceCollection
    {
    }

    public static class FakeServiceCollectionServiceExtensions
    {
        public static void AddSingleton(this IFakeAspNetServiceCollection services, System.Type serviceType)
        {
        }
    }

    // Mirrors a Unity/VContainer-style container API surface (Resolve/Register) distinct from the
    // ASP.NET-shaped fakes above, so tests can exercise both example call shapes named in the issue.
    public sealed class FakeContainer
    {
        public object? Resolve(System.Type serviceType) => null; // NOSONAR — instance required for IL scanner tests

        public void Register(System.Type serviceType) // NOSONAR — instance required for IL scanner tests
        {
        }
    }
}

// Types inside the declared composition boundary: calling the forbidden APIs here must not
// produce a violation.
namespace CompositionContractTestFixtures.Composition
{
    using CompositionContractTestFixtures.Fakes;

    public sealed class CompositionRoot
    {
        public static void ConfigureServices(IFakeServiceCollection services)
        {
            services.AddSingleton(typeof(object));
        }

        public static void ConfigureAspNetServices(IFakeAspNetServiceCollection services)
        {
            services.AddSingleton(typeof(object));
        }

        public static object? ResolveFromLocator(IFakeServiceProvider provider)
        {
            return provider.GetService(typeof(object));
        }
    }

    public sealed class ContainerBootstrap
    {
        public static object? Resolve(FakeContainer container)
        {
            container.Register(typeof(object));
            return container.Resolve(typeof(object));
        }
    }
}

// Types outside the declared composition boundary: calling the same forbidden APIs here must
// produce a violation.
namespace CompositionContractTestFixtures.Application
{
    using CompositionContractTestFixtures.Fakes;

    public sealed class ServiceLocatorLeak
    {
        public static object? ResolveFromLocator(IFakeServiceProvider provider)
        {
            return provider.GetService(typeof(object));
        }
    }

    public sealed class DiRegistrationLeak
    {
        public static void ConfigureServices(IFakeServiceCollection services)
        {
            services.AddSingleton(typeof(object));
        }
    }

    public sealed class AspNetDiRegistrationLeak
    {
        public static void ConfigureServices(IFakeAspNetServiceCollection services)
        {
            services.AddSingleton(typeof(object));
        }
    }

    // A Unity/VContainer-style container Resolve/Register call made outside the composition
    // boundary, exercising the "container API example" scenario distinctly from the ASP.NET-shaped
    // service-locator leak above.
    public sealed class ContainerLeak
    {
        public static object? Resolve(FakeContainer container)
        {
            container.Register(typeof(object));
            return container.Resolve(typeof(object));
        }
    }

    public sealed class CleanApplicationType
    {
        public static int Add(int a, int b) => a + b;
    }
}
