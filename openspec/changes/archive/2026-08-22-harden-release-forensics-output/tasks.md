## 1. Canonical serialization and stdout boundary

- [x] 1.1 Correct the report renderer naming violations and reject unpaired UTF-16 surrogates while retaining valid non-BMP scalars.
- [x] 1.2 Add the history JSON stdout byte sink and deterministic serialization-failure diagnostic path.

## 2. Contract regression coverage

- [x] 2.1 Add focused Core byte vectors for scalar validation, enrichment states/order, candidates, and report-versus-diagnostic separation.
- [x] 2.2 Add focused CLI byte-output and failure-boundary tests with non-ASCII report content.

## 3. Verification and specification synchronization

- [x] 3.1 Run formatting, relevant Core/CLI tests, repository lint, and inspect the patch.
- [x] 3.2 Validate and archive the completed OpenSpec corrective change, then update the pull request.
