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
