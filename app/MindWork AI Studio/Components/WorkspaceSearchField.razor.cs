using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AIStudio.Components;

/// <summary>
/// The field of the search, in the bar above the list of workspaces and the chat.
/// </summary>
/// <remarks>
/// It hands what the user types to the list and to the open chat through the search. While the list
/// shows beside the chat, the search covers both: the list counts and marks its results, see
/// WorkspaceSearchBar, and the open chat its matches, see ChatFindBar. While the list is hidden, or
/// there is none, the search covers the open chat only. Enter does nothing here on purpose: the
/// composer of the chat sends a prompt with Enter, and a habit carried over from the field must never
/// send a half-written one. The clear button of the field and Escape take back the text.
/// </remarks>
public partial class WorkspaceSearchField : MSGComponentBase
{
    [Parameter, EditorRequired]
    public WorkspaceSearch Search { get; set; } = null!;

    /// <summary>
    /// Whether the list of workspaces shows beside the chat, so that the search covers it as well.
    /// </summary>
    [Parameter]
    public bool IsListSearched { get; set; }

    private string Placeholder => this.IsListSearched ? T("Search chats") : T("Search this chat");

    /// <summary>
    /// What the search covers, which also tells what the content of a chat is, as the results of the list
    /// name it: its messages, the one being written included, the names of its attached files, and its sources.
    /// </summary>
    private string CoverageText => this.IsListSearched
        ? T("Searches the names of chats and workspaces, and the content of the chats: their messages, attached files, and sources.")
        : T("Searches the content of the open chat: its messages and their sources.");

    private void TextChanged(string? text) => this.Search.SetText(text);

    private void KeyDown(KeyboardEventArgs args)
    {
        // Escape takes back what the user typed, as the clear button of the field does:
        if (args.Key is "Escape")
            this.Search.SetText(string.Empty);
    }
}