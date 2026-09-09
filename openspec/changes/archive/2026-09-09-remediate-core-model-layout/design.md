## Context

See [proposal.md](proposal.md). The existing audit convention classifies the following 24 public and internal model types as `Model` and requires their source files to be under a `Models` directory. The repository already uses that folder while retaining the `ArchLinterNet.Core.Model` namespace, so path and namespace are intentionally independent.

| Responsibility | Types | Current path | Target path |
| --- | --- | --- | --- |
| Waiver target identity | `ArchitectureWaiverTargetFingerprint` | `Core/Model/ArchitectureWaiverTargetFingerprint.cs` | `Core/Models/ArchitectureWaiverTargetFingerprint.cs` |
| Imported diagnostic result | `ImportedExternalDiagnosticProjection` | `Core/Model/ImportedExternalDiagnosticProjection.cs` | `Core/Models/ImportedExternalDiagnosticProjection.cs` |
| SARIF source evidence | `SarifEvidenceSourceSeverity`, `SarifEvidenceSourceDiagnostic`, `SarifEvidenceSourceFingerprint`, `SarifEvidenceSourceLocation`, `SarifEvidenceSourceRegion` | `Core/Model/SarifEvidenceSourceModels.cs` | `Core/Models/SarifEvidenceSourceModels.cs` |
| SARIF evidence result | `SarifEvidenceTrustStatus`, `SarifEvidenceArtifactReference`, `SarifEvidenceAssessmentContext`, `SarifEvidenceLimits`, `SarifEvidenceProducerContext`, `SarifEvidenceProvenance`, `SarifEvidenceReadResult`, `SarifEvidenceResolvedContext` | `Core/Model/SarifEvidenceModels.cs` | `Core/Models/SarifEvidenceModels.cs` |
| SARIF authorization | `SarifEvidenceAuthorizationSnapshot`, `SarifExternalDiagnosticFilterAuthorization` | `Core/Model/SarifEvidenceAuthorizationModels.cs` | `Core/Models/SarifEvidenceAuthorizationModels.cs` |
| SARIF external-diagnostic selection | `SarifExternalDiagnosticFilterDimension`, `SarifExternalDiagnosticFingerprintOrigin`, `SarifExternalDiagnosticGovernanceMode`, `SarifExternalDiagnosticFilterMismatch`, `SarifExternalDiagnosticFingerprint`, `SarifExternalDiagnosticSelectionResult`, `SarifSelectedExternalDiagnostic` | `Core/Model/SarifExternalDiagnosticSelectionModels.cs` | `Core/Models/SarifExternalDiagnosticSelectionModels.cs` |

## Frozen audit inventory

The following rows are the exact 24 frozen identity components before any source move. Every row has `identity_version: 2`, `contract_family: layout_conventions`, `kind: reference`, `source_assembly: null`, `source_member: null`, `target_assembly: null`, `target_type: layout-convention`, `occurrence: 0`, and `configuration: null`.

