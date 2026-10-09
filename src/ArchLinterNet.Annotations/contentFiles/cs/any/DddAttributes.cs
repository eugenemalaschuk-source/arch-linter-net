using System;

namespace ArchLinterNet.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
internal sealed class EntityAttribute : RoleAnnotationAttributeBase
{
    public EntityAttribute()
    {
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
internal sealed class AggregateRootAttribute : RoleAnnotationAttributeBase
{
    public AggregateRootAttribute()
    {
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
internal sealed class ValueObjectAttribute : RoleAnnotationAttributeBase
{
    public ValueObjectAttribute()
    {
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
internal sealed class DomainServiceAttribute : RoleAnnotationAttributeBase
{
    public DomainServiceAttribute()
    {
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
internal sealed class DomainEventAttribute : RoleAnnotationAttributeBase
{
    public DomainEventAttribute()
    {
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
internal sealed class RepositoryAttribute : RoleAnnotationAttributeBase
{
    public RepositoryAttribute()
    {
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
internal sealed class SpecificationAttribute : RoleAnnotationAttributeBase
{
    public SpecificationAttribute()
    {
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
internal sealed class FactoryAttribute : RoleAnnotationAttributeBase
{
    public FactoryAttribute()
    {
    }
}
