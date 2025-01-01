namespace AIStudio.Assistants.ERI;

public sealed class RetrievalParameter
{
    /// <summary>
    /// The name of the parameter.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The description of the parameter.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}