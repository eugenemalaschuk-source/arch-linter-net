namespace ArchLinterNet.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
    AllowMultiple = false, Inherited = false)]
public sealed class FutureRoleAttribute : Attribute;
