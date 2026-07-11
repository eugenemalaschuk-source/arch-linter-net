namespace AttributeRoleExtractionTestFixtures
{
    public enum Tier
    {
        Core = 1,
        Domain = 2
    }

#pragma warning disable CA1069 // Intentional: exercises the extractor's aliased-enum evidence-extraction-failure path.
    public enum AliasedTier
    {
        First = 1,
        Second = 1
    }
#pragma warning restore CA1069

    public enum UlongTier : ulong
    {
        Max = ulong.MaxValue
    }

    public static class Constants
    {
        public const string Owner = "platform-team";
        public const int Priority = 5;
        public const decimal Ratio = 1.5m;
        public const string? NullOwner = null;
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

        public DomainMarkerAttribute(Type moduleType)
        {
            Domain = string.Empty;
            Module = string.Empty;
            ModuleType = moduleType;
        }

        public string Domain { get; }

        public string Module { get; set; }

        public bool Enabled { get; set; }

        public Tier Tier { get; set; }

        public AliasedTier AliasTier { get; set; }

        public UlongTier UlongValue { get; set; }

        public Type? ModuleType { get; }

        public float FloatValue { get; set; }

        public double DoubleValue { get; set; }
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

    [DomainMarker(typeof(PlainType))]
    public sealed class TypeWithTypeConstructorArgument;

    [DomainMarker("Sales", FloatValue = float.NaN)]
    public sealed class TypeWithNonFiniteFloatProperty;

    [DomainMarker("Sales", FloatValue = float.MaxValue)]
    public sealed class TypeWithOverflowingFloatProperty;

    [DomainMarker("Sales", DoubleValue = double.NaN)]
    public sealed class TypeWithNonFiniteDoubleProperty;

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

    [DomainMarker("Sales")]
    [DomainMarker("Marketing")]
    [DomainMarker("Engineering")]
    public sealed class TypeWithThreeDifferingRepeatedInstances;

    public sealed class TypeRelyingOnAssemblyAttribute;

    [DomainMarker("Sales")]
    public sealed class TypeOverridingAssemblyAttribute;
}
