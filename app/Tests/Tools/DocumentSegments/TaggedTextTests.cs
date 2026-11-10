using AIStudio.Tools.DocumentSegments;

namespace AIStudio.Tests.Tools.DocumentSegments;

/// <summary>
/// Checks how the text of a segment carries its tags to a model and back.
/// </summary>
[TestFixture]
public sealed class TaggedTextTests
{
    [Test]
    public void TheBuilderNumbersTheTagsAndMergesTheWords()
    {
        var builder = new TaggedTextBuilder().Words("Read ").Words("the ");
        var link = builder.Open();
        builder.Words("manual");
        builder.Close(link);
        var footnote = builder.Placeholder();
        var text = builder.Words(" first.").Build();

        Assert.Multiple(() =>
        {
            Assert.That(link, Is.EqualTo(1));
            Assert.That(footnote, Is.EqualTo(2));
            Assert.That(text.ToString(), Is.EqualTo("Read the <1>manual</1><2/> first."));
            Assert.That(text.PlainText, Is.EqualTo("Read the manual first."));
            Assert.That(text.HasTags, Is.True);
        });
    }

    [Test]
    public void AWrittenTextIsReadAsItWasBuilt()
    {
        const string TEXT = "Lies <1>das <2>Handbuch</2></1> zuerst.<3/>";

        Assert.That(TaggedText.TryRead(TEXT, out var text, out var issue), Is.True, issue);
        Assert.Multiple(() =>
        {
            Assert.That(text!.ToString(), Is.EqualTo(TEXT));
            Assert.That(text.Tokens.Count(token => token.Kind is TaggedTokenKind.OPEN), Is.EqualTo(2));
            Assert.That(text.Tokens.Count(token => token.Kind is TaggedTokenKind.PLACEHOLDER), Is.EqualTo(1));
        });
    }

    [TestCase("x < y and y > z")]
    [TestCase("<b>bold</b>")]
    [TestCase("<01>")]
    [TestCase("< 1 >")]
    [TestCase("</1/>")]
    [TestCase("<1234567>")]
    public void WhatOnlyLooksLikeATagStaysWords(string value)
    {
        Assert.That(TaggedText.TryRead(value, out var text, out var issue), Is.True, issue);
        Assert.Multiple(() =>
        {
            Assert.That(text!.HasTags, Is.False);
            Assert.That(text.PlainText, Is.EqualTo(value));
            Assert.That(TaggedText.ContainsTagLikeText(value), Is.False);
        });
    }

    [TestCase("<1>open", "never closed")]
    [TestCase("closed</1>", "no opening tag")]
    [TestCase("<1><2>overlapping</1></2>", "must not overlap")]
    [TestCase("<1>twice</1><1>twice</1>", "more than one tag")]
    [TestCase("<1/> and <1/>", "more than one tag")]
    public void TagsWhichDoNotFitTogetherAreNamed(string value, string expectedIssue)
    {
        Assert.Multiple(() =>
        {
            Assert.That(TaggedText.TryRead(value, out var text, out var issue), Is.False);
            Assert.That(text, Is.Null);
            Assert.That(issue, Does.Contain(expectedIssue));
        });
    }

    [Test]
    public void TagsOfTheirOwnMayMoveAndPairsMayMoveWithTheirWords()
    {
        var source = Read("Click <1>here</1> to open the <2>settings</2>.<3/>");
        var translation = Read("<3/>Öffnen Sie die <2>Einstellungen</2>, indem Sie <1>hier</1> klicken.");

        Assert.That(translation.HasSameTagsAs(source, out var issue), Is.True, issue);
    }

    [Test]
    public void AMissingAndAnAddedTagAreNamed()
    {
        var source = Read("Click <1>here</1>.<2/>");
        var translation = Read("Klicken Sie hier.<3/>");

        Assert.Multiple(() =>
        {
            Assert.That(translation.HasSameTagsAs(source, out var issue), Is.False);
            Assert.That(issue, Does.Contain("<1>…</1> is missing"));
            Assert.That(issue, Does.Contain("<2/> is missing"));
            Assert.That(issue, Does.Contain("<3/> does not belong"));
        });
    }

    [Test]
    public void APairWhichLeavesItsPairIsNamed()
    {
        var source = Read("<1>See <2>the manual</2></1>.");
        var translation = Read("<1>Siehe</1> <2>das Handbuch</2>.");

        Assert.Multiple(() =>
        {
            Assert.That(translation.HasSameTagsAs(source, out var issue), Is.False);
            Assert.That(issue, Does.Contain("<2>…</2> must stay inside the pair <1>…</1>"));
        });
    }

    [Test]
    public void APairTurnedIntoATagOfItsOwnIsNamed()
    {
        var source = Read("A <1>link</1>.");
        var translation = Read("Ein Link<1/>.");

        Assert.That(translation.HasSameTagsAs(source, out _), Is.False);
    }

    [Test]
    public void RemovingTheTagsLeavesTheWords()
    {
        Assert.That(TaggedText.RemoveTags("<1>Ein</1> Link<2/> und </3>x < y"), Is.EqualTo("Ein Link und x < y"));
    }

    [Test]
    public void WordsWhichReadAsATagAreFound()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TaggedText.ContainsTagLikeText("Press <1> to continue."), Is.True);
            Assert.That(TaggedText.ContainsTagLikeText("Use <br/> for a break."), Is.False);
        });
    }

    [Test]
    public void LettersAreWhatAModelWorksOn()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Read("2026 – 42 %<1/>").HasLetters, Is.False, "Numbers and signs stay as they are.");
            Assert.That(Read("<1>日本語</1>").HasLetters, Is.True, "Letters of any script count.");
        });
    }

    private static TaggedText Read(string value)
    {
        Assert.That(TaggedText.TryRead(value, out var text, out var issue), Is.True, issue);
        return text!;
    }
}