namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Data structure representing annotation deltas in chat completions.
/// </summary>
/// <param name="Annotations">The list of annotations, which can be null.</param>
public record ChatCompletionAnnotationDelta(IList<Annotation>? Annotations);