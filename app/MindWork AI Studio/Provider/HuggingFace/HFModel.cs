namespace AIStudio.Provider.HuggingFace;

/// <summary>
/// One model as the Hugging Face router describes it.
/// </summary>
/// <param name="Id">The ID of the model, written as "org/model".</param>
/// <param name="Providers">The inference providers serving this model.</param>
public readonly record struct HFModel(string Id, IList<HFModelProvider>? Providers)
{
    /// <summary>
    /// The window this model has when it is reached the way this user set things up.
    /// </summary>
    /// <remarks>
    /// A window belongs to an inference provider here, not to the model: the same weights run
    /// behind several of them, each configured by somebody else. Where the user named one, its
    /// number is the answer. Where they let the router choose, the smallest window among the
    /// providers currently serving the model is -- nobody knows which one the router will take, and
    /// a number promising more than the chosen provider delivers would walk a conversation into an
    /// error the user could not see coming.
    /// </remarks>
    /// <param name="providerSlug">The inference provider the user chose, or empty when the router chooses.</param>
    /// <returns>The window in tokens, or null where nobody stated one.</returns>
    public int? ContextWindowTokens(string providerSlug)
    {
        if (this.Providers is null)
            return null;

        var serving = this.Providers.Where(provider => provider.IsLive);
        if (!string.IsNullOrEmpty(providerSlug))
            serving = serving.Where(provider => string.Equals(provider.Provider, providerSlug, StringComparison.OrdinalIgnoreCase));

        return serving.Where(provider => provider.ContextWindowTokens is > 0).Min(provider => provider.ContextWindowTokens);
    }
}