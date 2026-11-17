namespace AIStudio.Tools;

/// <summary>
/// Data model for a source used in the response.
/// </summary>
/// <param name="Title">The title of the source.</param>
/// <param name="URL">The URL of the source.</param>
public record Source(string Title, string URL, SourceOrigin Origin) : ISource
{
    //
    // The properties of the parameters, declared again to be settable. A derived record which passes a value of
    // its own to one of them, as the search results of Perplexity do with the origin, would otherwise lose it:
    // the source generation of System.Text.Json sets every init-only property while it creates the object, also
    // one missing in the JSON. Keep them in the order of the parameters, which is the order they get stored in:
    //
    public string Title { get; set; } = Title;
    public string URL { get; set; } = URL;
    public SourceOrigin Origin { get; set; } = Origin;
}