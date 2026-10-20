namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// A presentation as the model describes it, slide by slide.
/// </summary>
/// <remarks>
/// The model writes JSON, which is read leniently into this shape: whatever the model may write in
/// several ways, such as a number in a table cell, is accepted, and every missing value becomes
/// empty. Whether the slides fit the template is a separate question, which
/// <see cref="SlideDeckSpecValidator"/> answers.
/// </remarks>
/// <param name="Title">The title of the presentation.</param>
/// <param name="Slides">The slides, in order.</param>
internal sealed partial record SlideDeckSpec(string Title, IReadOnlyList<SlideSpec> Slides);