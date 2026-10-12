using System;

namespace ArchLinterNet.Annotations
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class ControllerAttribute : RoleAnnotationAttributeBase
    {
        public ControllerAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class EndpointAttribute : RoleAnnotationAttributeBase
    {
        public EndpointAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class RequestDtoAttribute : RoleAnnotationAttributeBase
    {
        public RequestDtoAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class ResponseDtoAttribute : RoleAnnotationAttributeBase
    {
        public ResponseDtoAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class ApiContractAttribute : RoleAnnotationAttributeBase
    {
        public ApiContractAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class ValidatorAttribute : RoleAnnotationAttributeBase
    {
        public ValidatorAttribute()
        {
        }
    }
}
