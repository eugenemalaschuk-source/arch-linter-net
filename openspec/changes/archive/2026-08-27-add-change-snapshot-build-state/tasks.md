## 1. Core build-state routing

- [x] 1.1 Extend graph requests and graph construction to honor explicit build-state preparation and materialize verified post-build runners.
- [x] 1.2 Extend baseline-diff candidate collection to honor the same build-state request and fail closed on blocked preflight.
- [x] 1.3 Add focused Core tests for graph and baseline-diff post-build routing, including output-context forwarding.

## 2. Change snapshot CLI

- [x] 2.1 Add the established build-state option set to `change snapshot` parsing, typed options, help, and all contributing requests.
- [x] 2.2 Add focused CLI handler coverage that proves validation, graph, and optional baseline requests receive identical build-state options.
- [x] 2.3 Update the reviewed Core public API approval evidence and user-facing change-snapshot documentation.

## 3. Regression evidence and completion

- [x] 3.1 Add a packaged-CLI ASP.NET Core regression that writes and deserializes a strict change snapshot using `--ensure-built`.
- [x] 3.2 Run focused Core and CLI tests, formatting, relevant lint/API checks, and OpenSpec validation.
- [x] 3.3 Synchronize the change artifacts with the implemented behavior and archive the completed OpenSpec change.
