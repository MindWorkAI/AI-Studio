using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

/// <summary>
/// Shows the system prompt a chat sends, part by part, each under the name of where it comes from.
/// </summary>
/// <remarks>
/// Nobody types this prompt as it is sent. A chat template replaces the default, a profile, the
/// retrieved data of a data source, and the policy of the selected tools are added to it, and the
/// date goes in front. Each part stands under its own heading, in the order of the prompt, so a
/// person does not have to work out from the text alone where a paragraph came from.<br/><br/>
///
/// The parts are those ChatThread.BuildSystemPrompt recorded, so the dialog decides nothing about
/// them. Only the retrieved data needs a word of its own: AI Studio works it out anew whenever a
/// message is sent, see AISrcSelWithRetCtxVal.ProcessAsync. What the dialog shows of it was sent with
/// the last message, and the next one gets what the search finds then. Before the first search,
/// e.g., in a new chat or one just loaded, there is nothing to show, but the chat may still search.
/// Two more things stand where they would be: a selected profile which the chat template does not
/// allow, and the instruction the tool calling loop adds once the limit of tool calls is reached.
/// </remarks>
public partial class SystemPromptDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Inject]
    private RustService RustService { get; init; } = null!;

    /// <summary>
    /// The system prompt, together with the parts it was assembled from.
    /// </summary>
    [Parameter]
    public PreparedSystemPrompt Prompt { get; set; } = null!;

    /// <summary>
    /// The display names of the tools whose usage policy is part of the prompt.
    /// </summary>
    [Parameter]
    public IReadOnlyList<string> ToolNames { get; set; } = [];

    /// <summary>
    /// How the next message searches the data sources of the chat, or null when it searches none.
    /// </summary>
    [Parameter]
    public DataSourceRetrievalMode? RetrievalMode { get; set; }

    private IReadOnlyList<Section> sections = [];

    #region Overrides of ComponentBase

    protected override void OnParametersSet()
    {
        this.sections = this.BuildSections();
        base.OnParametersSet();
    }

    #endregion

    /// <summary>
    /// Lists the parts of the prompt in its order, with what stands where nothing was added.
    /// </summary>
    /// <remarks>
    /// Retrieved data follows the base prompt or the chat template, and a profile follows the
    /// retrieved data. So a chat which has not retrieved anything yet, but searches its data sources,
    /// says so right after the base prompt, and a profile which the chat template switched off is
    /// named where it would otherwise stand.
    /// </remarks>
    private IReadOnlyList<Section> BuildSections()
    {
        var result = new List<Section>(this.Prompt.Parts.Count + 2);
        var hasRetrievedData = this.Prompt.Parts.Any(part => part.Kind is SystemPromptPartKind.RETRIEVED_DATA);
        foreach (var part in this.Prompt.Parts)
        {
            result.Add(this.SectionOf(part));
            switch (part.Kind)
            {
                case SystemPromptPartKind.BASE_PROMPT or SystemPromptPartKind.CHAT_TEMPLATE when !hasRetrievedData:
                    if (this.NoticeWithoutRetrievedData is { Length: > 0 } notice)
                        result.Add(new(T("Data from your data sources"), string.Empty, notice));

                    this.AddSwitchedOffProfile(result);
                    break;

                case SystemPromptPartKind.RETRIEVED_DATA:
                    this.AddSwitchedOffProfile(result);
                    break;
            }
        }

        return result;
    }

    private Section SectionOf(SystemPromptPart part) => part.Kind switch
    {
        SystemPromptPartKind.DATE_TIME => new(T("Current date and time"), part.Text, string.Empty),
        SystemPromptPartKind.BASE_PROMPT when part.Text == SystemPrompts.DEFAULT => new(T("Default prompt of AI Studio"), part.Text, string.Empty),
        SystemPromptPartKind.BASE_PROMPT => new(T("System prompt of this chat"), part.Text, string.Empty),
        SystemPromptPartKind.CHAT_TEMPLATE => new(string.Format(T("Chat template '{0}'"), part.SourceName), part.Text, string.Empty),
        SystemPromptPartKind.RETRIEVED_DATA => new(T("Data from your data sources"), part.Text, this.NoticeWithRetrievedData),
        SystemPromptPartKind.PROFILE => new(string.Format(T("Profile '{0}'"), part.SourceName), part.Text, string.Empty),
        SystemPromptPartKind.TOOL_INSTRUCTIONS => new(string.Format(T("Instructions for your tools: {0}"), string.Join(", ", this.ToolNames)), part.Text, T("When the AI reaches the limit of tool calls for one answer, AI Studio adds one more instruction at the end: to answer with what the tools have returned so far.")),
        _ => new(string.Empty, part.Text, string.Empty),
    };

    private void AddSwitchedOffProfile(List<Section> result)
    {
        if (this.Prompt.SwitchedOffProfile == Profile.NO_PROFILE)
            return;

        result.Add(new(string.Format(T("Profile '{0}'"), this.Prompt.SwitchedOffProfile.Name), string.Empty, T("The chat template does not allow profiles, so your selected profile is not part of the prompt.")));
    }

    private string NoticeWithRetrievedData => this.RetrievalMode is DataSourceRetrievalMode.EVERY_MESSAGE
        ? T("AI Studio retrieved this data for your last message. When you send your next message, AI Studio searches your data sources again and replaces this data with what it finds then.")
        : T("AI Studio sent this data with your last message. When you send your next message, AI Studio works out this part anew.");

    private string NoticeWithoutRetrievedData => this.RetrievalMode switch
    {
        DataSourceRetrievalMode.EVERY_MESSAGE => T("When you send your next message, AI Studio searches your data sources and adds what it finds here."),
        DataSourceRetrievalMode.SEMANTIC_SEARCH => T("The AI searches your data sources itself whenever a question calls for it. What it finds reaches the AI as the result of that search, not as part of this prompt."),
        _ => string.Empty,
    };

    private async Task CopyCompletePrompt() => await this.RustService.CopyText2Clipboard(this.Prompt.Text);

    private void Close() => this.MudDialog.Close();

    /// <summary>
    /// One part of the prompt as the dialog shows it.
    /// </summary>
    /// <param name="Heading">Where the part comes from.</param>
    /// <param name="Text">The text of the part, or empty when only the notice stands there.</param>
    /// <param name="Notice">What the user should know about the part, or empty when there is nothing to say.</param>
    private sealed record Section(string Heading, string Text, string Notice);
}