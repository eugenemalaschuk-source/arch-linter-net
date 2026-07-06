using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureGeneratedFileFilterTests
{
    [TestCase("/repo/src/Project/bin/Debug/net10.0/Generated.cs")]
    [TestCase("/repo/src/Project/obj/Debug/net10.0/Project.AssemblyInfo.cs")]
    public void IsExcluded_BuildOutputDirectory_ReturnsTrue(string path)
    {
        Assert.That(ArchitectureGeneratedFileFilter.IsExcluded(path), Is.True);
    }

    [TestCase("/repo/Library/ScriptAssemblies/Assembly-CSharp.cs")]
    [TestCase("/repo/Temp/Bee/Fake.cs")]
    [TestCase("/repo/PackageCache/com.example.pkg@1.0.0/Runtime/Widget.cs")]
    public void IsExcluded_UnityGeneratedDirectory_ReturnsTrue(string path)
    {
        Assert.That(ArchitectureGeneratedFileFilter.IsExcluded(path), Is.True);
    }

    [TestCase("/repo/src/Project/Widget.g.cs")]
    [TestCase("/repo/src/Project/Widget.g.i.cs")]
    [TestCase("/repo/src/Project/Widget.designer.cs")]
    public void IsExcluded_GeneratedFilenameSuffix_ReturnsTrue(string path)
    {
        Assert.That(ArchitectureGeneratedFileFilter.IsExcluded(path), Is.True);
    }

    [TestCase("/repo/src/Project/Widget.cs")]
    [TestCase("/repo/src/Project/Nested/Feature.cs")]
    public void IsExcluded_OrdinarySourceFile_ReturnsFalse(string path)
    {
        Assert.That(ArchitectureGeneratedFileFilter.IsExcluded(path), Is.False);
    }
}
