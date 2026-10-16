namespace AIStudio.Tools;

/// <summary>
/// An image of a slide, ready to be appended to the slide's Markdown.
/// </summary>
/// <param name="markdownImage">The image as a Markdown image with an embedded data URI.</param>
public sealed class SlideImageContent(string markdownImage) : ISlideContent
{
    public string MarkdownImage => markdownImage;
}