using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

/// <summary>
/// Shows the system prompt a chat sends, together with the parts it was assembled from.
/// </summary>
/// <remarks>
/// Nobody types this prompt as it is sent. A chat template replaces the default, a profile, the
/// retrieved data of a data source, and the policy of the selected tools are added to it, and the
/// date goes in front. The list above the text names these parts, so a person does not have to
/// work out from the text alone where a paragraph came from.
/// </remarks>
public partial class SystemPromptDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// The whole system prompt, as the provider receives it.
    /// </summary>
    [Parameter]
    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>
    /// The name of the chat template whose prompt replaces the default, or empty when there is none.
    /// </summary>
    [Parameter]
    public string ChatTemplateName { get; set; } = string.Empty;

    /// <summary>
    /// The name of the profile which takes part, or empty when there is none.
    /// </summary>
    [Parameter]
    public string ProfileName { get; set; } = string.Empty;

    /// <summary>
    /// Whether the chat template forbids a profile, which explains why none takes part even though one may be selected.
    /// </summary>
    [Parameter]
    public bool IsProfileForbiddenByChatTemplate { get; set; }

    /// <summary>
    /// Whether the prompt carries data retrieved from a data source.
    /// </summary>
    [Parameter]
    public bool HasRetrievedData { get; set; }

    /// <summary>
    /// The names of the tools whose usage policy is part of the prompt.
    /// </summary>
    [Parameter]
    public IReadOnlyList<string> ToolNames { get; set; } = [];

    /// <summary>
    /// Whether the current date and time go in front of the prompt.
    /// </summary>
    [Parameter]
    public bool IncludesDateTime { get; set; }

    private string ChatTemplateText => string.IsNullOrWhiteSpace(this.ChatTemplateName)
        ? T("None")
        : this.ChatTemplateName;

    private string ProfileText
    {
        get
        {
            if (this.IsProfileForbiddenByChatTemplate)
                return T("Switched off by the chat template");

            return string.IsNullOrWhiteSpace(this.ProfileName)
                ? T("None")
                : this.ProfileName;
        }
    }

    //
    // Not promised to be refreshed with the next message: the data is replaced only when a new
    // retrieval finds something. Until then, what was retrieved earlier travels along again.
    //
    private string RetrievedDataText => this.HasRetrievedData
        ? T("Included, retrieved for an earlier message")
        : T("None");

    private string ToolNamesText => this.ToolNames.Count == 0
        ? T("None")
        : string.Join(", ", this.ToolNames);

    private string DateTimeText => this.IncludesDateTime
        ? T("Included")
        : T("Not included");

    private void Close() => this.MudDialog.Close();
}