| Contract ID | Source type | Target member | Current path |
| --- | --- | --- | --- |
| `models-live-in-models-directories-class` | `ArchLinterNet.Core.Model.ArchitectureWaiverTargetFingerprint` | `type-kind:not class:Class` | `src/ArchLinterNet.Core/Model/ArchitectureWaiverTargetFingerprint.cs` |
| `models-live-in-models-directories-class` | `ArchLinterNet.Core.Model.ImportedExternalDiagnosticProjection` | `type-kind:not class:Class` | `src/ArchLinterNet.Core/Model/ImportedExternalDiagnosticProjection.cs` |
| `models-live-in-models-directories-enum` | `ArchLinterNet.Core.Model.SarifEvidenceSourceSeverity` | `type-kind:not enum:Enum` | `src/ArchLinterNet.Core/Model/SarifEvidenceSourceModels.cs` |
| `models-live-in-models-directories-enum` | `ArchLinterNet.Core.Model.SarifEvidenceTrustStatus` | `type-kind:not enum:Enum` | `src/ArchLinterNet.Core/Model/SarifEvidenceModels.cs` |
| `models-live-in-models-directories-enum` | `ArchLinterNet.Core.Model.SarifExternalDiagnosticFilterDimension` | `type-kind:not enum:Enum` | `src/ArchLinterNet.Core/Model/SarifExternalDiagnosticSelectionModels.cs` |
| `models-live-in-models-directories-enum` | `ArchLinterNet.Core.Model.SarifExternalDiagnosticFingerprintOrigin` | `type-kind:not enum:Enum` | `src/ArchLinterNet.Core/Model/SarifExternalDiagnosticSelectionModels.cs` |
| `models-live-in-models-directories-enum` | `ArchLinterNet.Core.Model.SarifExternalDiagnosticGovernanceMode` | `type-kind:not enum:Enum` | `src/ArchLinterNet.Core/Model/SarifExternalDiagnosticSelectionModels.cs` |
| `models-live-in-models-directories-record` | `ArchLinterNet.Core.Model.SarifEvidenceArtifactReference` | `type-kind:not record:Record` | `src/ArchLinterNet.Core/Model/SarifEvidenceModels.cs` |
| `models-live-in-models-directories-record` | `ArchLinterNet.Core.Model.SarifEvidenceAssessmentContext` | `type-kind:not record:Record` | `src/ArchLinterNet.Core/Model/SarifEvidenceModels.cs` |
| `models-live-in-models-directories-record` | `ArchLinterNet.Core.Model.SarifEvidenceAuthorizationSnapshot` | `type-kind:not record:Record` | `src/ArchLinterNet.Core/Model/SarifEvidenceAuthorizationModels.cs` |
| `models-live-in-models-directories-record` | `ArchLinterNet.Core.Model.SarifEvidenceLimits` | `type-kind:not record:Record` | `src/ArchLinterNet.Core/Model/SarifEvidenceModels.cs` |
| `models-live-in-models-directories-record` | `ArchLinterNet.Core.Model.SarifEvidenceProducerContext` | `type-kind:not record:Record` | `src/ArchLinterNet.Core/Model/SarifEvidenceModels.cs` |
| `models-live-in-models-directories-record` | `ArchLinterNet.Core.Model.SarifEvidenceProvenance` | `type-kind:not record:Record` | `src/ArchLinterNet.Core/Model/SarifEvidenceModels.cs` |
| `models-live-in-models-directories-record` | `ArchLinterNet.Core.Model.SarifEvidenceReadResult` | `type-kind:not record:Record` | `src/ArchLinterNet.Core/Model/SarifEvidenceModels.cs` |
| `models-live-in-models-directories-record` | `ArchLinterNet.Core.Model.SarifEvidenceResolvedContext` | `type-kind:not record:Record` | `src/ArchLinterNet.Core/Model/SarifEvidenceModels.cs` |
| `models-live-in-models-directories-record` | `ArchLinterNet.Core.Model.SarifEvidenceSourceDiagnostic` | `type-kind:not record:Record` | `src/ArchLinterNet.Core/Model/SarifEvidenceSourceModels.cs` |
| `models-live-in-models-directories-record` | `ArchLinterNet.Core.Model.SarifEvidenceSourceFingerprint` | `type-kind:not record:Record` | `src/ArchLinterNet.Core/Model/SarifEvidenceSourceModels.cs` |
| `models-live-in-models-directories-record` | `ArchLinterNet.Core.Model.SarifEvidenceSourceLocation` | `type-kind:not record:Record` | `src/ArchLinterNet.Core/Model/SarifEvidenceSourceModels.cs` |
| `models-live-in-models-directories-record` | `ArchLinterNet.Core.Model.SarifEvidenceSourceRegion` | `type-kind:not record:Record` | `src/ArchLinterNet.Core/Model/SarifEvidenceSourceModels.cs` |
| `models-live-in-models-directories-record` | `ArchLinterNet.Core.Model.SarifExternalDiagnosticFilterAuthorization` | `type-kind:not record:Record` | `src/ArchLinterNet.Core/Model/SarifEvidenceAuthorizationModels.cs` |
| `models-live-in-models-directories-record` | `ArchLinterNet.Core.Model.SarifExternalDiagnosticFilterMismatch` | `type-kind:not record:Record` | `src/ArchLinterNet.Core/Model/SarifExternalDiagnosticSelectionModels.cs` |
| `models-live-in-models-directories-record` | `ArchLinterNet.Core.Model.SarifExternalDiagnosticFingerprint` | `type-kind:not record:Record` | `src/ArchLinterNet.Core/Model/SarifExternalDiagnosticSelectionModels.cs` |
| `models-live-in-models-directories-record` | `ArchLinterNet.Core.Model.SarifExternalDiagnosticSelectionResult` | `type-kind:not record:Record` | `src/ArchLinterNet.Core/Model/SarifExternalDiagnosticSelectionModels.cs` |
| `models-live-in-models-directories-record` | `ArchLinterNet.Core.Model.SarifSelectedExternalDiagnostic` | `type-kind:not record:Record` | `src/ArchLinterNet.Core/Model/SarifExternalDiagnosticSelectionModels.cs` |

## Goals / Non-Goals

**Goals:**

- Remove exactly the frozen 24 Core/Model audit identities through source conformance.
- Keep the `ArchLinterNet.Core.Model` namespace and the compiled public API unchanged.
- Demonstrate that the policy still detects a model intentionally placed outside `Models`.

**Non-Goals:**

- Change the policy, exclusions, audit/strict modes, waiver baseline, or layout checker.
- Rename, split, reshape, or alter serialization of any moved type.
- Address the three History diagnostics owned by #820 or declaration-count debt owned by other issues.

## Decisions

### Move whole source files without namespace edits

The six source files move intact to the existing `Core/Models` directory, retaining their namespaces and contents. This is the smallest coherent change: the layout checker evaluates source paths, while callers and serialized names depend on namespaces and type declarations.

Changing namespaces would create widespread `using` churn and public-API/schema risk without contributing to the layout requirement. Splitting type groups into one file per type would similarly broaden the change and is not required by the policy.

### Add a narrow self-policy regression

Add or extend the repository self-policy layout test fixture to assert both sides of the convention: moved production files are free of the 24 owned canonical identities, and a fixture model outside `Models` still produces the applicable audit diagnostic. This proves conformance was obtained by movement rather than a policy bypass.

Relying solely on `make lint-architecture` would establish the repository's aggregate state but would not give a focused regression for this specific directory contract.

### Preserve shared coordination artifacts unless a minimal status update is required

The audit-policy file must remain unchanged. The active `decompose-god-classes` task list is a shared serial coordination file; update only its #819 status hunk if the existing wording has an exact place for this completed slice. The new change's artifacts hold the authoritative scope and evidence for this PR.

## Risks / Trade-offs

- **A physical move is accidentally seen as an API change by snapshot tooling** → retain namespaces, run the Core public-API check, and inspect its diff.
- **The audit count changes for unrelated moving-main work** → compare diagnostics by the frozen canonical identity set and report only the #819 paths.
- **A broad test fixture masks the policy change** → use an isolated misplaced-model fixture and assert its exact layout contract identity.
- **Concurrent remediation edits conflict in shared OpenSpec files** → make only a minimal task-hunk update after integration, then rebase before opening the PR.

## Migration Plan

No deployment or data migration is needed. Move the files, run focused tests and architecture checks, archive the change after synchronization, and submit the branch. Reverting the commit restores the prior source paths without runtime-data impact.
