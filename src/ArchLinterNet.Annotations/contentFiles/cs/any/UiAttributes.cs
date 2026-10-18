using System;

namespace ArchLinterNet.Annotations
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class ViewAttribute : RoleAnnotationAttributeBase
    {
        public ViewAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class ViewModelAttribute : RoleAnnotationAttributeBase
    {
        public ViewModelAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class PresenterAttribute : RoleAnnotationAttributeBase
    {
        public PresenterAttribute()
        {
        }
    }
}
