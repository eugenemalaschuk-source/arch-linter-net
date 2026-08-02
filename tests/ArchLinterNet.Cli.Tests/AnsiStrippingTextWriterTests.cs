using System.Text;
using ArchLinterNet.Cli.Infrastructure;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

// AnsiStrippingTextWriter wraps redirected stdout/stderr. It used to forward every visible
// character to the inner writer individually, which made a real report unusably slow to publish —
// a 45 MB `--format json` document took ~43 s to reach a redirected stdout, an order of magnitude
// longer than producing it (issue #419). Visible characters are batched now; these tests pin that
// batching changed only how the characters are delivered, never which ones or in what order.
[TestFixture]
public sealed class AnsiStrippingTextWriterTests
{
    [Test]
    public void Write_ContentWithEscapeSequences_MatchesStripper()
    {
        const string Content = "plain [33mcoloured[0m tail\n";
        var inner = new StringWriter();

        using (var writer = new AnsiStrippingTextWriter(inner))
        {
            writer.Write(Content);
        }

        Assert.That(inner.ToString(), Is.EqualTo(AnsiEscapeSequenceStripper.Strip(Content)));
    }

    // The block boundary must not be a semantic boundary: content far larger than one block has to
    // come out exactly as the single-shot stripper produces it.
    [Test]
    public void Write_PayloadLargerThanOneBlock_MatchesStripper()
    {
        string content = string.Concat(Enumerable.Range(0, 5_000)
            .Select(i => $"line {i} [31mred[0m value\n"));
        var inner = new StringWriter();

        using (var writer = new AnsiStrippingTextWriter(inner))
        {
            writer.Write(content);
        }

        Assert.That(inner.ToString(), Is.EqualTo(AnsiEscapeSequenceStripper.Strip(content)));
    }

    // The parser keeps state between calls, so an escape sequence split across two writes must
    // still be stripped — draining the block between calls must not reset that.
    [Test]
    public void Write_EscapeSequenceSplitAcrossCalls_IsStillStripped()
    {
        var inner = new StringWriter();

        using (var writer = new AnsiStrippingTextWriter(inner))
        {
            writer.Write("before [");
            writer.Write("33mafter");
        }

        Assert.That(inner.ToString(), Is.EqualTo("before after"));
    }

    // Each call must publish everything it was given before returning, so interleaving with a
    // separately written stderr is unchanged by buffering.
    [Test]
    public void Write_EachCall_PublishesBeforeReturning()
    {
        var inner = new StringWriter();
        using var writer = new AnsiStrippingTextWriter(inner);

        writer.Write("first");
        Assert.That(inner.ToString(), Is.EqualTo("first"));

        writer.Write("second");
        Assert.That(inner.ToString(), Is.EqualTo("firstsecond"));
    }

    // The actual regression guard: delivery is in blocks, not one inner write per character.
    [Test]
    public void Write_LargePayload_ReachesInnerWriterInBlocks()
    {
        string content = new('x', 200_000);
        var inner = new CountingTextWriter();

        using (var writer = new AnsiStrippingTextWriter(inner))
        {
            writer.Write(content);
        }

        Assert.That(inner.Written.ToString(), Is.EqualTo(content));
        Assert.That(inner.WriteCallCount, Is.LessThan(content.Length / 100),
            $"expected block delivery, got {inner.WriteCallCount} inner writes for {content.Length} characters");
    }

    private sealed class CountingTextWriter : TextWriter
    {
        public StringBuilder Written { get; } = new();

        public int WriteCallCount { get; private set; }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            WriteCallCount++;
            Written.Append(value);
        }

        public override void Write(ReadOnlySpan<char> buffer)
        {
            WriteCallCount++;
            Written.Append(buffer);
        }

        public override void Write(string? value)
        {
            WriteCallCount++;
            Written.Append(value);
        }
    }
}
