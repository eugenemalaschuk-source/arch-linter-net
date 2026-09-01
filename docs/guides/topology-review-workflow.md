# Review a topology before declaring it

ArchLinterNet's topology workflow separates observation from an architecture decision. Capture
records what the current first-party analysis can observe. A reviewer then decides which
observations belong in a bounded declared topology. Diff and verify evaluate that hand-authored
declaration; they do not accept capture output or rewrite policy.

## Capture observations

Start with a policy that describes the analysis inputs. It may omit `topology` when the repository
has no declaration yet:

```yaml
version: 1
name: Topology capture inputs
layers: {}
analysis:
  target_assemblies: [MyProduct.Server, MyProduct.Application, MyProduct.Domain]
  projects:
    - src/MyProduct.Server/MyProduct.Server.csproj
    - src/MyProduct.Application/MyProduct.Application.csproj
    - src/MyProduct.Domain/MyProduct.Domain.csproj
contracts: {}
```

For the first capture, use `--ensure-built` so ArchLinterNet builds the selected project graph
and writes the receipt that proves which artifacts it analyzed. A regular `dotnet build` alone
does not create that receipt. Later commands can reuse receipt-backed artifacts while their
inputs remain unchanged.

```bash
dotnet arch-linter-net topology capture \
  --policy architecture/arch.yml \
  --subject-kind assembly \
  --ensure-built \
  --format json \
  --output artifacts/topology-capture.json
```

The supported subject kinds are `type`, `namespace`, `project`, and `assembly`. Capture is
read-only, including when the policy has no declared topology. The output is a review artifact:
it is not a YAML policy fragment, does not invent exact type selectors, and does not imply that a
candidate is approved. Keep it outside `architecture/` or another directory containing the
reviewed policy, and commit it only when the repository wants a traceable observation artifact.

## Review and hand-author the declaration

Review subjects and directed dependency witnesses together. Select a bounded `scope`, group
subjects under stable node IDs, and decide which edges are allowed. If a subject is intentionally
outside this declaration, record an explicit `out_of_scope` entry with a reason. A reviewer, not
the capture command, must author these choices in the policy.

For example, a server/library declaration can make the intended direction explicit:

```yaml
topology:
  mode: exhaustive
  subject_kind: assembly
  scope:
    allow_empty: false
    selectors:
      - assembly: MyProduct.Server
      - assembly: MyProduct.Application
      - assembly: MyProduct.Domain
  nodes:
    - id: server
      mappings:
        - assembly: MyProduct.Server
    - id: application
      mappings:
        - assembly: MyProduct.Application
    - id: domain
      mappings:
        - assembly: MyProduct.Domain
  allowed_edges:
    - from: server
      to: application
    - from: application
      to: domain
```

For a Unity project, use the `.asmdef` assembly names as `assembly` selectors after Unity has
exported the assemblies for analysis. `.asmdef` validation remains a Core capability; topology
capture does not rewrite the manifests or turn them into an approved topology.

## Diff declared and observed evidence

Once a declaration is present, ask for a review projection:

```bash
dotnet arch-linter-net topology diff \
  --policy architecture/arch.yml \
  --mode strict \
  --ensure-built \
  --format json \
  --output artifacts/topology-diff.json
```

Diff delegates observation and evaluation to ordinary validation. Its categories stay separate:

| Category | Meaning |
| --- | --- |
| `structural` | A mapped subject has ambiguous component ownership. |
| `relational` | Exactly mapped components have an observed directed edge that is not allowed; retain its dependency witness. |
| `unmapped` | An observed in-scope subject has no component mapping in an exhaustive declaration. |
| `stale` | With `stale_declarations: true` and complete mapping evidence, a declared node or allowed edge has no observed counterpart. |

Reviewed out-of-scope subjects remain visible evidence. They are not reclassified as unmapped or
stale drift. Diff requires a declared topology and does not modify the policy, imports, baseline,
or capture artifact.

## Verify with normal validation semantics

Use verify when a focused topology result is useful to a review or CI job:

```bash
dotnet arch-linter-net topology verify \
  --policy architecture/arch.yml \
  --mode strict \
  --ensure-built \
  --format json

dotnet arch-linter-net topology verify \
  --policy architecture/arch.yml \
  --mode audit \
  --ensure-built \
  --format json
```

Verify invokes ordinary validation once. Strict and audit pass/fail and applicability behavior
therefore match a normal validation run; verify adds no topology-specific success condition and
does not create a second baseline or result envelope. A policy without declared topology is an
actionable input error for diff and verify, while capture remains available.

## Stable JSON contract

Capture JSON is versioned independently of the package version. Its top-level `kind` is
`topology-capture` and `schema_version` is `1`. Arrays are canonically ordered, so repeating a
capture for unchanged inputs produces identical bytes. The v1 document contains:

```json
{
  "kind": "topology-capture",
  "schema_version": 1,
  "subject_kind": "assembly",
  "subjects": [
    {
      "identity": "assembly|project=MyProduct.Server|assembly=MyProduct.Server|subject=MyProduct.Server",
      "subject_kind": "assembly",
      "subject": "MyProduct.Server",
      "project": "MyProduct.Server",
      "assembly": "MyProduct.Server"
    }
  ],
  "relationships": [
    {
      "source_identity": "...",
      "target_identity": "...",
      "witness": "MyProduct.Server -> MyProduct.Application"
    }
  ],
  "repository_root": "...",
  "policy_import_paths": [],
  "resolved_assembly_paths": [],
  "discovered_project_paths": [],
  "preflight_diagnostics": [],
  "preflight_blocked": false
}
```

`identity` is an opaque stable key; consumers must not reconstruct it from display names. Subjects
and relationships are observations, not declaration entries. Diff JSON has the same versioned
document discipline and exposes the four categories above, while verify preserves the ordinary
validation JSON envelope and its existing exit codes (`0` passed, `1` findings, `2` input/runtime
error). Consumers should key automation on `kind`, `schema_version`, category names, and typed
diagnostic fields rather than human rendering.

Neither operation writes a policy. A clean capture, diff, or verify result is evidence for review,
never automatic architecture approval.
