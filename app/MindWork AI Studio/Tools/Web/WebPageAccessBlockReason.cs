namespace AIStudio.Tools.Web;

public enum WebPageAccessBlockReason
{
    UNSPECIFIED,
    UNSUPPORTED_SCHEME,
    LOCAL_HOST_NAME,
    NEVER_ALLOWED_ADDRESS,
    PRIVATE_HOST_NOT_ALLOWED,
    INSUFFICIENT_PROVIDER_CONFIDENCE,
    TARGET_NOT_ALLOWED,
    PUBLIC_TARGET_NOT_ALLOWED,

    /// <summary>
    /// The address returned a document, such as a PDF, which the request may not read from there.
    /// </summary>
    DOCUMENT_NOT_ALLOWED,
}