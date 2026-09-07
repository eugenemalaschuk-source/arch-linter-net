using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

using CommandResult = CheckpointBReleaseGateTests.CommandResult;

/// <summary>
/// The v0.8 full-cycle scenario's shared health/gate oracle: every phase group that drives
/// <c>health</c> asserts its outcome through this single canonical mapping, rather than each
/// re-deriving <c>HealthCommandHandler</c>'s pass/fail/unassessable exit-code contract locally.
/// </summary>
internal static class CheckpointBV08HealthOracle
{
    internal static void AssertHealthState(
        CommandResult result, string expectedHealth, string expectedGate, string scenarioId, string? extraDiagnostic = null)
    {
        // HealthCommandHandler maps pass -> 0, fail -> 1, unassessable -> 2. Asserting the exact code
        // derived from expectedGate (not "any documented exit code") is what actually proves the CLI
        // contract this scenario claims to authorize -- a regression returning 2 for a HEALTHY/DEBT
        // pass, or 0 for a FAILING fail, would otherwise still pass.
        int expectedExitCode = expectedGate switch
        {
            "pass" => 0,
            "fail" => 1,
            "unassessable" => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(expectedGate), expectedGate, "Unknown expected gate."),
        };
        Assert.That(result.ExitCode, Is.EqualTo(expectedExitCode), $"{scenarioId}: {result.CombinedOutput}");
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        string? health = document.RootElement.TryGetProperty("health", out JsonElement healthElement)
            ? healthElement.GetString()
            : null;
        string? gate = document.RootElement.TryGetProperty("gate", out JsonElement gateElement)
            ? gateElement.GetString()
            : null;
        string message = extraDiagnostic is null
            ? $"{scenarioId}: {result.StandardOutput}"
            : $"{scenarioId}: {result.StandardOutput}{Environment.NewLine}{extraDiagnostic}";
        Assert.Multiple(() =>
        {
            Assert.That(health, Is.EqualTo(expectedHealth), message);
            Assert.That(gate, Is.EqualTo(expectedGate), message);
        });
    }
}
