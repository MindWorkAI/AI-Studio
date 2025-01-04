namespace AIStudio.Provider;

public interface IResponseStreamLine
{
    /// <summary>
    /// Checks if the response line contains any content.
    /// </summary>
    /// <returns>True when the response line contains content, false otherwise.</returns>
    public bool ContainsContent();
    
    /// <summary>
    /// Gets the content of the response line.
    /// </summary>
    /// <returns>The content of the response line.</returns>
    public string GetContent();
}