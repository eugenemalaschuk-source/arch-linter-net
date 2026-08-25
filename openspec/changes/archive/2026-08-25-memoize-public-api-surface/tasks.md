## 1. Session-owned exported-surface index

- [x] 1.1 Add an internal immutable exported-surface materialization and a session-owned assembly-identity index; verify the existing scanner's normalized exported entries and defensive reflection behavior remain unchanged.
- [x] 1.2 Route public-API validation, selector membership/safety, and capture/diff/update/migrate through the session index; verify contract-specific selectors, snapshots, comparison modes, and ignores remain per-contract.

## 2. Regression evidence

- [x] 2.1 Add focused deterministic Core tests using a synthetic multi-contract fixture to prove one materialization per assembly per session, selector/no-selector isolation, capture-path reuse, and fresh materialization in an independent session; run the focused tests successfully.

## 3. Integration validation

- [x] 3.1 Run formatting, the affected Core test project, architecture lint, and strict OpenSpec validation; inspect the final diff for unchanged public API/report semantics.
