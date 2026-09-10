namespace AIStudio.Provider.OpenRouter;

/// <summary>
/// A data model for an OpenRouter model from the API.
/// </summary>
/// <param name="Id">The model's ID.</param>
/// <param name="Name">The model's human-readable display name.</param>
/// <param name="Reasoning">The model's provider-reported reasoning behavior.</param>
public readonly record struct OpenRouterModel(string Id, string? Name, OpenRouterReasoning? Reasoning)
{
    /// <summary>
    /// Converts the catalog entry into the model the rest of the app works with.
    /// </summary>
    public Model ToModel() => new(this.Id, this.Name)
    {
        ReasoningBehavior = this.Reasoning switch
        {
            { Mandatory: true } => ModelReasoningBehavior.ALWAYS_ON,
            { DefaultEnabled: true } => ModelReasoningBehavior.DEFAULT_ON,
            not null => ModelReasoningBehavior.OPTIONAL,
            _ => ModelReasoningBehavior.UNKNOWN,
        },
    };
}
