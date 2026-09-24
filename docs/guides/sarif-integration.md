# Use an external SARIF result

Keep your analyzer's existing CI step. ArchLinterNet consumes its local SARIF
file; it does not install the analyzer, call its service or run the scan.

## Choose trust-only checking or diagnostic import

Declare a root-level `external_evidence` requirement in the selected policy:

```yaml
external_evidence:
  - id: static-analysis
    format: sarif
    required: true
    tool: Example Analyzer
    run: architecture-check
    require_repository: true
    require_revision: true
    require_scope: true
    diagnostic_filter:
      severity:
        error: strict
        warning: audit
```

`tool` and `run` must match the producer, not the CI job's display name.
With `diagnostic_filter`, selected findings participate in the mapped mode.
Without it, the requirement checks trust/completion only: diagnostics are not
imported. Policy declares identity and selection; the invocation binds a file.

Additional filter categories combine with AND; values within one category use
OR. `require_matches: true` is for selectors expected to match actual results;
it can reject an empty selection. Do not add it to a zero-result acceptance
fixture expecting success. See [the full format](../policy-format/external-evidence.md)
for filter limits, identity, optional inputs and supported SARIF metadata.

## Try the boundary with a synthetic successful scan

The following file is a **test fixture**, not evidence that an analyzer ran on
your code. Save it as `evidence/static-analysis.sarif` after creating the directory:

```json
{
  "version": "2.1.0",
  "runs": [
    {
      "tool": {"driver": {"name": "Example Analyzer"}},
      "automationDetails": {"id": "architecture-check"},
      "invocations": [{"executionSuccessful": true}],
      "results": []
    }
  ]
}
```

This tests that a successful, matching zero-result run is distinct from a missing
file. Use your real producer's output before making the integration authoritative.
Do not publish this fixture as a security scan receipt.

## Bind producer and assessment context separately

For the synthetic fixture, explicitly set its context at creation time:

```bash
PRODUCER_REPOSITORY=https://github.com/example/repository
PRODUCER_REVISION=$(git rev-parse HEAD)
PRODUCER_SCOPE=ci
```

For a real scan, retain these values from the producer. When standard SARIF
metadata already supplies them, additional binding values must agree. A later
consumer must not substitute its own revision for an unknown producer revision.

With your policy/build inputs prepared, run:

```bash
arch-linter-net --policy architecture/arch.yml --mode strict --ensure-built \
  --external-evidence "id=static-analysis,path=evidence/static-analysis.sarif,repository=$PRODUCER_REPOSITORY,revision=$PRODUCER_REVISION,scope=$PRODUCER_SCOPE" \
  --evidence-repository https://github.com/example/repository \
  --evidence-revision "$(git rev-parse HEAD)" --evidence-scope ci \
  --format json > architecture.json
```

Replace the synthetic repository identity consistently. The per-file fields
are the producer context; `--evidence-*` describes the current assessment.
A conflict is an error, not permission to prefer whichever context is convenient.

## Verify the failure cases

Retain a successful zero-result fixture. Then remove the required file, change
its run identity, mark execution unsuccessful, and supply a different producer
revision in separate tests. None may become an ordinary successful empty result.
Restore the valid fixture between cases so each failure has one cause.

Add a real selected diagnostic to check its strict/audit mapping. Required
incomplete evidence and an ordinary selected finding are different outcomes;
inspect structured applicability/trust diagnostics and the exit code rather
than counting only SARIF results.

## Health and historical comparisons

Repeat the required bindings on the `health` invocation; a previous validation
process does not pass them to a new one. On a supporting version,
`health --change-snapshot` keeps current Health and snapshot evidence in the
same session. Base evidence must be from the base revision with its own producer
context, not the current scan relabeled as historical evidence.

The standalone `change snapshot` command does not expose the same external-
evidence binding options. Do not weaken the policy to force a base snapshot.
Use an appropriately bound producer supported by the selected package, or report
that the complete base comparison is unavailable while preserving the current
gate and diagnostics.
