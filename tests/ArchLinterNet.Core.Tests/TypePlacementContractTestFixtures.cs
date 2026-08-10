namespace TypePlacementContractTestFixtures.Correct
{
    public sealed class SampleController;

    public sealed class SampleService;
}

namespace TypePlacementContractTestFixtures.Wrong
{
    public sealed class SampleController;

    public sealed class SampleHandlerImpl;

    public sealed class SampleHandler;
}

// Nested under an extra "module" segment so must_reside_in_namespaces glob patterns such as
// "...Modules.*.Correct" (issue #443) have a middle segment to consume. Named with a "Worker"
// suffix (not "Controller"/"Handler"/"Service") so it stays outside every other test's broad
// name_suffix selector over this fixtures root.
namespace TypePlacementContractTestFixtures.Modules.Orders.Correct
{
    public sealed class OrdersWorker;
}

namespace TypePlacementContractTestFixtures.Roles
{
    public class RoleBase;

    public sealed class RoleDerived : RoleBase;

    public sealed class UnrelatedType;

    public interface IRoleMarker;

    public sealed class RoleImplementer : IRoleMarker;

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RoleMarkerAttribute : Attribute;

    [RoleMarker]
    public sealed class RoleMarkedType;

    public sealed class RoleUnmarkedType;
}
