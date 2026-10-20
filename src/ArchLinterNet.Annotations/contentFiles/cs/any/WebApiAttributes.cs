using System;

namespace ArchLinterNet.Annotations
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class ControllerAttribute : RoleAnnotationBaseAttribute
    {
        public ControllerAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class EndpointAttribute : RoleAnnotationBaseAttribute
    {
        public EndpointAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class RequestDtoAttribute : RoleAnnotationBaseAttribute
    {
        public RequestDtoAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class ResponseDtoAttribute : RoleAnnotationBaseAttribute
    {
        public ResponseDtoAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class ApiContractAttribute : RoleAnnotationBaseAttribute
    {
        public ApiContractAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class ValidatorAttribute : RoleAnnotationBaseAttribute
    {
        public ValidatorAttribute()
        {
        }
    }
}
