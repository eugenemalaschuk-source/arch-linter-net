# Repository metrics observability

ArchLinterNet emits a versioned repository-metrics/v1 object as informational evidence. It
describes the analyzed repository without changing architecture findings, Health, metric budgets,
or process exit status.

## Absolute metrics

The size group contains physical source lines, readable source-file count, discovered project
count, reflected type count, and reflected public-type count. Source lines count successfully read
C# files after the existing generated-file exclusions and project ownership rules. A file is
counted once even when source roots overlap.

The coupling group contains deduplicated project-reference edges, dependencies per project,
directed density, maximum fan-in and fan-out, and one deterministic row per project. Afferent
coupling (Ca) is fan-in, efferent coupling (Ce) is fan-out, and instability is Ce divided by
Ca plus Ce. A project with no incident edges has instability zero. Density is zero for zero or
one project and otherwise **non-self-loop** directed edges divided by projects times projects
minus one. `DependencyCount` still reports every deduplicated internal edge, including self-loops;
excluding self-loops from the density numerator keeps the documented no-self-loop denominator
bounded at 1 while preserving the cycle/self-loop evidence in the structure group.

The structure group contains maximum dependency depth, strongly connected component counts and
ratios, largest SCC size and ratio, and the count and ratio of projects participating in cyclic
components. Self-loops are cycles. Depth is the longest directed edge count in the condensation
graph, so cycles are collapsed before depth is calculated.

## Availability and change reports

Complete evidence is available only when the type universe, source inventory, and project graph
are complete. Partial evidence retains the values that were safely measured and lists stable
reason codes for missing or ambiguous inputs. Unavailable evidence uses null metric values. Missing
or incompatible base evidence produces an explicit unavailable repository-metrics-delta section;
it is never represented as zero.

The existing architecture-change artifact carries the bounded base-to-head delta. The PR report
renders that delta in one existing report section, with unchanged values retained as deterministic
evidence and bounded presentation. It does not create a second comment or publisher.

## Badge provenance

The Source lines Shields payload is an absolute default-branch projection of the same Health
artifact. The trusted post-merge badge-promotion pipeline reads the reviewed
`repository-metrics-badge.json` member from the canonical `architecture-health` evidence artifact
and publishes it beside the Architecture Health payload on the existing raw badge branch. It does
not read the already-rendered `architecture-health.json` Shields payload as metrics evidence, and
it never contains pull-request deltas or a quality score.

## Performance boundary

The calculator is cached by the immutable analysis session. It consumes the existing type index,
source-fact inventory, and project-discovery graph; it does not load MSBuild a second time, create a
semantic compilation, or run one repository traversal per metric. Ordinary policy-only validation
does not force the lazy source inventory: source-size fields are explicitly partial until a
report/Health caller requests the inventory, or a preceding contract has already materialized it.
This keeps the informational projection from adding a recursive source read to analyses that do
not otherwise need source facts.

Cache entries created before this projection may not contain a repository-metrics field. Such a
legacy cache hit preserves the older artifact byte-for-byte and does not fabricate metric values;
the next eligible fresh analysis populates the versioned snapshot.
