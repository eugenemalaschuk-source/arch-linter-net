# External Evidence Policy Format

Use `external_evidence` to declare a vendor-neutral requirement for a
pre-produced SARIF artifact. The declaration describes which evidence is
expected and which context bindings are required; it does not run an analyzer
or choose a file by name.

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

The three `require_...` fields are opt-in binding requirements. If a binding
is required, its value must be available and must agree between the evidence
and the current assessment context. Omitting a requirement (or setting it to
`false`) does not make that dimension a required binding.

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
a later family can consume the trust decision. The reader does not select,
filter, or interpret individual diagnostics.

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
- diagnostic filters, severity mapping, result selection, deduplication, or
  normalized findings ([#521](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/521)
  and [#522](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/522)); or
- freshness inference from filenames, modification times, or workflow/job
  names.

The documented consumption point is the Core external-evidence boundary for a
later consuming family. This page does not define a command-line integration.
