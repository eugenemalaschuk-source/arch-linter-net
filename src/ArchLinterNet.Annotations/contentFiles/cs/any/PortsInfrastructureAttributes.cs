using System;

namespace ArchLinterNet.Annotations
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class PortAttribute : RoleAnnotationAttributeBase
    {
        public PortAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class AdapterAttribute : RoleAnnotationAttributeBase
    {
        public AdapterAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class AntiCorruptionLayerAttribute : RoleAnnotationAttributeBase
    {
        public AntiCorruptionLayerAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class DbContextAttribute : RoleAnnotationAttributeBase
    {
        public DbContextAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class RepositoryImplementationAttribute : RoleAnnotationAttributeBase
    {
        public RepositoryImplementationAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class ExternalClientAttribute : RoleAnnotationAttributeBase
    {
        public ExternalClientAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class MessageBusAdapterAttribute : RoleAnnotationAttributeBase
    {
        public MessageBusAdapterAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class FileSystemAdapterAttribute : RoleAnnotationAttributeBase
    {
        public FileSystemAdapterAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class ClockAdapterAttribute : RoleAnnotationAttributeBase
    {
        public ClockAdapterAttribute()
        {
        }
    }
}
