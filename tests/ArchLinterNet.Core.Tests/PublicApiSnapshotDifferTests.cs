using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class PublicApiSnapshotDifferTests
{
    private static readonly string[] _value = { "class Acme.Module.Other" };
    private static readonly string[] _value1 = { "class Acme.Module.Gone" };
    private static readonly string[] _value2 = { "const Acme.Module.Color.Blue: Acme.Module.Color" };
    private static readonly string[] _value3 = { "const Acme.Module.Thing.Version: System.Int32" };
    private const string Assembly = "Acme.Module";

    private static PublicApiSnapshotEntry[] Entries(params string[] signatures)
    {
        return signatures.Select(signature => new PublicApiSnapshotEntry(Assembly, signature)).ToArray();
    }

    [Test]
    public void Diff_IdenticalSurfaces_ReportsNoChanges()
    {
        PublicApiDelta delta = PublicApiSnapshotDiffer.Diff(
            Entries("class Acme.Module.Thing"), Entries("class Acme.Module.Thing"));

        Assert.That(delta.HasChanges, Is.False);
    }

    [Test]
    public void Diff_NewSignatureWithNoDeclaredCounterpart_IsAddition()
    {
        PublicApiDelta delta = PublicApiSnapshotDiffer.Diff(
            Entries("class Acme.Module.Thing"),
            Entries("class Acme.Module.Thing", "class Acme.Module.Other"));

        Assert.Multiple(() =>
        {
            Assert.That(delta.Added.Select(e => e.Signature), Is.EqualTo(_value));
            Assert.That(delta.Removed, Is.Empty);
            Assert.That(delta.Changed, Is.Empty);
        });
    }

    [Test]
    public void Diff_DeclaredSignatureNoLongerExported_IsRemoval()
    {
        PublicApiDelta delta = PublicApiSnapshotDiffer.Diff(
            Entries("class Acme.Module.Thing", "class Acme.Module.Gone"),
            Entries("class Acme.Module.Thing"));

        Assert.Multiple(() =>
        {
            Assert.That(delta.Removed.Select(e => e.Signature), Is.EqualTo(_value1));
            Assert.That(delta.Removed[0].PreviousSignature, Is.EqualTo("class Acme.Module.Gone"));
            Assert.That(delta.Added, Is.Empty);
        });
    }

    [Test]
    public void Diff_ChangedReturnType_IsOneChangeCarryingBothSignatures()
    {
        PublicApiDelta delta = PublicApiSnapshotDiffer.Diff(
            Entries("method Acme.Module.Thing.Do(System.Int32): System.Void"),
            Entries("method Acme.Module.Thing.Do(System.Int32): System.Boolean"));

        Assert.Multiple(() =>
        {
            Assert.That(delta.Added, Is.Empty);
            Assert.That(delta.Removed, Is.Empty);
            Assert.That(delta.Changed, Has.Count.EqualTo(1));
            Assert.That(delta.Changed[0].Signature, Is.EqualTo("method Acme.Module.Thing.Do(System.Int32): System.Boolean"));
            Assert.That(delta.Changed[0].PreviousSignature, Is.EqualTo("method Acme.Module.Thing.Do(System.Int32): System.Void"));
        });
    }

    [Test]
    public void Diff_ChangedParameterType_IsChangeNotAddRemovePair()
    {
        PublicApiDelta delta = PublicApiSnapshotDiffer.Diff(
            Entries("method Acme.Module.Thing.Do(System.Int32): System.Void"),
            Entries("method Acme.Module.Thing.Do(System.Int64): System.Void"));

        Assert.That(delta.Changed, Has.Count.EqualTo(1));
    }

    [Test]
    public void Diff_NewOverloadWithDifferentArity_IsAddition()
    {
        PublicApiDelta delta = PublicApiSnapshotDiffer.Diff(
            Entries("method Acme.Module.Thing.Do(System.Int32): System.Void"),
            Entries(
                "method Acme.Module.Thing.Do(System.Int32): System.Void",
                "method Acme.Module.Thing.Do(System.Int32, System.String): System.Void"));

        Assert.Multiple(() =>
        {
            Assert.That(delta.Added, Has.Count.EqualTo(1));
            Assert.That(delta.Changed, Is.Empty);
        });
    }

    [Test]
    public void Diff_GenericArgumentCommasDoNotSplitParameters()
    {
        PublicApiDelta delta = PublicApiSnapshotDiffer.Diff(
            Entries("method Acme.Module.Thing.Do(System.Collections.Generic.Dictionary`2[System.String,System.Int32]): System.Void"),
            Entries("method Acme.Module.Thing.Do(System.Collections.Generic.Dictionary`2[System.String,System.Int64]): System.Void"));

        Assert.That(delta.Changed, Has.Count.EqualTo(1));
    }

    // The identity parser finds a method's parameter list by locating its closing paren. A
    // zero-parameter generic method's exact signature ends with `(): ... [where0:class new()]` —
    // the detail suffix's own `new()` contains a closing paren *after* the true parameter-list
    // paren. A naive last-closing-paren search would swallow everything up to that one as "the
    // parameter list" and misparse a 0-parameter method as having one, splitting one changed
    // constraint into an unrelated addition plus removal instead of a single change.
    [Test]
    public void Diff_GenericConstraintChangeOnZeroParameterMethod_IsOneChangeNotAddPlusRemove()
    {
        PublicApiDelta delta = PublicApiSnapshotDiffer.Diff(
            Entries("method Acme.Module.Thing.Do`1(): System.Void [where0:class]"),
            Entries("method Acme.Module.Thing.Do`1(): System.Void [where0:class new()]"));

        Assert.Multiple(() =>
        {
            Assert.That(delta.Added, Is.Empty);
            Assert.That(delta.Removed, Is.Empty);
            Assert.That(delta.Changed, Has.Count.EqualTo(1));
        });
    }

    // A string constant's value can legitimately contain " [" or a trailing "]" — the detail-suffix
    // escaping in ArchitecturePublicApiSignatureDetails.Quote() keeps the identity parser from being
    // confused by it, so a retyped/revalued bracket-containing constant still correlates as one
    // change rather than a garbled add/remove pair.
    [Test]
    public void Diff_StringConstantValueContainingEscapedBrackets_CorrelatesAsOneChange()
    {
        PublicApiDelta delta = PublicApiSnapshotDiffer.Diff(
            Entries("const Acme.Module.Thing.Label: System.String [value:\"foo \\[bar\\]\"]"),
            Entries("const Acme.Module.Thing.Label: System.String [value:\"foo \\[baz\\]\"]"));

        Assert.Multiple(() =>
        {
            Assert.That(delta.Added, Is.Empty);
            Assert.That(delta.Removed, Is.Empty);
            Assert.That(delta.Changed, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Diff_EnumMemberAddedAndConstantRetyped_AreReportedSeparately()
    {
        PublicApiDelta delta = PublicApiSnapshotDiffer.Diff(
            Entries("const Acme.Module.Color.Red: Acme.Module.Color", "const Acme.Module.Thing.Version: System.String"),
            Entries(
                "const Acme.Module.Color.Red: Acme.Module.Color",
                "const Acme.Module.Color.Blue: Acme.Module.Color",
                "const Acme.Module.Thing.Version: System.Int32"));

        Assert.Multiple(() =>
        {
            Assert.That(delta.Added.Select(e => e.Signature), Is.EqualTo(_value2));
            Assert.That(delta.Changed.Select(e => e.Signature), Is.EqualTo(_value3));
            Assert.That(delta.Removed, Is.Empty);
        });
    }

    // Two assemblies may legitimately export the same fully qualified signature. Without assembly
    // qualification, removing it from one is masked by the copy in the other.
    [Test]
    public void Diff_SameSignatureInAnotherAssembly_DoesNotMaskARemoval()
    {
        PublicApiDelta delta = PublicApiSnapshotDiffer.Diff(
            new[]
            {
                new PublicApiSnapshotEntry("Acme.One", "class Shared.Thing"),
                new PublicApiSnapshotEntry("Acme.Two", "class Shared.Thing"),
            },
            new[] { new PublicApiSnapshotEntry("Acme.Two", "class Shared.Thing") });

        Assert.Multiple(() =>
        {
            Assert.That(delta.Removed, Has.Count.EqualTo(1));
            Assert.That(delta.Removed[0].AssemblyName, Is.EqualTo("Acme.One"));
            Assert.That(delta.Changed, Is.Empty);
        });
    }

    [Test]
    public void Diff_SameIdentityInDifferentAssemblies_IsNotPairedAsAChange()
    {
        PublicApiDelta delta = PublicApiSnapshotDiffer.Diff(
            new[] { new PublicApiSnapshotEntry("Acme.One", "method Shared.Thing.Do(): System.Void") },
            new[] { new PublicApiSnapshotEntry("Acme.Two", "method Shared.Thing.Do(): System.Int32") });

        Assert.Multiple(() =>
        {
            Assert.That(delta.Changed, Is.Empty);
            Assert.That(delta.Removed, Has.Count.EqualTo(1));
            Assert.That(delta.Added, Has.Count.EqualTo(1));
        });
    }

    // Legacy inline `declared_api` entries have no assembly, so they must match any assembly rather
    // than failing to match every one of them.
    [Test]
    public void Diff_WildcardDeclaredEntryMatchesAnyAssembly()
    {
        PublicApiDelta delta = PublicApiSnapshotDiffer.Diff(
            new[] { new PublicApiSnapshotEntry(PublicApiSnapshotDiffer.WildcardAssembly, "class Shared.Thing") },
            new[] { new PublicApiSnapshotEntry("Acme.One", "class Shared.Thing") });

        Assert.That(delta.HasChanges, Is.False);
    }

    [Test]
    public void Diff_WildcardDeclaredEntryIsBoundToTheAssemblyThatChangedIt()
    {
        PublicApiDelta delta = PublicApiSnapshotDiffer.Diff(
            new[]
            {
                new PublicApiSnapshotEntry(
                    PublicApiSnapshotDiffer.WildcardAssembly, "method Shared.Thing.Do(): System.Void"),
            },
            new[] { new PublicApiSnapshotEntry("Acme.One", "method Shared.Thing.Do(): System.Int32") });

        Assert.Multiple(() =>
        {
            Assert.That(delta.Changed, Has.Count.EqualTo(1));
            Assert.That(delta.Changed[0].AssemblyName, Is.EqualTo("Acme.One"));
        });
    }

    [Test]
    public void Diff_OrderingIsDeterministicRegardlessOfInputOrder()
    {
        PublicApiSnapshotEntry[] declared = Entries("class Acme.Module.A");
        PublicApiSnapshotEntry[] forward = Entries("class Acme.Module.A", "class Acme.Module.C", "class Acme.Module.B");
        PublicApiSnapshotEntry[] reversed = forward.Reverse().ToArray();

        PublicApiDelta first = PublicApiSnapshotDiffer.Diff(declared, forward);
        PublicApiDelta second = PublicApiSnapshotDiffer.Diff(declared, reversed);

        Assert.That(first.Added.Select(e => e.Signature), Is.EqualTo(second.Added.Select(e => e.Signature)));
    }
}
