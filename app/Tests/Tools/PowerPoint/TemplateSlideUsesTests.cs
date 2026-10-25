using AIStudio.Tools.PowerPoint;

namespace AIStudio.Tests.Tools.PowerPoint;

/// <summary>
/// Checks how the choices for some slides of a template, e.g., the preselection of an organization, become a use for each slide.
/// </summary>
[TestFixture]
public sealed class TemplateSlideUsesTests
{
    [Test]
    public void EverySlideGetsAUseInTheOrderOfTheTemplate()
    {
        var template = TemplateWithAContentSlideAndAClosingSlide();
        var uses = new Dictionary<int, TemplateSlideUse> { [2] = TemplateSlideUse.KEEP }.ToTemplateSlideUses(template);

        Assert.That(uses, Is.EqualTo(new[] { TemplateSlideUse.OMIT, TemplateSlideUse.KEEP }), "A slide without a choice is left out, as by default.");
    }

    [Test]
    public void ASlideWithoutRoomForContentIsKeptInsteadOfEdited()
    {
        var template = TemplateWithAContentSlideAndAClosingSlide();
        var uses = new Dictionary<int, TemplateSlideUse> { [1] = TemplateSlideUse.EDIT, [2] = TemplateSlideUse.EDIT }.ToTemplateSlideUses(template);

        Assert.Multiple(() =>
        {
            Assert.That(template.Slides.Select(slide => slide.IsEditable), Is.EqualTo(new[] { true, false }), "The test needs one slide which the model can edit and one which it cannot.");
            Assert.That(uses, Is.EqualTo(new[] { TemplateSlideUse.EDIT, TemplateSlideUse.KEEP }), "An organization may lock the choice, so it must not block the form.");
        });
    }

    [Test]
    public void AChoiceForASlideTheTemplateLacksIsPassedOver()
    {
        var template = TemplateWithAContentSlideAndAClosingSlide();
        var uses = new Dictionary<int, TemplateSlideUse> { [0] = TemplateSlideUse.KEEP, [3] = TemplateSlideUse.EDIT }.ToTemplateSlideUses(template);

        Assert.That(uses, Is.EqualTo(new[] { TemplateSlideUse.OMIT, TemplateSlideUse.OMIT }));
    }

    /// <summary>
    /// A template whose first slide uses a layout with room for content, and whose second slide uses one with a title only.
    /// </summary>
    private static PresentationTemplate TemplateWithAContentSlideAndAClosingSlide()
    {
        using var file = TestTemplate.Create(
            new TestTemplateOptions(Slides: [new TestSlide(0, (TestPlaceholder.Title(), "Mission")), new TestSlide(1, (TestPlaceholder.Title(), "Thank you"))]),
            new TestLayout("Content", TestPlaceholder.Title(), TestPlaceholder.Text(1, TestTemplate.MASTER_BODY_BOUNDS)),
            new TestLayout("Title Only", TestPlaceholder.Title()));

        return PresentationTemplate.Read(file);
    }
}