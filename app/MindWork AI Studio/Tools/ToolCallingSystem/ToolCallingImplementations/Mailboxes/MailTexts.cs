using AIStudio.Settings.DataModel;
using AIStudio.Tools.Security;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

/// <summary>
/// The texts of a result which came from mails, filtered for prompt injections in one request.
/// </summary>
/// <remarks>
/// A mail tool registers every text it is about to show, the subject as well as an address or the
/// name of an attachment, and reads the filtered texts back once all of them went through the
/// filter together. So the user hears once for the whole call what was filtered, and the result
/// never shows a text of a mail which skipped the filter.
/// </remarks>
internal sealed class MailTexts
{
    private readonly List<PromptInjectionText> texts = [];
    private IReadOnlyList<string> sanitizedTexts = [];

    /// <summary>
    /// The filtered text registered under this index. Only once SanitizeAsync is done.
    /// </summary>
    public string this[int index] => this.sanitizedTexts[index];

    /// <param name="text">The text, as it came from the mail.</param>
    /// <param name="mailbox">The mailbox it came from, which the report to the user names.</param>
    /// <returns>The index under which the filtered text can be read once SanitizeAsync is done.</returns>
    public int Add(string text, DataSourceMailbox mailbox)
    {
        this.texts.Add(new(text, PromptInjectionSource.MailContent(mailbox.Name)));
        return this.texts.Count - 1;
    }

    public async Task SanitizeAsync(PromptInjectionGuardService guardService) => this.sanitizedTexts = await guardService.SanitizeAsync(this.texts);
}