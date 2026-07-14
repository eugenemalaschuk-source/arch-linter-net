using ArchLinterNet.CEL;
using NUnit.Framework;

namespace ArchLinterNet.CEL.Tests;

[TestFixture]
public sealed class CelEngineSmokeTests
{
    [Test]
    public void CelEngine_Instantiates()
    {
        var engine = new CelEngine();

        Assert.That(engine, Is.Not.Null);
    }
}
