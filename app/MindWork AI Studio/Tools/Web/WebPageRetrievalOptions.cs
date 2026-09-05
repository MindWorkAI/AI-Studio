using AIStudio.Provider;

namespace AIStudio.Tools.Web;

public sealed class WebPageRetrievalOptions
{
    public required int TimeoutSeconds { get; init; }

    /// <summary>
    /// Whether the user named this exact URL, as opposed to a model asking for it.
    /// </summary>
    /// <remarks>
    /// Lifts the restrictions on which targets may be reached — private networks, loopback, and
    /// hosts named localhost — because those exist to keep a model from reaching into the user's
    /// network, and the user is not a model. The network-level protections stay: the connection
    /// is still bound to validated addresses, redirects are still checked, the response size is
    /// still capped, and only HTML is still accepted.<br/><br/>
    /// Never set this for a URL that reached AI Studio through a model, however plausible it
    /// looks.
    /// </remarks>
    public bool TargetChosenByUser { get; init; }

    public bool PublicTargetsOnly { get; init; }

    public ConfidenceLevel ProviderConfidence { get; init; } = ConfidenceLevel.NONE;

    public bool ProviderIsTrustedByConfiguration { get; init; }

    public bool UseOsSso { get; init; }

    public Func<string, bool>? IsPrivateHostAllowed { get; init; }

    public Func<Uri, ConfidenceLevel, Task>? OnPrivateHostProviderBlockAsync { get; init; }
}