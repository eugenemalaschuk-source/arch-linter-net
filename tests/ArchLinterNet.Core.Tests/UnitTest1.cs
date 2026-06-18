using ArchLinterNet.Core;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public class ArchitectureValidatorTests
{
    [Test]
    public void Validate_ShouldReturnTrue()
    {
        IArchitectureValidator validator = new ArchitectureValidator();
        var result = validator.Validate("dummy.yml");
        Assert.That(result, Is.True);
    }
}
