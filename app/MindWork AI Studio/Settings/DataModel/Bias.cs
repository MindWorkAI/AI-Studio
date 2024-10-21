namespace AIStudio.Settings.DataModel;

public sealed class Bias
{
    /// <summary>
    /// The unique identifier of the bias.
    /// </summary>
    public Guid Id { get; init; } = Guid.Empty;

    /// <summary>
    /// In which category the bias is located.
    /// </summary>
    public BiasCategory Category { get; set; } = BiasCategory.NONE;

    /// <summary>
    /// The bias name.
    /// </summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>
    /// The bias description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Related bias.
    /// </summary>
    public IReadOnlyList<Guid> Related { get; init; } = [];

    /// <summary>
    /// Related links.
    /// </summary>
    public IReadOnlyList<string> Links { get; init; } = [];
}