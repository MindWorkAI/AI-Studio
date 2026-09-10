namespace AIStudio.Provider.OpenRouter;

/// <summary>
/// The reasoning defaults returned for a model by OpenRouter's model catalog.
/// </summary>
/// <remarks>
/// The catalog reports more than this, such as the supported efforts and the default one.
/// We read only what decides the model's reasoning capability today.
/// </remarks>
/// <param name="DefaultEnabled">Whether reasoning is enabled when the request does not configure it.</param>
/// <param name="Mandatory">Whether reasoning cannot be disabled for the model.</param>
public readonly record struct OpenRouterReasoning(bool DefaultEnabled, bool Mandatory);
