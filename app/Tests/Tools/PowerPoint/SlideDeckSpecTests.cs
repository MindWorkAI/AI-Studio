using AIStudio.Tools.PowerPoint;

namespace AIStudio.Tests.Tools.PowerPoint;

/// <summary>
/// Checks how the slide deck is read out of a model answer, and how it is shown as an outline.
/// </summary>
[TestFixture]
public sealed class SlideDeckSpecTests
{
    [Test]
    public void TheJsonIsReadOutOfProseAndACodeFence()
    {
        const string ANSWER = """
            Here is your presentation:
            ```json
            { "title": "Mars", "slides": [ { "layout": "L3", "title": "Why Mars", "texts": [["Close to Earth", { "text": "Two years per trip", "level": 1 }]] } ] }
            ```
            """;

        Assert.That(SlideDeckSpec.TryParse(ANSWER, out var spec, out var issue), Is.True, issue);
        Assert.Multiple(() =>
        {
            Assert.That(spec.Title, Is.EqualTo("Mars"));
            Assert.That(spec.Slides, Has.Count.EqualTo(1));
            Assert.That(spec.Slides[0].Layout, Is.EqualTo("L3"));
            Assert.That(spec.Slides[0].Title, Is.EqualTo("Why Mars"));
            Assert.That(spec.Slides[0].Texts[0].Paragraphs, Is.EqualTo(new[] { new SlideParagraph("Close to Earth", 0), new SlideParagraph("Two years per trip", 1) }));
        });
    }

    [Test]
    public void WhatModelsWriteInSeveralWaysIsAccepted()
    {
        const string ANSWER = """
            {
              "title": "Mars",
              "slides": [
                {
                  "layout": "L6",
                  "starts_chapter": true,
                  "title": "Costs",
                  "subtitle": null,
                  "texts": ["- First point\n- Second point"],
                  "labels": [null, "Source: NASA"],
                  "pictures": ["2"],
                  "table": { "headers": ["Year", "Missions"], "rows": [[2024, 3], ["2025", null]] },
                },
              ],
            }
            """;

        Assert.That(SlideDeckSpec.TryParse(ANSWER, out var spec, out var issue), Is.True, issue);
        var slide = spec.Slides[0];
        Assert.Multiple(() =>
        {
            Assert.That(slide.StartsChapter, Is.True);
            Assert.That(slide.Subtitle, Is.Empty, "A null is as good as a missing value.");
            Assert.That(slide.Texts[0].Paragraphs, Is.EqualTo(new[] { new SlideParagraph("First point", 0), new SlideParagraph("Second point", 0) }), "One string with line breaks is one paragraph per line, and the template draws the bullets.");
            Assert.That(slide.Labels, Is.EqualTo(new[] { string.Empty, "Source: NASA" }), "An empty label keeps its place, so that the second one still lands in the second slot.");
            Assert.That(slide.Pictures, Is.EqualTo(new[] { 2 }));
            Assert.That(slide.Table?.Headers, Is.EqualTo(new[] { "Year", "Missions" }));
            Assert.That(slide.Table?.Rows, Is.EqualTo(new[] { new[] { "2024", "3" }, new[] { "2025", string.Empty } }), "Numbers in a table are text on a slide.");
        });
    }

    [Test]
    public void AnAnswerWithoutJsonIsReported()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SlideDeckSpec.TryParse("I cannot help with that.", out _, out var issue), Is.False);
            Assert.That(issue, Does.Contain("no complete JSON object"));
        });
    }

    [Test]
    public void BrokenJsonIsReported()
    {
        const string ANSWER = """{ "title": "Mars", "slides": [ { "layout": "L1",, "title": "Mars" } ] }""";

        Assert.Multiple(() =>
        {
            Assert.That(SlideDeckSpec.TryParse(ANSWER, out _, out var issue), Is.False);
            Assert.That(issue, Does.StartWith("The JSON object cannot be read"), "The reader's own message follows, which tells the model where the JSON breaks.");
        });
    }

    [Test]
    public void TheImprintTheModelTranslatedIsRead()
    {
        const string ANSWER = """
            {
              "title": "Mars",
              "slides": [],
              "imprint": {
                "title": "Imprint",
                "topic_label": "Topic:",
                "date_label": "Date:",
                "date_placeholder": "2023-01-01 (YYYY-MM-DD)",
                "author_label": "Author:",
                "author_placeholder": "First name Last name",
                "institute_label": "Institute:",
                "picture_credits_label": "Picture credits:",
                "picture_credits_placeholder": "All pictures “Creator (license)”,\nunless stated otherwise"
              }
            }
            """;

        Assert.That(SlideDeckSpec.TryParse(ANSWER, out var spec, out var issue), Is.True, issue);
        Assert.That(spec.Imprint, Is.EqualTo(new ImprintTexts("Imprint", "Topic:", "Date:", "2023-01-01 (YYYY-MM-DD)", "Author:", "First name Last name", "Institute:", "Picture credits:", "All pictures “Creator (license)”,\nunless stated otherwise")));
    }

    [Test]
    public void TheGermanImprintOfThePromptReadsBackUnchanged()
    {
        // The field names the model sees are the ones the reader expects:
        var answer = $$"""{ "title": "Mars", "slides": [], "imprint": {{ImprintTexts.GERMAN.ToPromptJson()}} }""";

        Assert.That(SlideDeckSpec.TryParse(answer, out var spec, out var issue), Is.True, issue);
        Assert.That(spec.Imprint, Is.EqualTo(ImprintTexts.GERMAN));
    }

    [Test]
    public void AnIncompleteImprintLeavesTheGermanOne()
    {
        const string INCOMPLETE = """{ "title": "Mars", "slides": [], "imprint": { "title": "Imprint", "topic_label": "Topic:", "date_label": "" } }""";
        const string MISSING = """{ "title": "Mars", "slides": [] }""";

        Assert.Multiple(() =>
        {
            Assert.That(SlideDeckSpec.TryParse(INCOMPLETE, out var incomplete, out _), Is.True);
            Assert.That(incomplete.Imprint, Is.Null, "Half translated, the imprint would mix two languages.");
            Assert.That(SlideDeckSpec.TryParse(MISSING, out var missing, out _), Is.True, "A missing imprint is no reason to ask the model again.");
            Assert.That(missing.Imprint, Is.Null);
        });
    }

    [Test]
    public void TheOutlineFollowsTheReadingOrderOfEachLayout()
    {
        var deck = new SlideDeckSpec("Mars",
        [
            new SlideSpec("L2", true, "Getting there", "Part one", [], [], [], null),
            new SlideSpec("L5", false, "The rocket", string.Empty, [new SlideTextBlock([new SlideParagraph("Big", 0), new SlideParagraph("Very big", 1)])], [], [1], null),
            new SlideSpec("L6", false, "Costs", string.Empty, [], [], [], new SlideTableSpec(["Item", "Cost"], [["Fuel", "10"]])),
        ]);

        const string EXPECTED = """
            # Mars

            ***

            ## Getting there

            _Part one_

            ***

            ### The rocket

            - Big
              - Very big

            *Picture: rocket.png*

            ***

            ### Costs

            | Item | Cost |
            | --- | --- |
            | Fuel | 10 |
            """;

        // The literal takes the line breaks of this file, which depend on the checkout:
        Assert.That(deck.ToMarkdownOutline(PresentationTemplate.CreateDefault(), ["rocket.png"]), Is.EqualTo(EXPECTED.ReplaceLineEndings("\n")));
    }
}