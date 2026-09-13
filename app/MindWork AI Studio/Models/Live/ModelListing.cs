namespace AIStudio.Models.Live;

/// <summary>
/// What a provider's own model list says about one of the models it serves.
/// </summary>
/// <remarks>
/// That list is fetched anyway: before every chat round, before every assistant run, and whenever
/// somebody opens the provider dialog. Reading what it already carries therefore costs no request
/// of its own, which is the whole reason these numbers are taken from here and not asked for.
///
/// This describes one installation, never the model as such. Two machines may serve the same
/// weights behind different settings, and a statement about one of them says nothing about the
/// other -- which is why a listing is kept per configured provider instance and is gone with the
/// process. It is also the only source for a self-hosted model: a rule can say what the weights
/// were trained for, but only the engine knows what its operator started it with.
/// </remarks>
/// <param name="ModelId">The model, named the way the provider names it in its list.</param>
/// <param name="Context">The window the provider states for it, or unknown where it states none.</param>
public readonly record struct ModelListing(string ModelId, ContextWindow Context)
{
    /// <summary>
    /// What we have about a model nobody has reported anything about.
    /// </summary>
    public static readonly ModelListing NOTHING = new(string.Empty, ContextWindow.UNKNOWN);

    /// <summary>
    /// Whether this listing states anything at all.
    /// </summary>
    public bool IsKnown => this.Context.IsKnown;

    /// <summary>
    /// Puts what the provider stated over what the rules worked out.
    /// </summary>
    /// <remarks>
    /// A stated window replaces the whole window, the ceiling included, for the same reason the
    /// expert settings do: what a model card says it could be raised to is a statement about the
    /// model, while this is a statement about the installation serving it. Whoever started that
    /// engine has already decided, and a ceiling nobody can reach without restarting it is not a
    /// number to keep showing.
    /// </remarks>
    /// <param name="profile">What is known about the model without this listing.</param>
    /// <returns>The profile, with what the provider stated in it.</returns>
    public ModelProfile ApplyTo(in ModelProfile profile) => this.IsKnown ? profile with { Context = this.Context } : profile;
}