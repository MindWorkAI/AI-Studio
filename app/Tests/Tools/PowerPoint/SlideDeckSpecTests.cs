using AIStudio.Tools.PowerPoint;

using P = DocumentFormat.OpenXml.Presentation;

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

    [Test]
    public void TheTemplateSlideAnEntryRefersToIsRead()
    {
        const string ANSWER = """
            { "title": "Weekly", "slides": [
              { "template_slide": 1 },
              { "template_slide": "2", "title": "News", "texts": [["A new release"]] },
              { "layout": "L3", "title": "Example" }
            ] }
            """;

        Assert.That(SlideDeckSpec.TryParse(ANSWER, out var spec, out var issue), Is.True, issue);
        Assert.Multiple(() =>
        {
            Assert.That(spec.Slides.Select(slide => slide.TemplateSlide), Is.EqualTo(new int?[] { 1, 2, null }));
            Assert.That(spec.Slides.Select(slide => slide.HasContent), Is.EqualTo(new[] { false, true, true }), "Only content of its own makes a slide more than a reference.");
            Assert.That(spec.Slides[0].Layout, Is.Empty);
        });
    }

    [Test]
    public void WhatAnEditedSlideRemovesIsRead()
    {
        const string ANSWER = """
            { "title": "Weekly", "slides": [
              { "template_slide": 1, "remove": ["element 2", " label 1 ", 3, "Element 2", ""] },
              { "template_slide": 2, "title": "News", "remove": "element 1" },
              { "template_slide": 3 }
            ] }
            """;

        Assert.That(SlideDeckSpec.TryParse(ANSWER, out var spec, out var issue), Is.True, issue);
        Assert.Multiple(() =>
        {
            Assert.That(spec.Slides[0].Removals, Is.EqualTo(new[] { "element 2", "label 1", "3" }), "Every name counts once, whatever its case.");
            Assert.That(spec.Slides[1].Removals, Is.EqualTo(new[] { "element 1" }), "A single name is as good as a list.");
            Assert.That(spec.Slides[2].Removals, Is.Empty);
            Assert.That(spec.Slides.Select(slide => slide.HasContent), Is.EqualTo(new[] { true, true, false }), "Removing something changes a template slide as well.");
        });
    }

    [Test]
    public void TheOutlineShowsWhatTheTemplateSlidesKeep()
    {
        using var file = TestTemplate.Create(
            new TestTemplateOptions(Slides:
            [
                new TestSlide(0, (TestPlaceholder.Title(), "Consultation hour"), (new TestPlaceholder(P.PlaceholderValues.SubTitle, 1), "Every Monday")),
                new TestSlide(1, (TestPlaceholder.Title(), "News"), (new TestPlaceholder(null, 1), "AI Studio:\nEINSTEIN:")),
                new TestSlide(1, (TestPlaceholder.Title(), "Questions"), (new TestPlaceholder(null, 1), "Ask us")),
            ]),
            new TestLayout("Title", TestPlaceholder.Title(), TestPlaceholder.Subtitle(new SlotBounds(838_200, 3_602_038, 10_515_600, 1_655_762))),
            new TestLayout("Content", TestPlaceholder.Title(), TestPlaceholder.Text(1, TestTemplate.MASTER_BODY_BOUNDS)));

        var deck = new SlideDeckSpec("Weekly",
        [
            new SlideSpec(string.Empty, false, string.Empty, string.Empty, [], [], [], null, 1),
            new SlideSpec(string.Empty, false, string.Empty, string.Empty, [new SlideTextBlock([new SlideParagraph("A new release", 0)])], [], [], null, 2),
            new SlideSpec(string.Empty, false, "Your questions", string.Empty, [], [], [], null, 3),
        ]);

        const string EXPECTED = """
            # Weekly

            ***

            ### Consultation hour

            *Slide 1 of the template, unchanged*

            _Every Monday_

            ***

            ### News

            *Slide 2 of the template, edited*

            - A new release

            ***

            ### Your questions

            *Slide 3 of the template, edited*

            - Ask us
            """;

        Assert.That(deck.ToMarkdownOutline(PresentationTemplate.Read(file), []), Is.EqualTo(EXPECTED.ReplaceLineEndings("\n")));
    }

    [Test]
    public void TheOutlineLeavesOutWhatAnEditedSlideRemoves()
    {
        using var file = TestTemplate.Create(
            new TestTemplateOptions(Slides: [new TestSlide(0, (TestPlaceholder.Title(), "Questions"), (new TestPlaceholder(null, 1), "Lorem ipsum"))]),
            new TestLayout("Content", TestPlaceholder.Title(), TestPlaceholder.Text(1, TestTemplate.MASTER_BODY_BOUNDS)));

        var deck = new SlideDeckSpec("Weekly", [new SlideSpec(string.Empty, false, "Your questions", string.Empty, [], [], [], null, 1) { Removals = ["text 1"] }]);

        const string EXPECTED = """
            # Weekly

            ***

            ### Your questions

            *Slide 1 of the template, edited*
            """;

        Assert.That(deck.ToMarkdownOutline(PresentationTemplate.Read(file), []), Is.EqualTo(EXPECTED.ReplaceLineEndings("\n")));
    }
}