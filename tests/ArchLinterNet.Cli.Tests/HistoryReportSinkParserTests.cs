using ArchLinterNet.Cli.Commands.History.Application;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class HistoryReportSinkParserTests
{
    [Test]
    public void NullInputProducesNoSinks()
    {
        Assert.That(HistoryReportSinkParser.Parse(null), Is.Empty);
    }

    [Test]
    public void EmptyInputProducesNoSinks()
    {
        Assert.That(HistoryReportSinkParser.Parse(Array.Empty<string>()), Is.Empty);
    }

    [Test]
    public void JsonToAFilePathParsesAsAFileSink()
    {
        IReadOnlyList<HistoryReportSink> sinks = HistoryReportSinkParser.Parse(new[] { "json=report.json" });

        Assert.That(sinks, Is.EqualTo(new[]
        {
            new HistoryReportSink("json", HistoryReportDestinationType.File, "report.json"),
        }));
    }

    [Test]
    public void MarkdownToStdoutParsesAsAStdoutSink()
    {
        IReadOnlyList<HistoryReportSink> sinks = HistoryReportSinkParser.Parse(new[] { "markdown=stdout" });

        Assert.That(sinks, Is.EqualTo(new[]
        {
            new HistoryReportSink("markdown", HistoryReportDestinationType.Stdout),
        }));
    }

    [Test]
    public void JsonToStderrParsesAsAStderrSink()
    {
        IReadOnlyList<HistoryReportSink> sinks = HistoryReportSinkParser.Parse(new[] { "json=stderr" });

        Assert.That(sinks, Is.EqualTo(new[]
        {
            new HistoryReportSink("json", HistoryReportDestinationType.Stderr),
        }));
    }

    [Test]
    public void MultipleValidValuesParseInOrder()
    {
        IReadOnlyList<HistoryReportSink> sinks = HistoryReportSinkParser.Parse(new[] { "json=report.json", "markdown=stderr" });

        Assert.That(sinks, Is.EqualTo(new[]
        {
            new HistoryReportSink("json", HistoryReportDestinationType.File, "report.json"),
            new HistoryReportSink("markdown", HistoryReportDestinationType.Stderr),
        }));
    }

    [TestCase("json")]
    [TestCase("=stdout")]
    [TestCase("json=")]
    public void AMalformedValueThrows(string raw)
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => HistoryReportSinkParser.Parse(new[] { raw }))!;

        Assert.That(exception.Message, Does.Contain("Invalid --report value"));
    }

    [Test]
    public void AnUnsupportedFormatThrows()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => HistoryReportSinkParser.Parse(new[] { "sarif=report.sarif" }))!;

        Assert.That(exception.Message, Does.Contain("Invalid format in --report"));
    }

    [Test]
    public void TwoSinksTargetingStdoutThrowAsDuplicateDestinations()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => HistoryReportSinkParser.Parse(new[] { "json=stdout", "markdown=stdout" }))!;

        Assert.That(exception.Message, Does.Contain("Duplicate --report destination"));
    }

    [Test]
    public void TwoSinksTargetingTheSameFilePathThrowAsDuplicateDestinations()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => HistoryReportSinkParser.Parse(new[] { "json=report.out", "markdown=report.out" }))!;

        Assert.That(exception.Message, Does.Contain("Duplicate --report destination"));
    }
}
