## 1. Machine-Readable Metadata

- [ ] 1.1 Derive the supported YAML field list from `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs` and current runner behavior.
- [ ] 1.2 Add `schema/dependencies.arch.schema.json` covering top-level policy fields, layer definitions, analysis settings, all strict/audit contract arrays, ignored violations, and unsupported-field rejection.
- [ ] 1.3 Add `archlinternet.capabilities.json` describing supported contract families, validation modes, matching semantics, ignored-violation behavior, Unity `.asmdef` support, method-body checks, and known limits.
- [ ] 1.4 Update `docs/reference/yaml-schema.md` to link to the JSON Schema and document that runtime YAML loading currently ignores unmatched fields unless external schema validation is used.

## 2. AI-Facing Documentation

- [ ] 2.1 Replace the placeholder `docs/ai/index.md` with an entry point for the AI policy-authoring kit.
- [ ] 2.2 Add `docs/ai/agent-guide.md` explaining how agents should inspect real assemblies, namespaces, project references, existing policies, and migration debt before authoring policy YAML.
- [ ] 2.3 Add `docs/ai/policy-authoring-guide.md` with rules for strict versus audit usage, narrow layer modeling, allow-only rules, ordered layers, ignored violations, and unsupported YAML fields.
- [ ] 2.4 Add `docs/ai/capabilities.md` summarizing supported capabilities and current limits in AI-facing language.
- [ ] 2.5 Add `docs/ai/policy-review-checklist.md` for reviewing AI-generated policy changes before PRs.
- [ ] 2.6 Update `mkdocs.yml` navigation to expose the new AI documentation pages.

## 3. Policy Recipe Samples

- [ ] 3.1 Create `samples/policies/` for standalone, schema-valid policy recipes.
- [ ] 3.2 Add `samples/policies/basic-clean-architecture.yml` showing application/domain/infrastructure/UI boundaries using supported fields.
- [ ] 3.3 Add `samples/policies/modular-monolith.yml` showing module independence, shared kernel, and public contract patterns using supported fields.
- [ ] 3.4 Add `samples/policies/unity-asmdef-boundaries.yml` showing Unity runtime/editor `.asmdef` boundaries using supported fields.
- [ ] 3.5 Ensure sample docs or comments make clear that these policy recipes use illustrative assemblies/namespaces and must be adapted to real repositories.

## 4. Validation

- [ ] 4.1 Verify all new JSON files parse successfully and are formatted consistently.
- [ ] 4.2 Validate all sample policy files against `schema/dependencies.arch.schema.json` using an available JSON Schema validator.
- [ ] 4.3 Run `rtk make restore` if needed for no-restore targets.
- [ ] 4.4 Run `rtk make verify` and resolve any lint, architecture, formatting, or test failures caused by the change.
