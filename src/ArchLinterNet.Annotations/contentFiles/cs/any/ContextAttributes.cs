using System;

namespace ArchLinterNet.Annotations
{

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Assembly,
        AllowMultiple = false, Inherited = false)]
    internal sealed class DomainAttribute : Attribute
    {
        public DomainAttribute(string value)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Assembly,
        AllowMultiple = false, Inherited = false)]
    internal sealed class BoundedContextAttribute : Attribute
    {
        public BoundedContextAttribute(string value)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Assembly,
        AllowMultiple = false, Inherited = false)]
    internal sealed class ModuleAttribute : Attribute
    {
        public ModuleAttribute(string value)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Assembly,
        AllowMultiple = false, Inherited = false)]
    internal sealed class FeatureAttribute : Attribute
    {
        public FeatureAttribute(string value)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Assembly,
        AllowMultiple = false, Inherited = false)]
    internal sealed class PlatformAttribute : Attribute
    {
        public PlatformAttribute(string value)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Assembly,
        AllowMultiple = false, Inherited = false)]
    internal sealed class RuntimeAttribute : Attribute
    {
        public RuntimeAttribute(string value)
        {
        }
    }
}
