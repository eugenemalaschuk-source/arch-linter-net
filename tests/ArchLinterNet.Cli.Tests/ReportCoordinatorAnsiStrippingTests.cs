using ArchLinterNet.Cli.Commands.Validate;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public sealed partial class ReportCoordinatorTests
{
    [Test]
    public void StripAnsi_RemovesCsiAndOscSequencesFromHumanReports()
    {
        const string Colored = "\u001b[31mviolation\u001b[0m\u001b]8;;https://example.test\u001b\\link\u001b]8;;\u001b\\";

        Assert.That(ReportCoordinator.StripAnsi(Colored), Is.EqualTo("violationlink"));
    }

    // Uses TestCaseData with an explicit .SetName(...) rather than [TestCase(...)] because
    // the control-character arguments below make NUnit generate a display name containing
    // literal backslash-escape sequences (e.g. \u001b7saved). That generated name becomes
    // this test case's FullyQualifiedName, and the VSTest/NUnit3TestAdapter filter-matching
    // path re-parses FullyQualifiedName through the same backslash-escape grammar used for
    // filter expression text; a backslash not followed by one of the filter DSL's recognized
    // escape targets (\(){}&|=!~) throws there, and the case is silently excluded from every
    // FullyQualifiedName filter bucket in make/test.mk (see issue #480). An explicit ASCII,
    // backslash-free .SetName(...) keeps FullyQualifiedName filter-safe while leaving the
    // actual control-byte arguments passed to StripAnsi unchanged.
    private static IEnumerable<TestCaseData> TerminatorsC1SequencesAndMalformedInputCases()
    {
        yield return new TestCaseData("\u001b]0;title\u0007visible", "visible")
            .SetName("StripAnsi_HandlesTerminatorsC1SequencesAndMalformedInput_BelTerminatedOscSequence");
        yield return new TestCaseData("\u009b31mred\u009b0m", "red")
            .SetName("StripAnsi_HandlesTerminatorsC1SequencesAndMalformedInput_C1CsiSequence");
        yield return new TestCaseData("\u009d0;title\u009cvisible", "visible")
            .SetName("StripAnsi_HandlesTerminatorsC1SequencesAndMalformedInput_C1OscSequence");
        yield return new TestCaseData("\u001b7saved", "saved")
            .SetName("StripAnsi_HandlesTerminatorsC1SequencesAndMalformedInput_UnrecognizedEscTerminator");
        yield return new TestCaseData("\u001b\u001b[31mred", "red")
            .SetName("StripAnsi_HandlesTerminatorsC1SequencesAndMalformedInput_DoubleEscBeforeCsi");
        yield return new TestCaseData("\u001b[31\nvisible", "\nvisible")
            .SetName("StripAnsi_HandlesTerminatorsC1SequencesAndMalformedInput_NewlineTerminatesUnclosedCsi");
        yield return new TestCaseData("\u001bé", "é")
            .SetName("StripAnsi_HandlesTerminatorsC1SequencesAndMalformedInput_NonAsciiAfterEsc");
    }

    [TestCaseSource(nameof(TerminatorsC1SequencesAndMalformedInputCases))]
    public void StripAnsi_HandlesTerminatorsC1SequencesAndMalformedInput(string input, string expected)
    {
        Assert.That(ReportCoordinator.StripAnsi(input), Is.EqualTo(expected));
    }
}
