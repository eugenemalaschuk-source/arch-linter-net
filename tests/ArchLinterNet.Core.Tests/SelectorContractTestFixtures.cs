using AttributeRoleExtractionTestFixtures;

namespace SelectorCycleFixtures.Domain
{
    [DomainMarker("Sales", Enabled = true)]
    public sealed class SelectedDomainNode
    {
        public SelectorCycleFixtures.ApplicationFalseEdge.FalseEdgeNode FalseEdge { get; } = null!;
        public SelectorCycleFixtures.ApplicationSelector.ApplicationNode SelectorCycle { get; } = null!;
    }

    public sealed class NonSelectedDomainNode;
}

namespace SelectorCycleFixtures.ApplicationSelector
{
    public sealed class ApplicationNode
    {
        public SelectorCycleFixtures.Domain.SelectedDomainNode Domain { get; } = null!;
    }
}

namespace SelectorCycleFixtures.ApplicationFalseEdge
{
    public sealed class FalseEdgeNode
    {
        public SelectorCycleFixtures.Domain.NonSelectedDomainNode Domain { get; } = null!;
    }
}

namespace AttributeRoleExtractionTestFixtures
{
    public sealed class UnclassifiedFixtureConsumer
    {
        public TypeWithBooleanProperty Target { get; } = null!;
    }
}
