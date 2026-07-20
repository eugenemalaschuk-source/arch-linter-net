using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureTypeNamesTests
{
    [Test]
    public void SafeNamespace_OrdinaryType_ReturnsNamespace()
    {
        Assert.That(ArchitectureTypeNames.SafeNamespace(typeof(ArchitectureTypeNamesTests)), Is.EqualTo("ArchLinterNet.Core.Tests"));
    }

    [Test]
    public void SafeFullName_OrdinaryType_ReturnsFullName()
    {
        Assert.That(ArchitectureTypeNames.SafeFullName(typeof(ArchitectureTypeNamesTests)),
            Is.EqualTo(typeof(ArchitectureTypeNamesTests).FullName));
    }

    [Test]
    public void SafeAssemblyName_OrdinaryType_ReturnsContainingAssemblyName()
    {
        string? assemblyName = ArchitectureTypeNames.SafeAssemblyName(typeof(ArchitectureTypeNamesTests));

        Assert.That(assemblyName, Is.EqualTo(typeof(ArchitectureTypeNamesTests).Assembly.GetName().Name));
    }

    [Test]
    public void SafeAssemblyName_TypeFromAnotherAssembly_ReturnsThatAssemblyName()
    {
        // A type from a different, ordinary assembly (not this test assembly) exercises the same
        // code path as scanning a referenced type from elsewhere in the target codebase.
        string? assemblyName = ArchitectureTypeNames.SafeAssemblyName(typeof(string));

        Assert.That(assemblyName, Is.EqualTo(typeof(string).Assembly.GetName().Name));
    }
}
