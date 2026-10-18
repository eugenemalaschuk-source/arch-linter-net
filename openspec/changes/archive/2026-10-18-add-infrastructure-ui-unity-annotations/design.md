## Context

The reviewed manifest already defines the exact FQNs, scopes, and targets for all 49 first-class v0.10 roles. The source-only content currently provides the layered/clean, DDD, CQRS/message, web/API, and context attributes; it is missing the 18 approved roles owned by #571. The C# 9 consumer project compiles the complete content source set, and the Core source-contract tests compare reflected attributes to the manifest.

## Goals / Non-Goals

**Goals:**

- Define the nine `ports-infrastructure` roles, three `ui` roles, and six `unity-client` roles already present in the manifest.
- Preserve internal sealed attribute types, exact FQNs and targets, public parameterless constructors, and the six inherited bounded metadata properties.
- Keep all definitions framework-neutral and compilable by the existing C# 9 consumer.

**Non-Goals:**

- Changing the manifest, package distribution, Core recognition, role resolution, metadata composition, or policy authority.
- Adding attributes for catalog roles marked custom-mapping-only or deferred, including `TelemetryAdapter`, `PersistenceModel`, `Migration`, `Component`, `NavigationService`, and `Installer`.
- Inspecting Unity or infrastructure framework runtime behavior.

## Decisions

1. **Use the manifest as the identity and target authority.** Add exactly `Port`, `Adapter`, `AntiCorruptionLayer`, `DbContext`, `RepositoryImplementation`, `ExternalClient`, `MessageBusAdapter`, `FileSystemAdapter`, `ClockAdapter`, `View`, `ViewModel`, `Presenter`, `UnityRuntime`, `UnityEditor`, `MonoBehaviourAdapter`, `ScriptableObjectAsset`, `InputAdapter`, and `SceneAdapter`. This avoids expanding the approved vocabulary based on candidate names in the issue.
2. **Reuse the existing role base and source conventions.** Each definition remains in `ArchLinterNet.Annotations`, is internal and sealed, uses `AllowMultiple=false` and `Inherited=false`, and has a public parameterless constructor. Role attributes inherit the six string metadata properties from `RoleAnnotationAttributeBase`.
3. **Match targets exactly.** `UnityRuntime` and `UnityEditor` are assembly-only. The other sixteen roles target classes, structs, and interfaces. No role attribute references a Unity, database, UI, or transport framework type.
4. **Keep family files and validation aligned.** Add separate source files for ports/infrastructure, UI, and Unity/client. Extend manifest-driven reflection checks for family counts, FQNs, targets, visibility, constructors, and metadata properties, and add representative consumer declarations to the existing C# 9 smoke project.

## Risks / Trade-offs

- **[Risk] Source definitions drift from the reviewed manifest.** → The Core source-contract test enumerates each family from the manifest and validates every reflected definition and target.
- **[Risk] A Unity/editor marker accidentally becomes type-targeted or implies runtime behavior.** → Keep those two attributes assembly-only and use only the static role names already in the manifest; the smoke consumer requires no Unity assemblies.

## Migration Plan

No migration is required. These source definitions are not shipped until the separate #567 package lifecycle authorizes distribution.

## Open Questions

None. The manifest and closed #566 contract settle identities, scope, targets, metadata, and visibility.
