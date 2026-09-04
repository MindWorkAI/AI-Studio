namespace AIStudio.Provider.HuggingFace;

/// <summary>
/// One inference provider serving a model.
/// </summary>
/// <param name="Provider">The slug of the inference provider, e.g. "novita".</param>
/// <param name="Status">Whether the provider currently serves the model. Known value: "live".</param>
public readonly record struct HFModelProvider(string Provider, string Status);