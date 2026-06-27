using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureTypeIndexTests
{
    private static readonly System.Reflection.Assembly[] _targetAssemblies =
        { typeof(ArchitectureTypeIndexTests).Assembly };

    [Test]
    public void FindTypesInLayer_MatchesDirectScannerOutput()
    {
        var layer = new ArchitectureLayer { Namespace = "ArchLinterNet.Core.Tests" };
        var index = new ArchitectureTypeIndex(_targetAssemblies);

        Type[] indexed = index.FindTypesInLayer(layer);
        Type[] direct = ArchitectureTypeScanner.FindTypesInLayer(_targetAssemblies, layer);

        Assert.That(indexed.OrderBy(t => t.FullName), Is.EqualTo(direct.OrderBy(t => t.FullName)));
    }

    [Test]
    public void FindTypesInNamespace_MatchesDirectScannerOutput()
    {
        var index = new ArchitectureTypeIndex(_targetAssemblies);

        Type[] indexed = index.FindTypesInNamespace("ArchLinterNet.Core.Tests");
        Type[] direct = ArchitectureTypeScanner.FindTypesInNamespace(_targetAssemblies, "ArchLinterNet.Core.Tests");

        Assert.That(indexed.OrderBy(t => t.FullName), Is.EqualTo(direct.OrderBy(t => t.FullName)));
    }

    [Test]
    public void FindTypesInLayer_RepeatedCalls_ReuseCachedTypeSet()
    {
        var layer = new ArchitectureLayer { Namespace = "ArchLinterNet.Core.Tests" };
        var index = new ArchitectureTypeIndex(_targetAssemblies);

        Type[] first = index.FindTypesInLayer(layer);
        Type[] second = index.FindTypesInLayer(layer);

        Assert.That(first, Is.EqualTo(second));
    }

    [Test]
    public void FindTypesInLayer_RepeatedCalls_EnumeratesTargetAssembliesOnlyOnce()
    {
        var countingAssemblies = new EnumerationCountingCollection(_targetAssemblies);
        var layer = new ArchitectureLayer { Namespace = "ArchLinterNet.Core.Tests" };
        var index = new ArchitectureTypeIndex(countingAssemblies);

        index.FindTypesInLayer(layer);
        index.FindTypesInNamespace("ArchLinterNet.Core.Tests");
        index.FindTypesInLayer(new ArchitectureLayer { Namespace = "ArchLinterNet.Core" });

        Assert.That(countingAssemblies.EnumerationCount, Is.EqualTo(1));
    }

    private sealed class EnumerationCountingCollection : IReadOnlyCollection<System.Reflection.Assembly>
    {
        private readonly System.Reflection.Assembly[] _items;

        public EnumerationCountingCollection(System.Reflection.Assembly[] items)
        {
            _items = items;
        }

        public int EnumerationCount { get; private set; }

        public int Count => _items.Length;

        public IEnumerator<System.Reflection.Assembly> GetEnumerator()
        {
            EnumerationCount++;
            return _items.AsEnumerable().GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
