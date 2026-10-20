using System;

namespace ArchLinterNet.Annotations
{

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    internal sealed class DomainLayerAttribute : RoleAnnotationBaseAttribute
    {
        public DomainLayerAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    internal sealed class ApplicationLayerAttribute : RoleAnnotationBaseAttribute
    {
        public ApplicationLayerAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    internal sealed class InfrastructureLayerAttribute : RoleAnnotationBaseAttribute
    {
        public InfrastructureLayerAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    internal sealed class PresentationLayerAttribute : RoleAnnotationBaseAttribute
    {
        public PresentationLayerAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    internal sealed class ApiLayerAttribute : RoleAnnotationBaseAttribute
    {
        public ApiLayerAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    internal sealed class PersistenceLayerAttribute : RoleAnnotationBaseAttribute
    {
        public PersistenceLayerAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    internal sealed class IntegrationLayerAttribute : RoleAnnotationBaseAttribute
    {
        public IntegrationLayerAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    internal sealed class SharedKernelAttribute : RoleAnnotationBaseAttribute
    {
        public SharedKernelAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    internal sealed class CompositionRootAttribute : RoleAnnotationBaseAttribute
    {
        public CompositionRootAttribute()
        {
        }
    }
}
