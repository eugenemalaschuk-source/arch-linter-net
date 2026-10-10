namespace ArchLinterNet.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
    AllowMultiple = false, Inherited = false)]
public sealed class DomainAttribute : Attribute
{
    public string? Value { get; set; }
}

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public sealed class AnnotationCatalogIdentityAttribute(int catalogGeneration, string packageVersion) : Attribute
{
    public AnnotationCatalogIdentityAttribute(string packageVersion) : this(1, packageVersion)
    {
    }

    public int CatalogGeneration { get; } = catalogGeneration;

    public string PackageVersion { get; } = packageVersion;
}

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public sealed class UnityRuntimeAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
    AllowMultiple = false, Inherited = false)]
public sealed class FutureRoleAttribute : Attribute;
