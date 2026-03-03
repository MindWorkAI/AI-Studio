namespace AIStudio.Provider.OpenRouter;

/// <summary>
/// A data model for an OpenRouter model from the API.
/// </summary>
/// <param name="Id">The model's ID.</param>
/// <param name="Name">The model's human-readable display name.</param>
public readonly record struct OpenRouterModel(string Id, string? Name);
