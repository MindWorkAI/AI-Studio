namespace AIStudio.Assistants.Dynamic;

public sealed class WebContentState
{
    public string Content { get; set; } = string.Empty;
    public string URL { get; set; } = string.Empty;
    public bool Preselect { get; set; }
    public bool PreselectContentCleaner { get; set; }
    public bool CleanerIsRunning { get; set; }

    /// <summary>
    /// Creates a copy which shares nothing with this state.
    /// </summary>
    /// <returns>The copy.</returns>
    public WebContentState Copy() => new()
    {
        Content = this.Content,
        URL = this.URL,
        Preselect = this.Preselect,
        PreselectContentCleaner = this.PreselectContentCleaner,
        CleanerIsRunning = this.CleanerIsRunning,
    };

    /// <summary>
    /// Tells whether another state holds the same inputs as this one.
    /// </summary>
    /// <remarks>
    /// Whether the content cleaner runs is not an input: it is work in progress, which ends with the component that started it.
    /// </remarks>
    /// <param name="other">The state to compare with.</param>
    /// <returns><c>true</c> when both states hold the same inputs; otherwise, <c>false</c>.</returns>
    public bool HasSameInputsAs(WebContentState other) => string.Equals(this.Content, other.Content, StringComparison.Ordinal)
                                                          && string.Equals(this.URL, other.URL, StringComparison.Ordinal)
                                                          && this.Preselect == other.Preselect
                                                          && this.PreselectContentCleaner == other.PreselectContentCleaner;
}