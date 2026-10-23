using AIStudio.Tools.PowerPoint;

namespace AIStudio.Tests.Tools.PowerPoint;

/// <summary>
/// Checks the kinds of slides the user may choose layouts for.
/// </summary>
[TestFixture]
public sealed class LayoutPurposeTests
{
    [Test]
    public void EveryKindOfSlideIsDescribedForTheUserAndTheModel()
    {
        Assert.Multiple(() =>
        {
            foreach (var purpose in Enum.GetValues<LayoutPurpose>())
            {
                Assert.That(purpose.Label(), Is.Not.Empty, $"The kind of slide {purpose} has no label.");
                Assert.That(purpose.Question(), Does.EndWith("?"), $"The kind of slide {purpose} has no question.");
                Assert.That(purpose.PromptDescription(), Is.Not.Empty.And.Not.EndWith("."), $"The kind of slide {purpose} has no description, or one which ends where the rule for the model goes on.");
            }
        });
    }

    /// <summary>
    /// Checks that no two kinds of slides read the same, since the user tells them apart by their labels only.
    /// </summary>
    [Test]
    public void EveryKindOfSlideHasALabelOfItsOwn()
    {
        var labels = Enum.GetValues<LayoutPurpose>().Select(purpose => purpose.Label()).ToList();
        Assert.That(labels, Is.Unique);
    }

    /// <summary>
    /// Checks that the kinds of slides are grouped by area, in the order of the areas.
    /// </summary>
    /// <remarks>
    /// The forms list the kinds of each area together, in the order of the enum. A kind added at the
    /// end of the enum rather than within its area would break up the areas.
    /// </remarks>
    [Test]
    public void TheKindsOfSlidesFollowTheOrderOfTheAreas()
    {
        var areas = Enum.GetValues<LayoutPurpose>().Select(purpose => purpose.Area()).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(areas, Is.Ordered, "The kinds of an area stand together, and the areas follow their own order.");
            Assert.That(areas.Distinct(), Is.EquivalentTo(Enum.GetValues<LayoutPurposeArea>()), "Every area has kinds of slides.");
        });
    }
}