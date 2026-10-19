using System;

namespace ArchLinterNet.Annotations
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class PortAttribute : RoleAnnotationBaseAttribute
    {
        public PortAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class AdapterAttribute : RoleAnnotationBaseAttribute
    {
        public AdapterAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class AntiCorruptionLayerAttribute : RoleAnnotationBaseAttribute
    {
        public AntiCorruptionLayerAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class DbContextAttribute : RoleAnnotationBaseAttribute
    {
        public DbContextAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class RepositoryImplementationAttribute : RoleAnnotationBaseAttribute
    {
        public RepositoryImplementationAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class ExternalClientAttribute : RoleAnnotationBaseAttribute
    {
        public ExternalClientAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class MessageBusAdapterAttribute : RoleAnnotationBaseAttribute
    {
        public MessageBusAdapterAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class FileSystemAdapterAttribute : RoleAnnotationBaseAttribute
    {
        public FileSystemAdapterAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class ClockAdapterAttribute : RoleAnnotationBaseAttribute
    {
        public ClockAdapterAttribute()
        {
        }
    }
}
