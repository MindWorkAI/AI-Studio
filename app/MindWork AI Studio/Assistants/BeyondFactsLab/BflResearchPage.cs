namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// A web page a research agent read.
/// </summary>
public sealed class BflResearchPage
{
    /// <summary>
    /// Gets or sets the title of the page, or its address when it has none.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the address the agent asked for.
    /// </summary>
    public string Url { get; set; } = string.Empty;
}