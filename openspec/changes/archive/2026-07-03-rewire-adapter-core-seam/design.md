## Context

The Core composition root already exposes validation and baseline operations through `ArchitectureEngine`, but CLI, public API, and Testing still call legacy static facades. That keeps behavior compatible, yet it does not prove adapters can consume the composed seam directly.

Unity is a separate case. `AsmdefValidator` validates only `strict_asmdef` contracts and intentionally does not support full validation options such as mode selection, baselines, condition sets, selected contract IDs, or full validation diagnostics. The Core blueprint therefore calls for a narrow asmdef application service instead of folding Unity into the full validation seam.

## Decisions

1. Use `ArchitectureEngine` as the adapter-facing composed facade for this issue.
   - It already hides `ServiceProvider` and exposes typed operations.
   - It avoids adding container references to adapter projects.
   - It keeps adapters out of execution/scanning/resolution internals.

2. Keep static validation and baseline services as compatibility facades.
   - They remain supported for existing external callers.
   - In-repository adapters should move to the engine to satisfy #140.
   - Their implementation remains a lazy default engine delegation.

3. Add `IAsmdefValidationService` plus typed request/outcome records.
   - `AsmdefValidationRequest` carries the policy path.
   - `AsmdefValidationOutcome` carries the pass/fail result and asmdef violations.
   - The service owns policy loading, repository-root resolution, and asmdef scanner invocation.

4. Expose `ArchitectureEngine.ValidateAsmdef(AsmdefValidationRequest)`.
   - This is the typed operation Unity consumes.
   - The engine still exposes no generic `GetService<T>()` and no container object.

5. Keep Unity adapter behavior intentionally narrow.
   - Only `strict_asmdef` contracts are evaluated.
   - No validation mode/baseline/condition-set semantics are introduced.
   - Existing `AsmdefValidator.Validate(...)` signatures remain unchanged.

## Non-goals

- No CLI argument or output behavior changes.
- No public API signature changes.
- No Testing adapter DSL changes.
- No Unity mode/baseline/condition-set support.
- No unrelated static-service cleanup outside #140.
