## 1. Enrichment model and revision guard

- [x] 1.1 Add internal deterministic enrichment status, reason, file-context, and type-context models to the history result.
- [x] 1.2 Add opt-in history request/CLI plumbing that preserves the default Git-only behavior.
- [x] 1.3 Verify requested enrichment against clean checkout state and exact resolved `to` identity before accessing source facts.

## 2. Core fact projection

- [x] 2.1 Materialize existing Core policy, project discovery, verified post-build assembly, and source-file fact services only after the revision guard succeeds.
- [x] 2.2 Project stable .NET context onto finalized canonical C# logical-file paths without changing history evidence or identity.
- [x] 2.3 Convert all enrichment setup and materialization failures into bounded unavailable status while preserving successful Git ingestion.

## 3. Tests and synchronization

- [x] 3.1 Add focused NUnit coverage for not-requested, successful mapping, not-applicable files, unavailable setup, revision mismatch, and Git-only invariance.
- [x] 3.2 Add regression coverage for same-path reuse and ambiguous rename lineage preservation.
- [ ] 3.3 Format, run focused validation and relevant architecture/OpenSpec checks, then synchronize and archive the change.
