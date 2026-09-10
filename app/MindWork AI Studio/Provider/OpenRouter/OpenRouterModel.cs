namespace AIStudio.Provider.OpenRouter;

/// <summary>
/// A data model for an OpenRouter model from the API.
/// </summary>
/// <param name="Id">The model's ID.</param>
/// <param name="Name">The model's human-readable display name.</param>
/// <param name="Reasoning">The model's provider-reported reasoning behavior.</param>
public readonly record struct OpenRouterModel(string Id, string? Name, OpenRouterReasoning? Reasoning)
{
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

/// <summary>
/// The reasoning defaults returned for a model by OpenRouter's model catalog.
/// </summary>
/// <param name="DefaultEnabled">Whether reasoning is enabled when the request does not configure it.</param>
/// <param name="Mandatory">Whether reasoning cannot be disabled for the model.</param>
public readonly record struct OpenRouterReasoning(bool DefaultEnabled, bool Mandatory);
