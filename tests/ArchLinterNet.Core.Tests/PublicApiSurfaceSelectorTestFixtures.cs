namespace PublicApiSurfaceSelectorTestFixtures
{
    // Orthogonal user-owned API marker (issue #525's primary adoption path) — deliberately unrelated
    // to any semantic role attribute below, so selecting a type through it never implies a role.
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PublicApiContractAttribute : Attribute;

    // Classification attribute mapped (in test setup) to Role "ValueObject" — proves a type selected
    // via PublicApiContractAttribute keeps its own, independently-assigned semantic role.
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ValueObjectRoleAttribute : Attribute;

    // Classification attribute mapped (in test setup) to Role "ApiContract" — the semantic-role
    // selector path (surface_selector.role), used with no structural marker at all.
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ApiContractRoleAttribute : Attribute;

    public abstract class ApiBase
    {
    }

    public interface IApiMarker
    {
    }

    // An incidental exported implementation type: nothing selects it, so it must disappear from a
    // selector-restricted governed surface entirely (not merely pass as "declared").
    public sealed class IncidentalType
    {
        public IncidentalType()
        {
        }

        public int Value { get; set; }
    }

    [PublicApiContract]
    [ValueObjectRole]
    public sealed class SelectedByAttribute
    {
        public SelectedByAttribute()
        {
        }

        public int Value { get; set; }
    }

    public sealed class SelectedByBaseType : ApiBase
    {
        public SelectedByBaseType()
        {
        }

        public int Value { get; set; }
    }

    public sealed class SelectedByInterface : IApiMarker
    {
        public SelectedByInterface()
        {
        }

        public int Value { get; set; }
    }

    [ApiContractRole]
    public sealed class SelectedByRole
    {
        public SelectedByRole()
        {
        }

        public int Value { get; set; }
    }

    // Selected via PublicApiContractAttribute; its own member signature references IncidentalType, a
    // first-party exported type from the same assembly that no selector in these tests selects.
    [PublicApiContract]
    public sealed class SelectedWithEscapingDependency
    {
        public SelectedWithEscapingDependency()
        {
        }

        public IncidentalType GetIncidental()
        {
            return new IncidentalType();
        }
    }
}

namespace PublicApiSurfaceSelectorTestFixtures.PublicSurface
{
    // Selected via a namespace-prefix matcher, proving the feature is not attribute-specific.
    public sealed class SelectedByNamespace
    {
        public SelectedByNamespace()
        {
        }

        public int Value { get; set; }
    }
}
