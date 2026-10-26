namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// What the presentation does with one slide of the template, as the user chose it.
/// </summary>
public enum TemplateSlideUse
{
    /// <summary>
    /// The presentation leaves the slide out, as it does with the samples and hints of a template.
    /// </summary>
    OMIT = 0,

    /// <summary>
    /// The presentation keeps the slide as it is, only in its color variant and with its footer.
    /// </summary>
    KEEP,

    /// <summary>
    /// The model writes new content into the slots of the slide, which keeps its design and every slot the model leaves empty.
    /// </summary>
    EDIT,
}