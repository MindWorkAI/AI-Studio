namespace AIStudio.Tools.Web;

/// <summary>
/// What loading web content brought, waiting for the reader of its field, see WebContentImportService.TryTakeResult.
/// </summary>
/// <param name="Url">The address the content was loaded from.</param>
/// <param name="Markdown">The content, filtered for prompt injections. It is what arrived before an error, which may be nothing.</param>
/// <param name="Issue">What went wrong, if anything.</param>
/// <param name="ErrorMessage">Why the content could not be loaded, for <see cref="WebContentImportIssue.LOAD_FAILED"/>; null otherwise.</param>
public sealed record WebContentImportResult(string Url, string Markdown, WebContentImportIssue Issue = WebContentImportIssue.NONE, string? ErrorMessage = null);