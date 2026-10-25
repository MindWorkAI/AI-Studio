namespace AIStudio.Assistants.SlideBuilder;

/// <summary>
/// The explanations which the Slide Assistant and its settings dialog share, so that both say the same.
/// </summary>
public static class SlideAssistantTexts
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(SlideAssistantTexts).Namespace, nameof(SlideAssistantTexts));

    /// <summary>
    /// What the LLM knows about the layouts of a template, and why their names matter.
    /// </summary>
    public static string WhatTheLLMKnowsOfLayouts => TB("The LLM does not see what your layouts look like. It knows only their names from the slide master of your template, where their placeholders sit, and how much text fits into them. Clear names such as “Chapter” or “Content with picture” therefore help it choose well; in PowerPoint, you rename a layout in the slide master view. Logo, background, and footer come with the layout. Layouts which differ only in their color count as one, and the color applies to the whole presentation.");

    /// <summary>
    /// What the choice of a layout for a kind of slide does, and what it does not.
    /// </summary>
    public static string WhatALayoutChoiceDoes => TB("Choose which layout of your template the LLM uses for certain kinds of slides. The lists offer the layouts with room for content. Where you have no preference, the LLM decides by the names and placeholders of the layouts. A choice says which layout such a slide uses, not whether the presentation has one: whether there is, e.g., an agenda, follows from your content and your important aspects. Only for the framing slides, e.g., the table of contents or the imprint, does a choice also add such a slide; their help says so. Each chapter becomes a section in PowerPoint, which the slide sorter shows as an outline.");

    /// <summary>
    /// What keeping the empty picture placeholders does.
    /// </summary>
    public static string WhatKeepingEmptyPicturePlaceholdersDoes => TB("When a new slide has no picture for a picture placeholder of its layout, it leaves the placeholder out, as every other placeholder without content. Keep these placeholders to insert pictures of your own later: PowerPoint shows them while you edit and hides them in the slide show.");

    /// <summary>
    /// What the presentation does with the slides a template carries besides its layouts.
    /// </summary>
    public static string WhatHappensToTemplateSlides => TB("Besides its layouts, your file contains slides, e.g., samples of the template or the slides of an earlier presentation. By default, the presentation leaves them out. Choose for each slide whether the presentation keeps it unchanged or lets the LLM edit its content. The LLM uses each of these slides exactly once, keeps them in their order, and may add new slides before, between, and after them. It never copies a slide as a pattern: new slides always come from the layouts of your template.");
}