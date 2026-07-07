namespace InheritanceContractTestFixtures.Framework
{
    public class FrameworkBase;

    public class GenericRepository<T>;

    public interface IFrameworkLike;
}

namespace InheritanceContractTestFixtures.Framework.Prefixed
{
    public class PrefixedBase;
}

namespace InheritanceContractTestFixtures.Domain
{
    using InheritanceContractTestFixtures.Framework;
    using InheritanceContractTestFixtures.Framework.Prefixed;

    public class DirectViolation : FrameworkBase;

    public class TransitiveViolation : DirectViolation;

    public class GenericBaseViolation : GenericRepository<int>;

    public class PrefixViolation : PrefixedBase;

    public sealed class CleanDomainType;

    // Implements an interface whose full name is listed in forbidden_base_types; the inheritance
    // family must not treat interface implementation as inheritance.
    public sealed class InterfaceOnlyImplementer : IFrameworkLike;

    public sealed class Outer
    {
        public sealed class NestedViolation : FrameworkBase;
    }
}

namespace InheritanceContractTestFixtures.Infrastructure
{
    using InheritanceContractTestFixtures.Framework;

    public sealed class OutsideSourceSurface : FrameworkBase;
}
