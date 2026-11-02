using AIStudio.Tools.PowerPoint;

namespace AIStudio.Tests.Tools.PowerPoint;

/// <summary>
/// Checks how the layouts the user chose by name are found in a template, and what the model is told about them.
/// </summary>
/// <remarks>
/// Uses the default catalog unless a test builds its own template: L1 "Title Slide", L2 "Section
/// Header", L3 "Title and Content", L4 "Two Content", L5 "Content with Picture", and L6 "Table".
/// </remarks>
[TestFixture]
public sealed class ChosenLayoutsTests
{
    private static readonly PresentationTemplate TEMPLATE = PresentationTemplate.CreateDefault();
    private static readonly SlotBounds LEFT_TEXT = new(838_200, 1_825_625, 5_150_000, 4_351_338);

    [Test]
    public void ALayoutIsFoundByItsExactName()
    {
        var chosen = ChosenLayouts.Of(TEMPLATE, new Dictionary<LayoutPurpose, string>
        {
            [LayoutPurpose.CHAPTER] = "Section Header",
            [LayoutPurpose.CONTENT] = "title and content",
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(chosen.For(LayoutPurpose.CHAPTER)?.Id, Is.EqualTo("L2"));
            Assert.That(chosen.For(LayoutPurpose.CONTENT), Is.Null, "Names are compared exactly, as the template's author wrote them.");
            Assert.That(chosen.For(LayoutPurpose.TITLE), Is.Null, "A kind of slide without a choice is left to the model.");
        }
    }

    [Test]
    public void ANameTheTemplateDoesNotHaveCountsAsNoChoice()
    {
        var chosen = ChosenLayouts.Of(TEMPLATE, new Dictionary<LayoutPurpose, string>
        {
            [LayoutPurpose.CHAPTER] = "Kapiteltrenner",
            [LayoutPurpose.TITLE] = string.Empty,
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(chosen, Is.SameAs(ChosenLayouts.NONE));
            Assert.That(chosen.ToPromptRules(), Is.Empty, "Without choices, the system prompt gets no rules at all.");
        }
    }

    [Test]
    public void TheFirstOfTwoLayoutsWithTheSameNameCounts()
    {
        var template = TestTemplate.Read(
            new TestLayout("Content", TestPlaceholder.Title(), TestPlaceholder.Text(1, TestTemplate.MASTER_BODY_BOUNDS)),
            new TestLayout("Content", TestPlaceholder.Title(), TestPlaceholder.Text(1, LEFT_TEXT)));

        var chosen = ChosenLayouts.Of(template, new Dictionary<LayoutPurpose, string> { [LayoutPurpose.CONTENT] = "Content" });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(template.Families.Select(family => family.Name), Is.EqualTo(new[] { "Content", "Content" }), "The test needs two families with the same name.");
            Assert.That(chosen.For(LayoutPurpose.CONTENT)?.Id, Is.EqualTo("L1"));
        }
    }

    [Test]
    public void TheRulesNameTheLayoutsByTheirIdsOnly()
    {
        var rules = ChosenLayouts.Of(TEMPLATE, new Dictionary<LayoutPurpose, string>
        {
            [LayoutPurpose.TIMELINE] = "Two Content",
            [LayoutPurpose.CHAPTER] = "Section Header",
        }).ToPromptRules();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rules, Does.Contain("\n        - L2 for each slide which starts a chapter;"));
            Assert.That(rules, Does.Contain("\n        - L4 for timelines, e.g., the course of a project."));
            Assert.That(rules.IndexOf("L2 for", StringComparison.Ordinal), Is.LessThan(rules.IndexOf("L4 for", StringComparison.Ordinal)), "The rules follow the course of a presentation.");
            Assert.That(rules, Does.Not.Contain("Section Header").And.Not.Contain("Two Content"), "No text of the template file reaches the system prompt.");
        }
    }

    [Test]
    public void AChoiceForAFramingSlideRequiresSuchASlide()
    {
        var chosen = ChosenLayouts.Of(TEMPLATE, new Dictionary<LayoutPurpose, string>
        {
            [LayoutPurpose.TIMELINE] = "Two Content",
            [LayoutPurpose.IMPRINT] = "Title and Content",
        });

        var rules = chosen.ToPromptRules();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(chosen.AddedSlides, Is.EqualTo(new[] { LayoutPurpose.IMPRINT }), "A timeline comes only where the content has one.");
            Assert.That(rules, Does.Contain("\n    - The presentation has at least one slide of each of these kinds, on its layout:\n        - L3: the imprint, e.g., with the authors, the organization, and the date, as the very last slide;"));
            Assert.That(rules, Does.Contain("leaves a slot empty rather than inventing anything."), "The model must not invent the details of an imprint.");
        }
    }
}