## Context

Baseline captured 2026-09-13 from the public SonarCloud project API (`sonarcloud.io`, project
`eugenemalaschuk-source_arch-linter-net`, org `eugenemalaschuk-source`) on the latest completed
`main` analysis, revision `103b2680511b702288a95ee4a16189bcec099027` (matching current `HEAD`):

- Quality gate: `ERROR`. Failing conditions are `new_reliability_rating` (C vs 1) and
  `new_security_rating` (E vs 1). `new_maintainability_rating`, `new_coverage`,
  `new_duplicated_lines_density` and `new_security_hotspots_reviewed` pass.
- 460 open findings (455 CODE_SMELL, 4 VULNERABILITY, 1 BUG); 2368 min estimated debt; 0 hotspots.
- 458 of 460 findings fall in the New Code/leak scope (2333 of 2368 min), so New Code and Overall Code
  coincide for this tree.
- Overall ratings: Maintainability A, Reliability C, Security E.
- Concentration: Core 173 / relay TS 76 / Cli 70 / tests 63 / tools/release 41 /
  tools/badge_promotion 32 / tools/scripts 1 / benchmarks 4.
- No accepted or false-positive dispositions exist yet; all 460 are `OPEN`.

The 5 gate-blocking findings were read from source before triage:

| Finding | File:line | Triage |
| --- | --- | --- |
| `pythonsecurity:S2083` BLOCKER | `tools/release/main_quality_coverage.py:121` | false positive — sink path comes from `_safe_path(...)`; custom sanitizer not recognized |
| `pythonsecurity:S8707` HIGH | `tools/release/verify_restored_main_packages.py:23` | false positive — `assets_path = _safe_path(arguments.assets, ...)` at line 82 before the call |
| `python:S5443` HIGH | `tools/release/tests/test_create_release_scope_evidence.py:378` | false positive — negative test asserting rejection of a `"/tmp/elsewhere"` literal |
| `pythonsecurity:S8707` HIGH | `tools/scripts/test_coverage_badge.py:68` | real unguarded taint — `glob.glob(args.reports_glob)` from argparse, developer script |
| `csharpsquid:S2583` MEDIUM (BUG) | `src/ArchLinterNet.Core/Validation/ArchitectureHealthPublicationEvidenceProjector.cs:87` | false positive — `reasons` is mutated by the local function `Add` through a captured variable |

## Goals / Non-Goals

**Goals:**

- Provide a reproducible, revision-bound inventory and triage report so #795 has in-repository
  evidence instead of manual dashboard reading.
- Bring the quality gate to a truthful green by fixing the one real sink and resolving the other four
  findings with correct, individually justified dispositions.
- Remove the material share of behavior-preserving debt in non-product tooling, tests and relay
  mechanical rules.
- Keep every change behavior-preserving and preserve public API, policy/schema, finding identity and
  release semantics.

**Non-Goals:**

- C#/CLI cognitive complexity (`S3776`) and parameter count (`S107`), which have architectural root
  causes: routing them here would create the Sonar-driven competing decomposition #783 forbids. They
  go to focused owners.
- Public-API-risky Roslyn rules (`CA1859`, `S3871`, `S2365`, `S3218`) and relay cognitive-complexity
  refactors.
- Any quality-profile, gate, exclusion or rule-setting change; any blanket `NOSONAR`.
- Quality-gate publication or final #783/#797 acceptance; that remains a separate verification on the
  integrated merged tree.

## Decisions

**Read the public API anonymously and bind to a revision.**
The project is public, so `api/issues/search`, `api/hotspots/search`, `api/measures/component`,
`api/qualitygates/project_status` and `api/project_analyses/search` are readable without a token. The
tool therefore takes no secret, paginates explicitly (the API caps `ps` at 500), cross-checks the
inventory total against the reported total, and asserts the requested revision equals the newest
completed `main` analysis revision. This makes the baseline reproducible in CI or locally and keeps
secrets out of the repository. *Alternative rejected:* a token-based script (needs a secret, no added
capability for a public project) and manual dashboard capture (not reproducible, contradicts #795).

**Resolve false positives individually, never by rule.**
#783 forbids manufacturing improvement through broad suppression. Each of the four reviewed findings
is recorded with rule key, component, line and the guarding/justifying evidence. Where the guarding
helper can be made recognizable to the analyzer without weakening it, prefer the code change; the
reviewed disposition is the fallback for analyzer limitations (custom sanitizer recognition, closure
mutation modelling, negative-test literals).

**Remove the C# false positive structurally.**
`Project` collects reasons by calling a local function `Add` that appends to a captured `List`. The
analyzer cannot prove the count changes, so `reasons.Count > 0` is misread as always-false. Collecting
into an explicit local list and returning it, or using a small accumulator type, removes the pattern
without changing observable behavior. This is preferred over a suppression because it is a genuine
readability improvement.

**Sanitize the one real taint sink and restructure the negative test.**
`test_coverage_badge.py` gains an explicit input validation/containment for its glob argument. The
`S5443` test keeps asserting rejection but avoids a bare publicly-writable literal so the analyzer no
longer treats a test fixture as an insecure temporary-file use.

**Limit relay changes to mechanical rules.**
Relay is a shipped transport artifact consumed by later release work. Only behavior-preserving
mechanical rules (`S6653` `Object.hasOwn`, `S7780` `String.raw`, `S6353`, `S1128`, `S2737`, `S6571`,
`S7763`, etc.) are in scope; relay `S3776` complexity is deferred.

## Risks / Trade-offs

- [Sonar may still not recognize a restructured sanitizer] → the fallback is the individually reviewed
  disposition required by the modified capability; the finding set is re-inventoried before/after and
  the report distinguishes a code fix from a disposition.
- [Mechanical edits across many Python/TS files can change behavior] → each cluster is behavior-
  preserving, covered by existing tests, and validated with `make acceptance`; no public API or
  canonical output is touched.
- [The inventory tool could silently read the wrong revision] → it fails closed on a revision mismatch
  and cross-checks pagination totals.
- [Scope creep into architecture/API debt] → explicitly listed as non-goals and deferred to focused
  owners, preserving #783's execution-order constraint.
