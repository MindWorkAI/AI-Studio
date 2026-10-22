namespace AIStudio.Provider.Groq;

/// <summary>
/// A data model for the response from the Groq models endpoint.
/// </summary>
/// <param name="Data">The models Groq serves.</param>
public readonly record struct GroqModelsResponse(IList<GroqModel> Data);