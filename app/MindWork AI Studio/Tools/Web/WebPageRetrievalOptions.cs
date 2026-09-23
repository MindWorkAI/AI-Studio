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

    public bool UseOsSso { get; init; }

    public Func<string, bool>? IsPrivateHostAllowed { get; init; }

    /// <summary>
    /// Decides for every URL, the first one as well as each redirect target, whether it may be
    /// requested at all. It runs before anything is sent, so a refused target never sees the URL.
    /// </summary>
    public Func<Uri, bool>? IsTargetAllowed { get; init; }

    /// <summary>
    /// Allows the operating system's sign-in for a host which resolves to public addresses.
    /// </summary>
    /// <remarks>
    /// Without it, the sign-in is only sent to allowed private hosts. Use it only for a host
    /// which the user or the organization configured, never for one a model named.
    /// </remarks>
    public Func<string, bool>? IsOsSsoAllowedForPublicHost { get; init; }

    public Func<Uri, ConfidenceLevel, Task>? OnPrivateHostProviderBlockAsync { get; init; }
}