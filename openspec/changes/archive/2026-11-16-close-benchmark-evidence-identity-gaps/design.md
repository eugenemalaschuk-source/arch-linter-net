# Design

## Context

See `proposal.md` for the review findings. History workloads are currently deterministic test inputs, while audit samples execute through Core's prepared-candidate isolated loader. That loader already retains the SHA-256 captured from the exact PE bytes passed to `LoadFromStream`.

## Goals / Non-Goals

**Goals:**

- Make ancestry query expectations explicit and independently check both outcomes.
- Report the generated DAG's strict-ancestor closure without presenting it as measured runtime retention.
- Keep source/configuration identity and loaded-binary identity separate in each measured sample.

**Non-Goals:**

- Run the future History production algorithms or claim their runtime retained-memory measurements.
- Change Core assembly loading, product APIs, or `analysis-profile/v1`.
- Include runtime/framework DLLs that are not captured as fixture artifacts by the isolated analysis loader.

## Decisions

1. **Use the generated DAG as the source of structural closure cardinality.** Remove `retained_ancestor_entry_count` from the catalog's input dimensions; it is neither an independent materialized input nor a measured runtime-memory value. The generator will compute transitive ancestor sets over topologically ordered commits using compact bit sets and report the sum as `strict_ancestor_closure_entry_count`. Tests will independently traverse the parent graph and compare every query's explicit expected answer and the derived count.

2. **Mix deterministic positive and negative queries.** Keep `query_endpoint_count` as total query volume. Generate at least one query of each outcome, alternating the generated positives and negatives; a negative query asks whether the latest mainline commit is an ancestor of an earlier mainline commit. The input record carries the expected Boolean so the independent test can detect an implementation that answers `true` for every query.

3. **Attach loaded assembly identity to every analysis sample.** Retain `materialized_input_sha256` for canonical source/configuration inputs. Add an optional `loaded_assemblies` sample object containing a stable, sorted inventory of DLL file names and per-file SHA-256 values copied from `ArchitectureLoadedAssemblyArtifact.AssemblyContentDigest`, plus a canonical SHA-256 for the inventory. The source's temporary root path is never serialized. Evidence validation will verify digest syntax, inventory non-emptiness, and the aggregate digest.

4. **Keep old benchmark evidence readable.** The new sample field remains optional in the v1 schema and model so prior evidence remains valid. The new explicit #1123 matrix records it for every newly measured compiled sample and asserts equivalence across paired/repeated runs.

## Risks / Trade-offs

- **[Derived closure count can grow quadratically for a chain] →** Compute it with packed bit sets and bound work to the existing 10,000-commit catalog ceiling; tests independently validate it with graph traversal and the field name documents its structural meaning.
- **[A loaded artifact inventory can be empty if the harness stops using the isolated prepared-candidate path] →** Fail the audit workload rather than emit a source-only binary provenance claim.
- **[Repeated per-sample artifact lists add evidence size] →** Retain only stable file names and SHA-256 values, not paths or DLL contents.

## Migration Plan

Update the History catalog and generator version, add backward-compatible optional evidence fields and schema, run the focused deterministic tests, regenerate the explicit #1123 matrix under the current source revision, synchronize the main spec through `openspec archive`, and validate all OpenSpec specs.
