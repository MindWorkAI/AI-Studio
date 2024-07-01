namespace AIStudio.Provider.OpenAI;

/// <summary>
/// A data model for the response from the model endpoint.
/// </summary>
/// <param name="Data"></param>
public readonly record struct ModelsResponse(IList<Model> Data);