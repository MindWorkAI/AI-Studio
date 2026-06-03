using System.Net;

namespace AIStudio.Provider;

public sealed class ProviderRequestException(
    ProviderRequestFailureReason failureReason,
    string userMessage,
    HttpStatusCode? statusCode = null,
    string reasonPhrase = "",
    string responseBody = "") : Exception(userMessage)
{
    public ProviderRequestException() : this(ProviderRequestFailureReason.NONE, string.Empty)
    {
    }

    public ProviderRequestFailureReason FailureReason { get; } = failureReason;

    public string UserMessage { get; } = userMessage;

    public HttpStatusCode? StatusCode { get; } = statusCode;

    public string ReasonPhrase { get; } = reasonPhrase;

    public string ResponseBody { get; } = responseBody;
}