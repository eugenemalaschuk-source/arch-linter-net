using System;

namespace ArchLinterNet.Annotations
{

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class EntityAttribute : RoleAnnotationBaseAttribute
    {
        public EntityAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class AggregateRootAttribute : RoleAnnotationBaseAttribute
    {
        public AggregateRootAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class ValueObjectAttribute : RoleAnnotationBaseAttribute
    {
        public ValueObjectAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class DomainServiceAttribute : RoleAnnotationBaseAttribute
    {
        public DomainServiceAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class DomainEventAttribute : RoleAnnotationBaseAttribute
    {
        public DomainEventAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class RepositoryAttribute : RoleAnnotationBaseAttribute
    {
        public RepositoryAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class SpecificationAttribute : RoleAnnotationBaseAttribute
    {
        public SpecificationAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class FactoryAttribute : RoleAnnotationBaseAttribute
    {
        public FactoryAttribute()
        {
        }
    }
}
