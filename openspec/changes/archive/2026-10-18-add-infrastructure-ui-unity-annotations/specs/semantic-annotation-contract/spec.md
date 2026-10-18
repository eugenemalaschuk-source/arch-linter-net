## ADDED Requirements

### Requirement: Ports, infrastructure, UI, and Unity role attributes follow the reviewed catalog

The source-only annotation set SHALL provide internal sealed role attributes for every `ports-infrastructure`, `ui`, and `unity-client` role in `architecture/semantic-annotations.v1.json`: `Port`, `Adapter`, `AntiCorruptionLayer`, `DbContext`, `RepositoryImplementation`, `ExternalClient`, `MessageBusAdapter`, `FileSystemAdapter`, `ClockAdapter`, `View`, `ViewModel`, `Presenter`, `UnityRuntime`, `UnityEditor`, `MonoBehaviourAdapter`, `ScriptableObjectAsset`, `InputAdapter`, and `SceneAdapter`. Each attribute SHALL use its exact manifest FQN and targets, reject multiple uses and inheritance, expose a public parameterless constructor, and inherit only the six bounded string metadata properties defined by the role-attribute contract. `UnityRuntime` and `UnityEditor` SHALL be assembly-targeted only; all other roles in these families SHALL be type-targeted only. The definitions SHALL not reference or infer behavior from infrastructure, UI, or Unity framework types.

#### Scenario: Ports/infrastructure and UI role definitions match the manifest

- **WHEN** the annotation source contract is checked against the manifest
- **THEN** all nine `ports-infrastructure` roles and all three `ui` roles are defined under their exact FQNs with the manifest targets and the reviewed role-attribute contract

#### Scenario: Unity roles retain static scope boundaries

- **WHEN** the annotation source contract is checked against the manifest
- **THEN** `UnityRuntime` and `UnityEditor` target assemblies only, while `MonoBehaviourAdapter`, `ScriptableObjectAsset`, `InputAdapter`, and `SceneAdapter` target types only, with no Unity runtime dependency

#### Scenario: The approved source set compiles in the C# 9 consumer

- **WHEN** an SDK-style consumer compiles the annotation source set using the repository's C# 9 smoke project
- **THEN** representative ports/infrastructure, UI, and Unity/client annotations compile without framework references or source changes
