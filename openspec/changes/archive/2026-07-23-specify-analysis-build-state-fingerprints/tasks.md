## 1. Design contract

- [x] 1.1 Define the `analysis-build-state/v1` canonical envelope, digest algorithm, serialization, ordering, and versioning rules.
- [x] 1.2 Define project-evaluation, build-input, analysis-input, expected-output, verified-artifact, completed-session, and Testing snapshot identity layers.
- [x] 1.3 Separate build identity, analysis/session identity, validation evidence, display-only evidence, and intentionally excluded machine/process fields.
- [x] 1.4 Define portable repository-relative path normalization, case handling, symlink/junction containment, and typed external coordinates.

## 2. Project/build verification semantics

- [x] 2.1 Define the minimum MSBuild/compiler/source/reference manifest included in project/build fingerprints.
- [x] 2.2 Define expected output identity for project/configuration/TFM/platform/RID.
- [x] 2.3 Define authoritative verification from an ArchLinterNet receipt or equivalent supported compiler evidence.
- [x] 2.4 Explicitly classify timestamps and file size as supporting evidence only.
- [x] 2.5 Define distinct primary categories for missing, stale, wrong configuration, wrong TFM, wrong project/output, inconsistent dependency, restore-required, and unverifiable states.

## 3. Preparation and ownership

- [x] 3.1 Define ordinary validation as evaluation/verification only, with no implicit restore, build, hook, or network preparation.
- [x] 3.2 Define opt-in ensure-built as one structured graph-level build followed by re-evaluation and re-verification.
- [x] 3.3 Define `--no-restore` independently from ensure-built and preserve deterministic offline behavior.
- [x] 3.4 Define the optional caller build-hook boundary as trusted executable + argv, never policy YAML or a shell command string.
- [x] 3.5 Define immutable snapshot publication, ownership, disposal, reuse, TOCTOU protection, and cancellation behavior.

## 4. Architecture and downstream alignment

- [x] 4.1 Add `docs/internal/analysis-build-state-blueprint.md` with field-role, state-machine, security, and downstream implementation guidance.
- [x] 4.2 Add the blueprint to `docs/internal/README.md`.
- [x] 4.3 State explicit consumption rules for #362, #363, #365, #366, #374, and #375.
- [x] 4.4 Record that public CLI/API/diagnostic schemas and the capability manifest remain unchanged until implementation exists.
- [x] 4.5 Confirm #362 references #387 as its dedicated design dependency.

## 5. Validation and review

- [ ] 5.1 Confirm `rtk openspec validate --all` in PR CI after archival.
- [ ] 5.2 Confirm repository Markdown/docs validation in PR CI after archival.
- [x] 5.3 Review the contract against issue #387 acceptance scenarios and the #355 Analysis/build-state slice.
- [x] 5.4 Archive the change and publish the main capability spec in this PR as explicitly requested by the maintainer.
