namespace AttributeRoleExtractionTestFixtures
{
    public enum Tier
    {
        Core = 1,
        Domain = 2
    }

    public enum AliasedTier
    {
        First = 1,
        Second = 1
    }

    public enum UlongTier : ulong
    {
        Max = ulong.MaxValue
    }

    public static class Constants
    {
        public const string Owner = "platform-team";
        public const int Priority = 5;
        public const decimal Ratio = 1.5m;
        public static readonly string NotConst = "not-const";
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class DomainMarkerAttribute : Attribute
    {
        public DomainMarkerAttribute(string domain, string module = "UnknownModule")
        {
            Domain = domain;
            Module = module;
        }

        public string Domain { get; }

        public string Module { get; set; }

        public bool Enabled { get; set; }

        public Tier Tier { get; set; }

        public AliasedTier AliasTier { get; set; }

        public UlongTier UlongValue { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class SecondMarkerAttribute : Attribute;

    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class BoundedContextMarkerAttribute : Attribute
    {
        public BoundedContextMarkerAttribute(string context)
        {
            Context = context;
        }

        public string Context { get; }
    }

    [DomainMarker("Sales")]
    public sealed class TypeWithConstructorDefault;

    [DomainMarker("Sales", Module = "Checkout")]
    public sealed class TypeWithSuppliedNamedProperty;

    [DomainMarker("Sales")]
    public sealed class TypeWithUnsuppliedNamedProperty;

    [DomainMarker("Sales", Enabled = true)]
    public sealed class TypeWithBooleanProperty;

    [DomainMarker("Sales", Tier = Tier.Domain)]
    public sealed class TypeWithEnumProperty;

    [DomainMarker("Sales", AliasTier = AliasedTier.First)]
    public sealed class TypeWithAliasedEnumProperty;

    [DomainMarker("Sales", UlongValue = UlongTier.Max)]
    public sealed class TypeWithUlongMaxEnumProperty;

    public sealed class PlainType;

    [DomainMarker("Sales")]
    [SecondMarker]
    public sealed class TypeWithConflictingEntries;

    [DomainMarker("Sales")]
    [DomainMarker("Sales")]
    public sealed class TypeWithIdenticalRepeatedInstances;

    [DomainMarker("Sales")]
    [DomainMarker("Marketing")]
    public sealed class TypeWithDifferingRepeatedInstances;

    public sealed class TypeRelyingOnAssemblyAttribute;

    [DomainMarker("Sales")]
    public sealed class TypeOverridingAssemblyAttribute;
}
