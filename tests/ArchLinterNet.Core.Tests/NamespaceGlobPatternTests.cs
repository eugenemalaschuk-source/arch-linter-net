using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class NamespaceGlobPatternTests
{
    [Test]
    public void Parse_NoGlobChars_ReturnsNonGlobPattern()
    {
        var pattern = NamespaceGlobPattern.Parse("FirstIce.Game.Features");

        Assert.That(pattern.IsGlob, Is.False);
    }

    [Test]
    public void Parse_GlobPattern_ReturnsGlobPattern()
    {
        var pattern = NamespaceGlobPattern.Parse("FirstIce.Game.Features.*");

        Assert.That(pattern.IsGlob, Is.True);
    }

    [Test]
    public void Parse_BareWildcard_ThrowsInvalidNamespacePatternException()
    {
        var ex = Assert.Throws<InvalidNamespacePatternException>(() =>
            NamespaceGlobPattern.Parse("*"));

        Assert.That(ex!.Message, Does.Contain("Bare wildcard"));
    }

    [Test]
    public void Parse_DoubleStar_ThrowsInvalidNamespacePatternException()
    {
        var ex = Assert.Throws<InvalidNamespacePatternException>(() =>
            NamespaceGlobPattern.Parse("FirstIce.**"));

        Assert.That(ex!.Message, Does.Contain("'**'"));
    }

    [Test]
    public void Parse_QuestionMark_ThrowsInvalidNamespacePatternException()
    {
        var ex = Assert.Throws<InvalidNamespacePatternException>(() =>
            NamespaceGlobPattern.Parse("FirstIce.Game.Features.?"));

        Assert.That(ex!.Message, Does.Contain("'?'"));
    }

    [Test]
    public void Parse_PartialSegmentWildcard_ThrowsInvalidNamespacePatternException()
    {
        var ex = Assert.Throws<InvalidNamespacePatternException>(() =>
            NamespaceGlobPattern.Parse("FirstIce.Game.Features.Foo*"));

        Assert.That(ex!.Message, Does.Contain("Partial segment"));
    }

    [Test]
    public void Parse_PartialSegmentWildcardPrefix_ThrowsInvalidNamespacePatternException()
    {
        var ex = Assert.Throws<InvalidNamespacePatternException>(() =>
            NamespaceGlobPattern.Parse("FirstIce.Game.Features.*Bar"));

        Assert.That(ex!.Message, Does.Contain("Partial segment"));
    }

    [Test]
    public void Parse_EmptySegment_ThrowsInvalidNamespacePatternException()
    {
        var ex = Assert.Throws<InvalidNamespacePatternException>(() =>
            NamespaceGlobPattern.Parse("FirstIce.Game..Features"));

        Assert.That(ex!.Message, Does.Contain("Empty segment"));
    }

    [Test]
    public void Parse_LeadingWildcard_ThrowsInvalidNamespacePatternException()
    {
        var ex = Assert.Throws<InvalidNamespacePatternException>(() =>
            NamespaceGlobPattern.Parse("*.Features"));

        Assert.That(ex!.Message, Does.Contain("Leading wildcard"));
    }

    [Test]
    public void Parse_CharacterClassBrackets_ThrowsInvalidNamespacePatternException()
    {
        var ex = Assert.Throws<InvalidNamespacePatternException>(() =>
            NamespaceGlobPattern.Parse("FirstIce.[ab].Features"));

        Assert.That(ex!.Message, Does.Contain("Character classes"));
    }

    [Test]
    public void Match_ExactNamespace_ReturnsMatched()
    {
        var pattern = NamespaceGlobPattern.Parse("FirstIce.Game.Features.*");
        var result = pattern.Match("FirstIce.Game.Features.Audio");

        Assert.That(result.Matched, Is.True);
        Assert.That(result.MatchedNamespacePrefix, Is.EqualTo("FirstIce.Game.Features.Audio"));
    }

    [Test]
    public void Match_DescendantNamespace_ReturnsMatched()
    {
        var pattern = NamespaceGlobPattern.Parse("FirstIce.Game.Features.*");
        var result = pattern.Match("FirstIce.Game.Features.Audio.Player");

        Assert.That(result.Matched, Is.True);
        Assert.That(result.MatchedNamespacePrefix, Is.EqualTo("FirstIce.Game.Features.Audio"));
    }

    [Test]
    public void Match_FewerSegmentsThanPattern_ReturnsNotMatched()
    {
        var pattern = NamespaceGlobPattern.Parse("FirstIce.Game.Features.*");
        var result = pattern.Match("FirstIce.Game.Features");

        Assert.That(result.Matched, Is.False);
    }

    [Test]
    public void Match_DifferentLiteral_ReturnsNotMatched()
    {
        var pattern = NamespaceGlobPattern.Parse("FirstIce.Game.Features.*");
        var result = pattern.Match("FirstIce.Game.Audio.Services");

        Assert.That(result.Matched, Is.False);
    }

    [Test]
    public void Match_MultipleWildcards_ReturnsMatched()
    {
        var pattern = NamespaceGlobPattern.Parse("A.*.B.*");
        var result = pattern.Match("A.Foo.B.Bar.Baz");

        Assert.That(result.Matched, Is.True);
        Assert.That(result.MatchedNamespacePrefix, Is.EqualTo("A.Foo.B.Bar"));
    }

    [Test]
    public void Match_GlobInMiddle_ReturnsMatched()
    {
        var pattern = NamespaceGlobPattern.Parse("FirstIce.*.Features");
        var result = pattern.Match("FirstIce.Game.Features.Module");

        Assert.That(result.Matched, Is.True);
        Assert.That(result.MatchedNamespacePrefix, Is.EqualTo("FirstIce.Game.Features"));
    }

    [Test]
    public void SpecificityScore_NoWildcard_ReturnsLiteralCountTimesTen()
    {
        var pattern = NamespaceGlobPattern.Parse("FirstIce.Game.Features");

        Assert.That(pattern.SpecificityScore, Is.EqualTo(30));
    }

    [Test]
    public void SpecificityScore_OneWildcard_ReducesScore()
    {
        var pattern = NamespaceGlobPattern.Parse("FirstIce.Game.Features.*");

        Assert.That(pattern.SpecificityScore, Is.EqualTo(29));
    }

    [Test]
    public void SpecificityScore_TwoWildcards_ReducesScoreFurther()
    {
        var pattern = NamespaceGlobPattern.Parse("FirstIce.*.Features.*");

        Assert.That(pattern.SpecificityScore, Is.EqualTo(18));
    }
}
