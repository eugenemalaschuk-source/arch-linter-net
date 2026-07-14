## Context

ArchLinterNet currently has three production assemblies: `Core`, `Cli`, and `Testing`. The architecture policy governs their boundaries via `architecture/dependencies.arch.yml` and is enforced by the self-policy acceptance test. The planned CEL engine requires a fourth assembly that Core can depend on without pulling any existing ArchLinterNet infrastructure into the new package.

This change establishes only the physical boundary — no CEL language behavior is introduced. All parser, binder, evaluator, and conformance work belongs to follow-up tasks under story #322.

## Goals / Non-Goals

**Goals:**

- `ArchLinterNet.CEL` builds, packs, and produces a valid `.nupkg` in isolation
- `ArchLinterNet.Core` references CEL via `ProjectReference`; the dependency direction is governed and enforced
- The architecture policy knows about the CEL assembly (layer declared, in target_assemblies, in namespace-coverage roots)
- Reverse-dependency contracts (`cel-must-not-depend-on-core/cli/testing`) are declared and passing
- An architecture test in `CEL.Tests` proves that a synthetic reverse dependency trips the contract

**Non-Goals:**

- No CEL language semantics, parser, type-system, evaluator, or conformance tests
- No public API surface beyond a minimal namespace placeholder
- No new external NuGet packages (YamlDotNet, JsonSchema.Net, Roslyn, Buildalyzer, DI container)
- No `InternalsVisibleTo` grants to `ArchLinterNet.Core`

## Decisions

### D1 — CEL project carries no forbidden dependencies

CEL must be usable in contexts without YAML loading, JSON schema, MSBuild evaluation, or IO abstractions. The `.csproj` lists no `PackageReference` entries other than the SDK defaults. This is enforced by the `Required constraints` in issue #323 and by the package validation step (inspect the produced `.nuspec`).

*Alternative considered*: Reference a small utility package. Rejected — no language behavior is being introduced this change, so there is nothing to reference. Any future dependency goes through its own proposal.

### D2 — Architecture policy uses a single flat `cel` layer

The namespace `ArchLinterNet.CEL` maps to one layer (`cel`). Internal sub-namespaces (future: `CEL.Parser`, `CEL.Evaluator`, etc.) will be added in their own tasks when the namespaces exist. Adding phantom sub-layers now would cause `namespace-coverage` and `assembly-coverage` contract failures (unmapped types).

*Alternative considered*: Pre-declare `cel_parser`, `cel_evaluator` sub-layers now. Rejected — empty layers cause coverage violations and `strict_coverage: error` would fail the self-policy test.

### D3 — Reverse-dependency contracts are three separate entries

Three contracts (`cel-must-not-depend-on-core`, `cel-must-not-depend-on-cli`, `cel-must-not-depend-on-testing`) are declared instead of one broad contract. This matches the existing pattern in the policy file and produces clearer diagnostic messages when a violation occurs.

### D4 — Package validation is shell-based, not a test fixture

The acceptance criteria require inspecting the produced `.nuspec` to confirm Core declares a CEL dependency. This validation runs as `rtk make pack` followed by manual inspection of the `nupkg/` artifact, rather than as a new test fixture. A test fixture would require the pack step to run first, creating a fragile ordering dependency. The CI pack job already validates this.

*Alternative considered*: An NUnit test that unzips the `.nupkg` and asserts the dependency. Deferred — this is follow-up infrastructure under story #322.

## Risks / Trade-offs

- **Coverage contract breakage** → The `namespace-coverage` and `assembly-coverage` contracts in `dependencies.arch.yml` must include `ArchLinterNet.CEL` as a root and target assembly. Forgetting either will cause the self-policy test to fail with a `coverage: error` diagnostic. Mitigation: tasks checklist includes both entries explicitly.

- **`assembly_search_paths` mismatch** → The policy resolves assemblies from `bin/Debug/net10.0`. If CEL's output path diverges (e.g., a different TFM), the assembly will not be found and coverage will fail. Mitigation: match the path pattern used by existing entries.

- **`SelfArchitecturePolicyTests` surface expansion** → The existing test runs the full policy including coverage contracts. Adding CEL to `target_assemblies` without a build step means the assembly may be absent at test time. Mitigation: `lint-architecture` already calls `dotnet build` before running the self-policy test; no change needed.
