// Fixtures for the two acceptance scenarios in issue #356: a minimal single-project policy
// (Product.* excluding Product.Generated.*) and a multi-module policy proving a persistence SDK
// external-dependency rule can be allowed in a module's owned Infrastructure/Persistence layers
// while remaining blocked in that module's Domain/Application layers, via layer namespace
// exclusion rather than per-module enumeration.

namespace LayerExclusionAcceptanceFixtures.Product.Core
{
    public sealed class CoreType;
}

namespace LayerExclusionAcceptanceFixtures.Product.Generated
{
    public sealed class GeneratedType;
}

namespace LayerExclusionAcceptanceFixtures.Modules.Weather.Domain
{
    public sealed class WeatherAggregateUsingVendorSdk
    {
        public LayerExclusionAcceptanceFixtures.VendorPersistenceSdk.Client Client { get; } = new();
    }
}

namespace LayerExclusionAcceptanceFixtures.Modules.Weather.Application
{
    public sealed class WeatherService;
}

namespace LayerExclusionAcceptanceFixtures.Modules.Weather.Infrastructure
{
    public sealed class WeatherPersistenceAdapter
    {
        public LayerExclusionAcceptanceFixtures.VendorPersistenceSdk.Client Client { get; } = new();
    }
}

namespace LayerExclusionAcceptanceFixtures.Modules.Weather.Persistence
{
    public sealed class WeatherRepository
    {
        public LayerExclusionAcceptanceFixtures.VendorPersistenceSdk.Client Client { get; } = new();
    }
}

namespace LayerExclusionAcceptanceFixtures.VendorPersistenceSdk
{
    public sealed class Client;
}
