using AIStudio.Chat;

namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// The contextual knowledge of an analysis, prepared once for all of its requests.
/// </summary>
/// <remarks>
/// The research and the four categories all need the same material. Read once, each document passes
/// the prompt injection filter once, and a missing file or a broken image is reported once instead of
/// five times. The text of the documents goes into the requests; only the images stay attachments.
/// </remarks>
/// <param name="Documents">The text of the documents, as the AI gets it; empty without documents.</param>
/// <param name="Images">The images which the model can read and which could be prepared for it.</param>
internal sealed record SwotContextualKnowledge(string Documents, IReadOnlyList<FileAttachment> Images)
{
    public static readonly SwotContextualKnowledge NONE = new(string.Empty, []);

    /// <summary>
    /// What the contextual-knowledge section of a request holds.
    /// </summary>
    public string Describe()
    {
        var images = this.Images.Count switch
        {
            0 => string.Empty,
            1 => "The image attached to this message belongs to the contextual knowledge.",

            _ => $"The {this.Images.Count} images attached to this message belong to the contextual knowledge.",
        };

        if (this.Documents.Length == 0)
            return images.Length == 0 ? "No additional contextual knowledge was provided." : images;

        return images.Length == 0 ? this.Documents : $"{this.Documents}\n\n{images}";
    }
}