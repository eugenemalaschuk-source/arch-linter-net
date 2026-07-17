using System.Reflection;
using NUnit.Framework;

namespace ArchLinterNet.CEL.Tests;

/// <summary>
/// Proves <see cref="CelEvaluateCallGraphNeverReachesCompilePipelineTests.ResolveVirtualTargets"/>'s
/// interface-dispatch branch actually finds implementing methods — both implicit and explicit —
/// rather than silently returning nothing for interface calls. This is the "separate positive
/// test" the interface-dispatch closure needs: without it, a bug that made the interface branch a
/// no-op (e.g. an always-false comparison, or a mapping lookup that never matches) would leave
/// <c>Evaluate_CallGraph_NeverReachesTokenizerParserOrBinder</c> green for the wrong reason — no
/// interface calls currently exist in <c>Evaluate()</c>'s call graph, so that test's silence alone
/// cannot distinguish "the branch works and found nothing to explore" from "the branch is broken
/// and explores nothing no matter what."
/// </summary>
[TestFixture]
public sealed class CelInterfaceDispatchClosureSanityCheckTests
{
    private interface ISanityCheckInterface
    {
        void ImplicitMethod();

        void ExplicitMethod();
    }

    private sealed class ImplicitImplementation : ISanityCheckInterface
    {
        public void ImplicitMethod()
        {
        }

        void ISanityCheckInterface.ExplicitMethod()
        {
        }
    }

    private sealed class AnotherImplicitImplementation : ISanityCheckInterface
    {
        public void ImplicitMethod()
        {
        }

        void ISanityCheckInterface.ExplicitMethod()
        {
        }
    }

    [Test]
    public void ResolveVirtualTargets_FindsImplicitInterfaceImplementationsAcrossMultipleTypes()
    {
        var interfaceMethod = typeof(ISanityCheckInterface).GetMethod(nameof(ISanityCheckInterface.ImplicitMethod))!;
        var unresolvedEdges = new List<string>();

        var targets = CelEvaluateCallGraphNeverReachesCompilePipelineTests.ResolveVirtualTargets(
            interfaceMethod, typeof(ISanityCheckInterface).Assembly, unresolvedEdges);

        var implicitOnImplicitImpl = typeof(ImplicitImplementation).GetMethod(nameof(ImplicitImplementation.ImplicitMethod))!;
        var implicitOnAnotherImpl = typeof(AnotherImplicitImplementation).GetMethod(nameof(AnotherImplicitImplementation.ImplicitMethod))!;

        Assert.That(unresolvedEdges, Is.Empty,
            "These well-formed synthetic types must map cleanly — a non-empty list here would mean " +
            "the fail-closed reporting path itself is misfiring on ordinary input.");
        Assert.That(targets, Does.Contain(interfaceMethod),
            "The token-resolved interface method itself must still be in the result.");
        Assert.That(targets, Does.Contain(implicitOnImplicitImpl),
            "Must find the implicit implementation on ImplicitImplementation.");
        Assert.That(targets, Does.Contain(implicitOnAnotherImpl),
            "Must find the implicit implementation on a second, independent implementing type.");
    }

    [Test]
    public void ResolveVirtualTargets_FindsExplicitInterfaceImplementations()
    {
        var interfaceMethod = typeof(ISanityCheckInterface).GetMethod(nameof(ISanityCheckInterface.ExplicitMethod))!;
        var unresolvedEdges = new List<string>();

        var targets = CelEvaluateCallGraphNeverReachesCompilePipelineTests.ResolveVirtualTargets(
            interfaceMethod, typeof(ISanityCheckInterface).Assembly, unresolvedEdges);

        // Explicit interface implementations are private methods named
        // "<Namespace>.ISanityCheckInterface.ExplicitMethod" — GetMethod(name) cannot find them by
        // their simple name, so locate via GetInterfaceMap directly (the same mechanism
        // ResolveVirtualTargets itself uses) to name the expected target unambiguously.
        var mapping = typeof(ImplicitImplementation).GetInterfaceMap(typeof(ISanityCheckInterface));
        var expectedExplicitTarget = mapping.TargetMethods[Array.IndexOf(mapping.InterfaceMethods, interfaceMethod)];

        Assert.That(unresolvedEdges, Is.Empty);
        Assert.That(targets, Does.Contain(interfaceMethod),
            "The token-resolved interface method itself must still be in the result.");
        Assert.That(targets, Does.Contain(expectedExplicitTarget),
            "Must find the explicit implementation, proving GetInterfaceMap-based resolution — not " +
            "just a name-based lookup that would miss explicit implementations entirely.");
    }
}
