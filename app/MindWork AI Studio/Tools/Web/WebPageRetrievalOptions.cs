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
    /// still capped, and only HTML and text content are still accepted.<br/><br/>
    /// Never set this for a URL that reached AI Studio through a model, however plausible it
    /// looks.
    /// </remarks>
    public bool TargetChosenByUser { get; init; }

    public bool PublicTargetsOnly { get; init; }

    /// <summary>
    /// Whether every request has to reach non-public addresses only, the counterpart of
    /// PublicTargetsOnly.
    /// </summary>
    /// <remarks>
    /// For an address the model chose on a host which counts as internal, such as one of the
    /// allowed private hosts, while its chat may not send data to third parties. A host name says
    /// nothing about where it leads: a pattern such as *.example.org matches public hosts as well,
    /// and an intranet server may redirect anywhere. Every request is therefore checked, redirects
    /// included, and one which would reach a public address is refused before anything is sent.
    /// <br/><br/>
    /// Holds whoever chose the address, because it guards what the address may carry rather than
    /// where the user may go.
    /// </remarks>
    public bool NonPublicTargetsOnly { get; init; }

    public ConfidenceLevel ProviderConfidence { get; init; } = ConfidenceLevel.NONE;

    public bool UseOsSso { get; init; }

    public Func<string, bool>? IsPrivateHostAllowed { get; init; }

    /// <summary>
    /// Decides for every URL, the first one as well as each redirect target, whether it may be
    /// requested at all. It runs before anything is sent, so a refused target never sees the URL.
    /// </summary>
    public Func<Uri, bool>? IsTargetAllowed { get; init; }

    public Func<Uri, ConfidenceLevel, Task>? OnPrivateHostProviderBlockAsync { get; init; }

    /// <summary>
    /// Whether and from where documents such as PDFs are read, or null to refuse them all, as
    /// before documents could be read at all.
    /// </summary>
    public WebDocumentOptions? Documents { get; init; }
}