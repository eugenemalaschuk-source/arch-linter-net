using ArchLinterNet.Cli.Infrastructure;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class SystemCliConsoleTests
{
    [Test]
    public void WritesToCurrentConsoleStreams()
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        using var outWriter = new StringWriter();
        using var errorWriter = new StringWriter();

        try
        {
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);

            var console = new SystemCliConsole();
            console.Out.Write("hello");
            console.Error.Write("boom");

            Assert.Multiple(() =>
            {
                Assert.That(outWriter.ToString(), Is.EqualTo("hello"));
                Assert.That(errorWriter.ToString(), Is.EqualTo("boom"));
            });
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Test]
    public void RedirectedStreamsStripAnsiAndPreserveOscLinkText()
    {
        using var outWriter = new StringWriter();
        using var errorWriter = new StringWriter();
        var console = new SystemCliConsole(
            outWriter,
            errorWriter,
            outputRedirected: true,
            errorRedirected: true);

        console.Out.Write("\u001b[");
        console.Out.Write("31mviolation\u001b[0m\u001b]8;;https://example.test\u001b");
        console.Out.Write("\\");
        console.Out.Write("link");
        console.Out.Write("\u001b]8;;\u001b");
        console.Out.Write("\\");
        console.Error.Write("\u001b[33mwarning\u001b[0m");

        Assert.Multiple(() =>
        {
            Assert.That(outWriter.ToString(), Is.EqualTo("violationlink"));
            Assert.That(errorWriter.ToString(), Is.EqualTo("warning"));
        });
    }
}
