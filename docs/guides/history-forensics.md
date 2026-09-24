# Investigate architecture history

Use `history analyze` to investigate files that repeatedly change together,
hotspots and coordination pressure across a selected Git range. Use
`change report` instead for a comparison of two complete architecture snapshots.
History findings are review evidence, not proof of a design violation or a reason
to block a merge automatically.

## Select the range

Work in a repository with the required commits, trees and blobs available.
A shallow checkout may lack them; prepare full history and the selected tags
before analysis. The CLI does not fetch missing history for you.

Pass full commit IDs or fully qualified refs. Shorthand tag/branch names are
accepted only when unambiguous. Remote refs require their full name, such as
`refs/remotes/origin/main`. Revision expressions such as `HEAD~20`, abbreviated
IDs and reflog selectors are not CLI operands. Resolve a deliberate Git
expression with Git first, then pass the resulting full ID.

The analyzed set is:

```text
Reachable(to) minus Reachable(from)
```

This is not necessarily a linear sequence of N commits, and `from` need not be
an ancestor of `to`. Release tags make a reproducible range when the question
is what changed between releases. Selecting a rolling window is a different
review policy, not a native `--last-n` option.

## Produce a report

Set `FROM_SHA` and `TO_SHA` to the full IDs you selected, then run:

```bash
mkdir -p artifacts
arch-linter-net history analyze --repository . \
  --from "$FROM_SHA" --to "$TO_SHA" --format json > artifacts/history.json
```

Use `--policy architecture/arch.yml` to select the policy's `history_analysis`
configuration. Do not assume ordinary architecture contracts define the history
ranking. Without an explicit policy, use the command's default history profile.
Check `history analyze --help` in the installed package before changing inputs.

For a human reading view, choose Markdown instead:

```bash
arch-linter-net history analyze --repository . \
  --from "$FROM_SHA" --to "$TO_SHA" --format markdown > artifacts/history.md
```

These are **alternative analytical invocations**. Running both performs history
analysis twice. The current command accepts one `--format`; it does not expose
validation's repeatable `--report` routing or a standalone history JSON renderer.
Choose the format the workflow needs rather than adding a duplicate expensive
run without accounting for it. Keep stderr separate from the report and preserve
a nonzero exit; an error document is not a successful empty history report.

## Read the evidence

Start with the resolved range, selected history policy/profile and input counts.
Then inspect hotspot and co-change evidence and the commits/tasks supporting
an investigation candidate. A large change count can reflect a broad mechanical
edit; files changing together can reflect one task rather than a necessary
architectural dependency. Check these explanations before assigning a refactor.

Hotspots, bottlenecks and OCP pressure are prioritization signals. They do not
prove a module boundary, an actual merge conflict or the correctness of a
proposed extraction. The deterministic analysis does not require an LLM.

When comparing reports, retain the exact CLI version, resolved commits and
history configuration. A changed policy or time range can change rankings even
when the implementation did not change. Do not compare rank positions alone.

## Optional .NET enrichment

`--enrich-dotnet` adds the supported .NET projection. It is optional and may need
additional compatible policy/build inputs. Establish the Git-only result first;
then inspect enrichment availability separately. Enrichment must not be treated
as permission to bind current working-tree assemblies to historical revisions.

## CI placement

A separate release or explicitly chosen scheduled job is usually a better fit
than the required PR path for a broad range investigation. Pin the two endpoints,
retain the selected report, and name the measured range in the job summary.
Do not silently use whatever the previous tag or `main` happens to be when a
retry runs. ArchLinterNet does not schedule the job or publish the report.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Unknown or ambiguous ref | Full object ID or fully qualified ref; tag/branch collisions; missing fetched tags. |
| Incomplete repository evidence | Shallow history, missing commit/tree/blob objects, unreadable objects. |
| Unexpected range size | Reachability across merges and whether `from` is an ancestor of `to`. |
| Expensive report | Range size, policy, runner, enrichment and accidental JSON/Markdown double invocation. |
| Unexpected ranking | Supporting commits/tasks, mechanical edits and configuration changes, not only the headline score. |

Use [performance diagnosis](../usage/timings.md) for measurement discipline;
profiling options are command-specific and must not be assumed available on
`history analyze` merely because validation accepts them.
