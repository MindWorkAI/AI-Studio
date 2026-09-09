using System.Net;

using AIStudio.Provider;

namespace AIStudio.Tools.Services;

/// <summary>
/// One input which could not be embedded, together with everything known about why.
/// </summary>
/// <remarks>
/// The reason alone used to be all we kept, which made every failure look alike in the UI: a
/// rejected API key, an unreachable provider, and a file nobody may read were one and the same
/// list entry. The surrounding fields are what lets the UI offer the matching way out.
/// </remarks>
/// <param name="FilePath">The file that failed or the name of the data source when the failure was not about one file.</param>
/// <param name="Reason">What to tell the user about it, ready to show.</param>
/// <param name="OccurredAtUtc">When it happened, so the list still makes sense when the user looks at it later.</param>
/// <param name="FailureReason">What kind of failure it was. Everything that did not come from a provider stays at NONE.</param>
/// <param name="StatusCode">What the provider answered, where it answered at all.</param>
/// <param name="EmbeddingProviderName">The embedding provider that was asked.</param>
/// <param name="ExtractionCode">Why reading the file failed, where the failure was about reading it at all.</param>
/// <param name="IsPermanent">Whether the file stays out of the index until it changes.</param>
public sealed record DataSourceEmbeddingFailure(string FilePath, string Reason, DateTimeOffset OccurredAtUtc, ProviderRequestFailureReason FailureReason = ProviderRequestFailureReason.NONE,
    HttpStatusCode? StatusCode = null, string EmbeddingProviderName = "", FileExtractionErrorCode ExtractionCode = FileExtractionErrorCode.NONE, bool IsPermanent = false);