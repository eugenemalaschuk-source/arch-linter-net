using System;

namespace ArchLinterNet.Annotations
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class CommandAttribute : RoleAnnotationAttributeBase
    {
        public CommandAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class CommandHandlerAttribute : RoleAnnotationAttributeBase
    {
        public CommandHandlerAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class QueryAttribute : RoleAnnotationAttributeBase
    {
        public QueryAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class QueryHandlerAttribute : RoleAnnotationAttributeBase
    {
        public QueryHandlerAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class EventAttribute : RoleAnnotationAttributeBase
    {
        public EventAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class EventHandlerAttribute : RoleAnnotationAttributeBase
    {
        public EventHandlerAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class IntegrationEventAttribute : RoleAnnotationAttributeBase
    {
        public IntegrationEventAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class ReadModelAttribute : RoleAnnotationAttributeBase
    {
        public ReadModelAttribute()
        {
        }
    }
}
