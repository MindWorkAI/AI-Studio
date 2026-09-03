namespace AIStudio.Tools.Web;

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
