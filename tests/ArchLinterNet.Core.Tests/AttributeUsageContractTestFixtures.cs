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
        public void MarkedMethod()
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
        private readonly int _markedPrivateField;

        [TestMarker]
        public int MarkedMethodTarget() => 0;

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
        public void OverloadedMethod()
        {
        }

        [SecondMarker]
        public void OverloadedMethod(int value)
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

#pragma warning restore CS0649
#pragma warning restore CS0169
#pragma warning restore CS0067
