## Context

The CLI currently reaches policy loading through validation commands that subsequently prepare projects and assemblies. Core already owns portable root/fragment composition and typed policy provenance, while CLI owns command parsing and output projection. Policy check must reuse that loader and its static validators without creating a second YAML parser or triggering analysis/session preparation.

## Goals / Non-Goals

**Goals:**

- Provide one assembly-free command and Testing API operation with deterministic completed, deferred, and failed states.
- Reuse Core policy-loading, import-boundary, schema, contract-ID, and static validation behavior.
- Project typed configuration diagnostics consistently to human, JSON, and SARIF.

**Non-Goals:**

- Build, evaluate projects, load target assemblies, or claim architecture compliance.
- Reimplement fact-dependent selector/contract evaluation or replace strict validation.
- Add remote imports or mutate policy, baseline, or snapshot inputs.

## Decisions

### Introduce a Core policy-check application service

The service loads a policy once and performs only static checks. It returns a normalized result containing completed-check identifiers, typed deferred checks, and typed diagnostics. This keeps assembly-free semantics testable outside the CLI and prevents the CLI from coupling to loader internals. Reusing `ArchitectureValidator` was rejected because its public operation progresses to architecture analysis.

### Make deferred state a successful-but-incomplete policy result

The command exits `0` when static checks pass, even if deferred checks exist; its result explicitly states that no architecture-clean conclusion was made. Invalid policy/configuration exits `2`, consistent with the compatibility exit categories. Treating deferrals as failures would make valid policies unusable in editor/pre-commit workflows; treating them as clean would violate the required boundary.

### Reuse normalized policy provenance in every diagnostic projection

Root descriptors remain roots and fragment diagnostics retain authored location plus full import chain. JSON and SARIF consume the same diagnostic/deferred records as human output rather than reconstructing string messages. A dedicated command-only text format was rejected because it would diverge from typed output behavior.

### Keep the CLI shape as `policy check`

A top-level `policy` module contains `check`, following existing grouped CLI commands. The command accepts `--policy` and `--format human|json|sarif`; it does not accept build, mode, or report-routing options because those imply full validation responsibilities.

## Risks / Trade-offs

- [Static checks accidentally call fact-dependent code] → Inject only the policy document loader/static service and add instrumentation tests that fail on assembly/project access.
- [Deferred output is mistaken for clean architecture] → State `status: valid-with-deferred-checks` in machine output and a prominent human summary.
- [New diagnostics drift from existing projections] → Reuse normalized diagnostics/provenance and parity tests for human, JSON, and SARIF.
- [Policy loading covers more than static validation] → Maintain an explicit completed/deferred inventory in the Core result and test it against policy fixtures.

## Migration Plan

The command is additive. Existing validation behavior and policy formats remain unchanged. Documentation introduces policy check as the editor/pre-commit entrypoint and directs architecture compliance users to strict validation.

## Open Questions

None; the issue and compatibility specification fix the command boundary and exit-code categories.
