namespace AIStudio.Tools;

/// <summary>
/// A contract for data classes with a confidence value.
/// </summary>
/// <remarks>
/// Using this confidence contract allows us to provide
/// algorithms based on confidence values.
/// </remarks>
public interface IConfidence
{
    /// <summary>
    /// How confident is the AI in this task or decision?
    /// </summary>
    public float Confidence { get; init; }
}