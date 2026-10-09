using System;

namespace ArchLinterNet.Annotations
{

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    internal sealed class DomainLayerAttribute : RoleAnnotationAttributeBase
    {
        public DomainLayerAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    internal sealed class ApplicationLayerAttribute : RoleAnnotationAttributeBase
    {
        public ApplicationLayerAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    internal sealed class InfrastructureLayerAttribute : RoleAnnotationAttributeBase
    {
        public InfrastructureLayerAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    internal sealed class PresentationLayerAttribute : RoleAnnotationAttributeBase
    {
        public PresentationLayerAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    internal sealed class ApiLayerAttribute : RoleAnnotationAttributeBase
    {
        public ApiLayerAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    internal sealed class PersistenceLayerAttribute : RoleAnnotationAttributeBase
    {
        public PersistenceLayerAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    internal sealed class IntegrationLayerAttribute : RoleAnnotationAttributeBase
    {
        public IntegrationLayerAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    internal sealed class SharedKernelAttribute : RoleAnnotationAttributeBase
    {
        public SharedKernelAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    internal sealed class CompositionRootAttribute : RoleAnnotationAttributeBase
    {
        public CompositionRootAttribute()
        {
        }
    }
}
