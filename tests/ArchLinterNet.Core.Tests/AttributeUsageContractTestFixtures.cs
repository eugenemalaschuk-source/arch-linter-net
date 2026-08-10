#pragma warning disable CS0649 // Fields exist only so the attribute usage scanner can discover them.
#pragma warning disable CS0169 // Fields exist only so the attribute usage scanner can discover them.
#pragma warning disable CS0067 // Event exists only so the attribute usage scanner can discover it.

namespace AttributeUsageContractTestFixtures.Markers
{
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public sealed class TestMarkerAttribute : Attribute;

    [AttributeUsage(AttributeTargets.All)]
    public sealed class SecondMarkerAttribute : Attribute;
}

namespace AttributeUsageContractTestFixtures.Markers.Prefixed
{
    [AttributeUsage(AttributeTargets.All)]
    public sealed class PrefixedMarkerAttribute : Attribute;
}

// Distinct from TestMarkerAttribute so glob-pattern tests over Modules.* fixtures below don't
// bleed into the closed-world "every TestMarker-tagged type in the assembly" assertions the
// tests above already make.
namespace AttributeUsageContractTestFixtures.ModuleMarkers
{
    [AttributeUsage(AttributeTargets.All)]
    public sealed class ModuleMarkerAttribute : Attribute;
}

namespace AttributeUsageContractTestFixtures.Allowed
{
    using AttributeUsageContractTestFixtures.Markers;

    [TestMarker]
    public sealed class AllowedHolder
    {
        [TestMarker]
        public AllowedHolder()
        {
        }

        [TestMarker]
        public int MarkedField;

        [TestMarker]
        public int MarkedProperty { get; set; }

        [TestMarker]
        public static void MarkedMethod()
        {
        }

        [TestMarker]
        public event EventHandler? MarkedEvent;
    }
}

namespace AttributeUsageContractTestFixtures.Wrong
{
    using AttributeUsageContractTestFixtures.Markers;
    using AttributeUsageContractTestFixtures.Markers.Prefixed;

    [TestMarker]
    public sealed class WrongHolder
    {
        [TestMarker]
        public WrongHolder()
        {
        }

        [TestMarker]
        private readonly int _markedPrivateField;

        [TestMarker]
        public static int MarkedMethodTarget() => 0;

        [TestMarker]
        public int MarkedProperty { get; set; }

        [PrefixedMarker]
        public int PrefixMatchedField;

        [TestMarker]
        [SecondMarker]
        public int DualMarkedField;

        [TestMarker]
        public event EventHandler? MarkedEvent;

        [TestMarker]
        public static void OverloadedMethod()
        {
        }

        [SecondMarker]
        public static void OverloadedMethod(int value)
        {
        }
    }
}

namespace AttributeUsageContractTestFixtures.Forbidden
{
    using AttributeUsageContractTestFixtures.Markers;

    [TestMarker]
    public sealed class ForbiddenHolder
    {
        [TestMarker]
        public int MarkedField;
    }
}

// Nested under an extra "module" segment so allowed_only_in_namespaces/forbidden_in_namespaces
// glob patterns such as "...Modules.*.Allowed"/"...Modules.*.Forbidden" (issue #443) have a
// middle segment to consume.
namespace AttributeUsageContractTestFixtures.Modules.Orders.Allowed
{
    using AttributeUsageContractTestFixtures.ModuleMarkers;

    [ModuleMarker]
    public sealed class OrdersAllowedHolder;
}

namespace AttributeUsageContractTestFixtures.Modules.Orders.Forbidden
{
    using AttributeUsageContractTestFixtures.ModuleMarkers;

    [ModuleMarker]
    public sealed class OrdersForbiddenHolder;
}

// Outside every Modules.*.Allowed/Modules.*.Forbidden glob boundary — the negative control for
// both glob-pattern tests.
namespace AttributeUsageContractTestFixtures.Modules.Orders.Other
{
    using AttributeUsageContractTestFixtures.ModuleMarkers;

    [ModuleMarker]
    public sealed class OrdersOtherHolder;
}

#pragma warning restore CS0649
#pragma warning restore CS0169
#pragma warning restore CS0067
