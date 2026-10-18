using System;

namespace ArchLinterNet.Annotations
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    internal sealed class UnityRuntimeAttribute : RoleAnnotationAttributeBase
    {
        public UnityRuntimeAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    internal sealed class UnityEditorAttribute : RoleAnnotationAttributeBase
    {
        public UnityEditorAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class MonoBehaviourAdapterAttribute : RoleAnnotationAttributeBase
    {
        public MonoBehaviourAdapterAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class ScriptableObjectAssetAttribute : RoleAnnotationAttributeBase
    {
        public ScriptableObjectAssetAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class InputAdapterAttribute : RoleAnnotationAttributeBase
    {
        public InputAdapterAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class SceneAdapterAttribute : RoleAnnotationAttributeBase
    {
        public SceneAdapterAttribute()
        {
        }
    }
}
