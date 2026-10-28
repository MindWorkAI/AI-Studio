using System.Text.Json;

using AIStudio.Chat;

namespace AIStudio.Tests.Chat;

[TestFixture]
public sealed class ContentPresentationTests
{
    [Test]
    public void TheDataHashCodeFollowsNewData()
    {
        var presentation = ContentPresentation.Create(ContentPresentationKind.SWOT_ANALYSIS, new { heading = "First" });
        var firstHashCode = presentation.GetDataHashCode();

        presentation.Data = JsonSerializer.SerializeToElement(new { heading = "Second" });

        Assert.Multiple(() =>
        {
            Assert.That(firstHashCode, Is.Not.Zero);
            Assert.That(presentation.GetDataHashCode(), Is.Not.EqualTo(firstHashCode));
        });
    }

    [Test]
    public void APresentationWithoutDataHasTheHashCodeZero()
    {
        var presentation = new ContentPresentation { Kind = ContentPresentationKind.SWOT_ANALYSIS };

        Assert.That(presentation.GetDataHashCode(), Is.Zero);
    }

    [Test]
    public void ACloneHasTheSameDataHashCode()
    {
        var presentation = ContentPresentation.Create(ContentPresentationKind.SWOT_ANALYSIS, new { heading = "First" });

        Assert.That(presentation.DeepClone().GetDataHashCode(), Is.EqualTo(presentation.GetDataHashCode()));
    }
}