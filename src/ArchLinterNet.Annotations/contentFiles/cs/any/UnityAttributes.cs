using System;

namespace ArchLinterNet.Annotations
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    internal sealed class UnityRuntimeAttribute : RoleAnnotationBaseAttribute
    {
        public UnityRuntimeAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    internal sealed class UnityEditorAttribute : RoleAnnotationBaseAttribute
    {
        public UnityEditorAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class MonoBehaviourAdapterAttribute : RoleAnnotationBaseAttribute
    {
        public MonoBehaviourAdapterAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class ScriptableObjectAssetAttribute : RoleAnnotationBaseAttribute
    {
        public ScriptableObjectAssetAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class InputAdapterAttribute : RoleAnnotationBaseAttribute
    {
        public InputAdapterAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    internal sealed class SceneAdapterAttribute : RoleAnnotationBaseAttribute
    {
        public SceneAdapterAttribute()
        {
        }
    }
}
