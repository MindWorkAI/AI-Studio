namespace AIStudio.Provider;

/// <summary>
/// A contract for a line in a response stream that can provide annotations such as sources.
/// </summary>
public interface IAnnotationStreamLine
{
    /// <summary>
    /// Checks if the response line contains any sources.
    /// </summary>
    /// <returns>True when the response line contains sources, false otherwise.</returns>
    public bool ContainsSources();

    /// <summary>
    /// Gets the sources of the response line.
    /// </summary>
    /// <returns>The sources of the response line.</returns>
    public IList<ISource> GetSources();
}