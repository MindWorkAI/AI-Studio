using AIStudio.Assistants.SWOTAnalysis;
using AIStudio.Chat;

namespace AIStudio.Tests.Assistants.SWOTAnalysis;

[TestFixture]
public sealed class SwotContextualKnowledgeTests
{
    private const string DOCUMENTS = "The following files are attached to this message: annual-report.pdf";

    [Test]
    public void WithoutMaterialTheSectionSaysSo() =>
        Assert.That(SwotContextualKnowledge.NONE.Describe(), Is.EqualTo("No additional contextual knowledge was provided."));

    [Test]
    public void DocumentsAloneAreTheSection() =>
        Assert.That(new SwotContextualKnowledge(DOCUMENTS, []).Describe(), Is.EqualTo(DOCUMENTS));

    [Test]
    public void ImagesAloneAreNamedByTheirCount()
    {
        var oneImage = new SwotContextualKnowledge(string.Empty, [Image("chart.png")]);
        var twoImages = new SwotContextualKnowledge(string.Empty, [Image("chart.png"), Image("photo.jpg")]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(oneImage.Describe(), Is.EqualTo("The image attached to this message belongs to the contextual knowledge."));
            Assert.That(twoImages.Describe(), Is.EqualTo("The 2 images attached to this message belong to the contextual knowledge."));
        }
    }

    [Test]
    public void ImagesFollowTheDocuments()
    {
        var description = new SwotContextualKnowledge(DOCUMENTS, [Image("chart.png")]).Describe();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(description, Does.StartWith(DOCUMENTS));
            Assert.That(description, Does.EndWith("The image attached to this message belongs to the contextual knowledge."));
        }
    }

    private static FileAttachmentImage Image(string fileName) => new(fileName, $"/tmp/{fileName}", 1024);
}