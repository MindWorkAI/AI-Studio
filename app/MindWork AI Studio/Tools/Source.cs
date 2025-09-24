namespace AIStudio.Tools;

/// <summary>
/// Data model for a source used in the response.
/// </summary>
/// <param name="Title">The title of the source.</param>
/// <param name="URL">The URL of the source.</param>
public record Source(string Title, string URL) : ISource;