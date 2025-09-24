namespace AIStudio.Tools;

/// <summary>
/// Data model for a source used in the response.
/// </summary>
public interface ISource
{
    /// <summary>
    /// The title of the source.
    /// </summary>
    public string Title { get; }
    
    /// <summary>
    /// The URL of the source.
    /// </summary>
    public string URL { get; }
}