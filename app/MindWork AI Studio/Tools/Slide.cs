namespace AIStudio.Tools;

public sealed class Slide
{
    public bool Delivered { get; set; }

    public int Position { get; init; }

    public List<ISlideContent> Content { get; } = new();

    /// <summary>
    /// The number of tokens of everything this slide holds, or null when it is unknown.
    /// </summary>
    /// <remarks>
    /// A slide grows across several stream events, so its count grows with it. It becomes unknown
    /// as soon as an image is embedded: the runtime counted the text of the slide, and a data URI
    /// is orders of magnitude larger than that.
    /// </remarks>
    public int? TokenCount { get; set; }
}