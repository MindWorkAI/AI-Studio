namespace AIStudio.Tools.Web;

public enum WebPageAccessBlockReason
{
    UNSPECIFIED,
    UNSUPPORTED_SCHEME,
    LOCAL_HOST_NAME,
    NEVER_ALLOWED_ADDRESS,
    PRIVATE_HOST_NOT_ALLOWED,
    INSUFFICIENT_PROVIDER_CONFIDENCE,
}

public sealed class WebPageAccessBlockedException : Exception
{
    public WebPageAccessBlockedException(string message) : this(message, WebPageAccessBlockReason.UNSPECIFIED)
    {
    }

    public WebPageAccessBlockedException(string message, WebPageAccessBlockReason reason) : base(message)
    {
        this.Reason = reason;
    }

    public WebPageAccessBlockReason Reason { get; }
}
