namespace AIStudio.Provider.Google;

/// <summary>
/// A data model for the response from the model endpoint.
/// </summary>
/// <param name="Models"></param>
public readonly record struct ModelsResponse(IList<Model> Models);