namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Base class for different types of annotations.
/// </summary>
/// <remarks>
/// We use this base class to represent various annotation types for all types of LLM providers.
/// </remarks>
/// <param name="Type">The type of the annotation.</param>
public abstract record Annotation(string Type);