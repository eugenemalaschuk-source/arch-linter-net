using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// PR #416 review round 3: FindTypesInLayer/FindTypesInNamespace previously materialized the full
// candidate type set (SelectMany over every target assembly's GetLoadableTypes) with no
// cancellation check at all, before ArchitectureIlMethodBodyScanner's own per-type loop over the
// result ever got a chance to run. These prove the token now reaches type discovery itself.
[TestFixture]
public sealed class ArchitectureTypeScannerCancellationTests
{
    private static readonly Assembly[] _fixtureAssembly = [typeof(ArchitectureTypeScannerCancellationTests).Assembly];

    [Test]
    public void FindTypesInLayer_PreCancelledToken_ThrowsOperationCanceledException()
    {
        var layer = new ArchitectureLayer { Namespace = "ArchLinterNet.Core.Tests" };
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            ArchitectureTypeScanner.FindTypesInLayer(_fixtureAssembly, layer, cts.Token));
    }

    [Test]
    public void FindTypesInNamespace_PreCancelledToken_ThrowsOperationCanceledException()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            ArchitectureTypeScanner.FindTypesInNamespace(_fixtureAssembly, "ArchLinterNet.Core.Tests", cts.Token));
    }

    // Proves the check is genuinely per assembly, not just before/after the whole scan: a second
    // assembly's types must never be fetched once cancellation is observed while the first is
    // being processed.
    [Test]
    public void FindTypesInLayer_CancelledAfterFirstAssembly_NeverEnumeratesTheSecond()
    {
        var layer = new ArchitectureLayer { Namespace = "ArchLinterNet.Core.Tests" };
        using CancellationTokenSource cts = new();
        var assemblies = new CancelOnSecondAssembly(
            new[] { typeof(ArchitectureTypeScannerCancellationTests).Assembly, typeof(NUnit.Framework.Assert).Assembly }, cts);

        Assert.Throws<OperationCanceledException>(() =>
            ArchitectureTypeScanner.FindTypesInLayer(assemblies, layer, cts.Token));

        Assert.That(assemblies.FetchedCount, Is.EqualTo(2),
            "the loop must stop as soon as cancellation is observed while fetching the second assembly — a third would prove it kept going");
    }

    private sealed class CancelOnSecondAssembly : IEnumerable<Assembly>
    {
        private readonly IReadOnlyList<Assembly> _items;
        private readonly CancellationTokenSource _cts;

        public CancelOnSecondAssembly(IReadOnlyList<Assembly> items, CancellationTokenSource cts)
        {
            _items = items;
            _cts = cts;
        }

        public int FetchedCount { get; private set; }

        public IEnumerator<Assembly> GetEnumerator()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (i == 1)
                {
                    _cts.Cancel();
                }

                FetchedCount++;
                yield return _items[i];
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
