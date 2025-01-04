namespace AIStudio.Tools;

/// <summary>
/// The result of a rate-limited HTTP stream.
/// </summary>
/// <param name="IsFailedAfterAllRetries">True, when the stream failed after all retries.</param>
/// <param name="ErrorMessage">The error message which we might show to the user.</param>
/// <param name="Response">The response from the server.</param>
public readonly record struct HttpRateLimitedStreamResult(
    bool IsSuccessful,
    bool IsFailedAfterAllRetries,
    string ErrorMessage,
    HttpResponseMessage? Response);