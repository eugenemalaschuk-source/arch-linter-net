# External Evidence Policy Format

Use `external_evidence` to declare a vendor-neutral requirement for a
pre-produced SARIF artifact. The declaration describes which evidence is
expected and which context bindings are required; it does not run an analyzer
or choose a file by name. An optional diagnostic filter selects a bounded,
policy-authorized subset only after the artifact has passed that trust boundary.

This is a trust boundary for a later consuming family. The Core external-
evidence boundary receives the repository-local artifact and the explicit
assessment and producer/CI contexts, then returns either trusted evidence or a
closed failure. See [SARIF output](../usage/output-formats.md#sarif-output) for
the SARIF documents ArchLinterNet produces for its own reports.

## YAML shape

`external_evidence` is a root-level collection. Each entry has exactly these
configuration fields:

```yaml
external_evidence:
  - id: static-analysis
    format: sarif
    required: true
    tool: Example Analyzer
    tool_version: "1.2.3"
    run: architecture-check
    require_repository: true
    require_revision: true
    require_scope: true
    diagnostic_filter:
      rule_ids: [SEC100]
      rule_tags: [security]
      projects: [src/ArchLinterNet.Core]
      path_prefixes: [src/]
      severity:
        error: strict
        warning: audit
      require_matches: true
```

The declaration is intentionally separate from the artifact location. A
caller binds an entry to a local artifact when it invokes the Core boundary;
the policy does not contain a URL, filename, or vendor service reference.

### Fields

| Field | Description |
| --- | --- |
| `id` | Required logical identity for this evidence requirement. It identifies the evidence declaration, not a path or an analyzer result. IDs must be unique in the effective policy. |
| `format` | Required format discriminator. The supported value is exactly `sarif`. |
| `required` | Required boolean. `true` means that missing or unusable evidence cannot satisfy the requirement. `false` makes deliberate absence an explicit optional outcome. |
| `tool` | Required expected producer/tool name. The reader uses it when matching the SARIF run. |
| `tool_version` | Optional expected producer version. When present, it must match the selected run; omit it when version binding is not required. |
| `run` | Required expected SARIF automation/run identity. It selects the one run that can satisfy the declaration. |
| `require_repository` | Optional boolean. Set to `true` when the producer/CI evidence must bind to the current repository identity. |
| `require_revision` | Optional boolean. Set to `true` when the producer/CI evidence must bind to the current source revision. |
| `require_scope` | Optional boolean. Set to `true` when the producer/CI evidence must bind to the current assessment scope. |
| `diagnostic_filter` | Optional typed selector for diagnostics from this already-trusted logical evidence input. Omit it to retain the reader's trust-only behavior. |

The three `require_...` fields are opt-in binding requirements. If a binding
is required, its value must be available and must agree between the evidence
and the current assessment context. Omitting a requirement (or setting it to
`false`) does not make that dimension a required binding.

## Diagnostic filtering

`diagnostic_filter` is owned by one `external_evidence` entry. The parent
entry's `id`, `tool`, optional `tool_version`, and `run` remain the logical
evidence and expected-producer selector; do not repeat those fields in the
filter.

| Field | Description |
| --- | --- |
| `rule_ids` | Optional non-empty exact source rule IDs; at most 128 values. |
| `rule_tags` | Optional non-empty exact tags declared by the selected SARIF driver's matching rule; at most 128 values. |
| `projects` | Optional non-empty exact source `result.properties.project` identities; at most 128 values. A result without that explicit source field does not match this criterion. |
| `path_prefixes` | Optional normalized repository-relative `/` prefixes; at most 128 values. A prefix matches the exact path or a descendant; absolute paths, `..`, backslashes, globs, and regular expressions are invalid. |
| `severity` | Required when `diagnostic_filter` is present. Maps one or more source levels (`error`, `warning`, `note`, `none`, `unspecified`) to the ArchLinterNet mode `strict` or `audit`. It both selects source levels and maps the selected result; it never changes the original source level. |
| `require_matches` | Optional boolean. When `true`, every configured rule ID, tag, project, path prefix, and severity key must match at least one result satisfying the other configured criteria. |

Non-empty filter categories combine with **and**; values within one category
combine with **or**. For example, a result must have a listed rule ID *and* a
listed path prefix, while either listed rule ID can match. With
`require_matches: true`, an old rule ID or source path produces deterministic
unmatched-filter evidence rather than disappearing behind a zero-result
selection.

Filtering only consumes typed source data from a valid selected run. It keeps
the original tool, rule, message, source severity, primary location, optional
project, driver-rule tags, and SARIF fingerprint pairs together with the trust
provenance described below. A wrong-revision or otherwise untrusted artifact
never supplies ordinary selected diagnostics.

When `diagnostic_filter` is omitted, the reader retains its trust-only
behavior: it does not parse or validate result members that are relevant only
to diagnostic selection, and it exposes neither source diagnostics nor an
authorization snapshot. This keeps existing #520 evidence valid unless it
violates the SARIF trust contract itself.

Rule tags are bound to one resolved driver-rule descriptor, not merely a rule
ID. When a SARIF driver repeats a descriptor ID, a result must provide a
consistent `ruleIndex` or `rule.index`; an ambiguous ID-only reference is
rejected rather than borrowing tags from another descriptor. The reader also
resolves supported artifact-location indexes through the run artifact table.

When the reader accepts an artifact, it also captures an immutable authorization
snapshot: the parent logical ID, tool/version/run identity, required binding
flags, validated assessment context, and a detached copy of `diagnostic_filter`.
The selector consumes that snapshot rather than a second mutable requirement, so
evidence trusted for one policy cannot be reinterpreted by a later policy with
the same `id`. For repeated artifacts with the same snapshot,
`require_matches` is evaluated across their combined trusted results.
Authorization grouping uses an unambiguous structural encoding, so filter values
containing control characters cannot merge distinct policy snapshots.

For projected primary locations, present line and column values must be positive
and character offsets/lengths must be non-negative. Ending positions cannot
precede their starts; malformed regions reject the artifact rather than becoming
trusted fallback-identity facts.

For a selected diagnostic, ArchLinterNet preserves source-provided
`fingerprints` where available and otherwise creates a deterministic fallback
from stable evidence, rule, project, and normalized location facts. Neither
fallback nor canonical selected-result identity uses a display message or
runtime result ordering. Equivalent current-context repeated runs deduplicate
while retaining each ordered artifact/run provenance; different logical keys,
revisions, scopes, and source locations remain distinct. Results with different
source severities or mapped governance modes also remain distinct, so a strict
occurrence cannot be lost to an audit occurrence with the same fingerprint.

## Normalized finding consumption

`SarifExternalDiagnosticSelector` produces selected diagnostics only from trusted reader results.
`ArchitectureImportedDiagnosticProjector` converts those selected diagnostics into the ordinary
ArchLinterNet normalized finding seam. `ArchitectureImportedDiagnosticBaselineProjector` reuses
the resulting exact identity for baseline candidates. The projections keep the selected logical
evidence control, policy-mapped strict/audit mode, original source rule/message/severity/location,
source-or-fallback fingerprint, and every authorizing evidence context.

The stable governed finding identity comes from the selected diagnostic's current-context/source
identity. Artifact path, content hash, and producer run remain inspectable provenance rather than
new debt identity, so an equivalent rerun does not create artificial baseline churn. A distinct
logical evidence key, revision, scope, or source location stays distinct where selection made it
distinct.

Human output, normalized JSON, SARIF's `properties.arch_linter_net`, and the Testing adapter use
the same typed facts. ArchLinterNet emits one of its own SARIF results for a normalized imported
finding; it does not nest or copy the original SARIF log.

External evidence completion uses the shared applicability projection: a trusted required
zero-result artifact is evaluable, deliberate optional absence is not applicable, and missing,
malformed, filter-mismatched, or wrong-context evidence is unassessable. This consumer carries the
reader's decision forward; it does not perform a second trust check.

## Assessment context and producer/CI context

The two contexts have different owners and purposes:

- The **current assessment context** describes what the invocation is
  assessing now: its repository identity, source revision, and scope.
- The **producer/CI context** describes what the producer actually analyzed
  for the artifact: the corresponding repository, revision, scope, and
  logical evidence identity when supplied outside standard SARIF metadata.

The reader compares these contexts only through explicit values. Standard
SARIF run metadata and explicit producer/CI context are both valid
vendor-neutral transports. If two supplied values conflict, the evidence is
unassessable. If a dimension is required but neither context supplies it, the
reader does not guess that the artifact is current.

This distinction prevents a policy from hard-coding a changing commit or
trusting an artifact name, filesystem timestamp, artifact ordering, or CI job
name as proof of freshness. `id` is the logical evidence identity used for
this binding; it is not inferred from a path.

## Reference scenario: current-context evidence

The following is a deliberately synthetic, vendor-neutral evidence flow. It
describes the values a caller supplies to the Core boundary; it is not an
analyzer invocation, a producer-service integration, or a CLI artifact option.

```text
repository-local SARIF 2.1.0 bytes
  + logical evidence id: static-analysis
  + current assessment: repository, revision, scope
  + producer context: repository, revision, scope, logical evidence id
  -> bounded trust validation
  -> policy-authorized diagnostic selection
  -> canonical imported finding and applicability projections
```

When the matching SARIF run is successful and every configured binding agrees,
the resulting canonical finding retains the logical evidence identity,
producer/run facts, repository/revision/scope, deterministic artifact hash, and
source location/fingerprint provenance. Human, JSON, SARIF, Testing, baseline,
and later report consumers use those canonical facts; they do not need to reopen
the source SARIF or query a producer service.

A successful trusted run with no selected results is explicit evaluable evidence.
By contrast, a missing, malformed, unsuccessful, wrong-repository,
wrong-revision, wrong-scope, wrong-logical-key, or binding-incomplete artifact is
unassessable evidence. It neither becomes a clean zero-result run nor supplies a
current imported finding. Equivalent repeated current-context results can
deduplicate deterministically, while different source locations and logical
evidence and scope contexts remain distinct.

## What the bounded reader proves

For a declared requirement, the Core boundary:

1. accepts only a regular artifact path contained by the repository root;
1. rejects absolute or out-of-repository paths and unsafe filesystem
   indirection before reading unrelated files;
1. applies bounded input limits before parsing and hashes the exact consumed
   bytes with deterministic lowercase SHA-256;
1. accepts only a SARIF 2.1.0 document and one unambiguous run matching the
   expected tool and, when supplied, run identity;
1. requires explicit successful execution metadata for that selected run; and
1. checks each required repository, revision, and scope binding against the
   current assessment context.

The result retains the normalized repository-relative artifact path, content
hash, selected tool/run facts, result count, and validated context bindings so
a later family can consume the trust decision. For a valid selected run, the
reader also retains typed source facts for the diagnostic filtering boundary;
it does not normalize them into native contract families or query a producer.

Missing required artifacts, malformed JSON or SARIF, unsupported versions,
ambiguous or absent expected runs, unsuccessful execution, unsafe paths,
resource-bound violations, and wrong-context evidence all fail closed. They
remain distinct from a successful run with no findings and cannot be treated
as a clean zero-result assessment.

## Valid zero results

An empty result collection is valid evidence only when the surrounding trust
proof succeeds. A valid zero-result artifact is a bounded SARIF 2.1.0
document with an unambiguous matching tool/run, explicit successful execution,
and all required context bindings present and matching. The trusted outcome
records a result count of zero.

Therefore:

- a present artifact with zero results can be valid evidence;
- a missing required artifact is not equivalent to zero results;
- a malformed, failed, unsafe, or wrong-context artifact is not equivalent to
  zero results; and
- an empty result collection does not prove that an assessment ran by itself.

## Optional absence

An entry with `required: false` may deliberately have no supplied artifact.
That outcome is explicitly optional/not configured. It is distinct from both
required missing evidence and a valid successful zero-result run.

Optional means absence is allowed, not that supplied evidence is trusted
automatically. If an optional artifact is supplied but is malformed, unsafe,
unsuccessful, over the configured bound, or bound to the wrong context, the
reader returns an unassessable failure for the supplied evidence.

## Non-goals

This declaration and reader do not provide:

- analyzer execution or analyzer configuration;
- remote URLs or vendor service APIs;
- producer-service queries; or
- freshness inference from filenames, modification times, or workflow/job
  names.

The documented consumption point is the Core external-evidence boundary for a
later consuming family. This page does not define a command-line integration.
