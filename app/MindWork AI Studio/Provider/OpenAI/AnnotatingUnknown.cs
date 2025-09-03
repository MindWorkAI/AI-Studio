namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Represents an unknown annotation type.
/// </summary>
/// <param name="Type">The type of the unknown annotation.</param>
public sealed record AnnotatingUnknown(string Type) : Annotation(Type);