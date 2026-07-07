namespace ArchLinterNet.Core.Model;

public enum ArchitectureDiagnosticKind
{
    Dependency,
    Cycle,
    UnmatchedIgnore,
    Configuration,
    ExternalDependency,
    PolicyConsistency,
    PackageDependency,
    TypePlacement,
    PublicApiSurface,
    AttributeUsage,
    Inheritance,
    InterfaceImplementation,
    Composition
}
