using NUnit.Framework;

// Each test in this assembly spawns its own `dotnet` subprocess (CLI integration tests)
// rather than sharing in-process mutable state, and every test that writes a file uses a
// unique Guid-based path — safe to run concurrently. The CLI build itself happens once in
// OneTimeSetUp before any test runs.
[assembly: Parallelizable(ParallelScope.All)]
[assembly: LevelOfParallelism(8)]
