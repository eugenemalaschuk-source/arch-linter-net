using System;

namespace ArchLinterNet.Annotations
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class ViewAttribute : RoleAnnotationBaseAttribute
    {
        public ViewAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class ViewModelAttribute : RoleAnnotationBaseAttribute
    {
        public ViewModelAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class PresenterAttribute : RoleAnnotationBaseAttribute
    {
        public PresenterAttribute()
        {
        }
    }
}
