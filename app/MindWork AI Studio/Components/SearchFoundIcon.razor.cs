using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// The sign of the chat search: a magnifier in its yellow. It marks a chat which holds the words of the
/// search inside, in the workspace list, and introduces what the search says about it, in the line below
/// the search field and in the row which finds in the open chat.
/// </summary>
/// <remarks>
/// The magnifier takes a darker yellow than the one behind found words, which would hardly show as an
/// icon on white, so it needs no highlight behind it and is sized like any other icon. It takes its size
/// from the font size around it, so each place gets a fitting one without a rule of its own; the list of
/// workspaces gives it the size of its other icons. The current result of the search needs no sign of its
/// own: its row carries the bar of the strong yellow.
/// </remarks>
public partial class SearchFoundIcon : ComponentBase
{
}