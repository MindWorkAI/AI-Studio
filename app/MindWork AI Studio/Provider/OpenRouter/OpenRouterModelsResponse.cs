namespace AIStudio.Provider.OpenRouter;

/// <summary>
/// A data model for the response from the OpenRouter models endpoint.
/// </summary>
/// <param name="Data">The list of models.</param>
public readonly record struct OpenRouterModelsResponse(IList<OpenRouterModel> Data);
