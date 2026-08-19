namespace AIStudio.Tools.Rust;

/// <summary>
/// A structured issue reported by the native credential store.
/// </summary>
public enum SecretStoreIssueCode
{
    NONE,
    SECRET_NOT_FOUND,
    NO_DEFAULT_COLLECTION,
    COLLECTION_LOCKED,
    PROMPT_DISMISSED,
    SERVICE_UNAVAILABLE,
    UNKNOWN,
}