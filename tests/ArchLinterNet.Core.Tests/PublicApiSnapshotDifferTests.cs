using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class PublicApiSnapshotDifferTests
{
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
            Assert.That(delta.Added.Select(e => e.Signature), Is.EqualTo(new[] { "class Acme.Module.Other" }));
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
            Assert.That(delta.Removed.Select(e => e.Signature), Is.EqualTo(new[] { "class Acme.Module.Gone" }));
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
            Assert.That(delta.Added.Select(e => e.Signature), Is.EqualTo(new[] { "const Acme.Module.Color.Blue: Acme.Module.Color" }));
            Assert.That(delta.Changed.Select(e => e.Signature), Is.EqualTo(new[] { "const Acme.Module.Thing.Version: System.Int32" }));
            Assert.That(delta.Removed, Is.Empty);
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
