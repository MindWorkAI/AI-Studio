using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// New chat, split in two: the button itself starts a chat outside of workspaces, and the arrow next to
/// it opens a menu of every place a new chat can go. It is the first row of the list, and stands in the
/// bar above the chat, without its text, while the list is out of sight.
/// </summary>
/// <remarks>
/// <para>
/// The menu starts with the plain new chat, which tells what the button itself does, and lists the
/// workspaces below it in the order of the sidebar, so that the order the user gives the sidebar holds
/// here as well. With many workspaces, they scroll below the plain new chat. The workspace of the open
/// chat carries the primary color, as it does in the sidebar.
/// </para>
/// <para>
/// The menu reads the workspaces each time it opens, so it needs to follow no change of them while it is
/// closed. While a new chat may go into no workspace, the button has no arrow.
/// </para>
/// </remarks>
public partial class NewChatButton : MSGComponentBase
{
    /// <summary>
    /// The tooltip of the pen shows below it, as those of the other buttons in the bar above the chat do.
    /// </summary>
    private const Placement TOOLTIP_PLACEMENT = Placement.Bottom;

    /// <summary>
    /// The workspaces in the menu scroll once they take more than the height of a long menu.
    /// </summary>
    private static readonly string WORKSPACES_STYLE = FormattableString.Invariant($"max-height: {MenuDefaults.MAX_HEIGHT}px; overflow-y: auto;");

    /// <summary>
    /// Whether the button is the first row of the list, with the text "New chat", rather than the pen in
    /// the bar above the chat.
    /// </summary>
    [Parameter]
    public bool IsRow { get; set; }

    /// <summary>
    /// Asks for a new chat in the workspace with the given ID, or outside of workspaces for Guid.Empty.
    /// </summary>
    [Parameter]
    public EventCallback<Guid> StartNewChatRequested { get; set; }

    /// <summary>
    /// The workspace of the open chat, or Guid.Empty while it belongs to none.
    /// </summary>
    [Parameter]
    public Guid CurrentWorkspaceId { get; set; }

    /// <summary>
    /// Whether a new chat may go into a workspace, see DataWorkspace.CanChatsGoIntoWorkspaces.
    /// </summary>
    [Parameter]
    public bool CanChooseWorkspace { get; set; }

    /// <summary>
    /// The workspaces in the order of the sidebar, as the menu read them when it opened last; null before
    /// it opened for the first time.
    /// </summary>
    private IReadOnlyList<WorkspaceTreeWorkspace>? workspaces;

    private async Task MenuOpenChanged(bool isOpen)
    {
        if (!isOpen)
            return;

        var snapshot = await WorkspaceBehaviour.GetOrLoadWorkspaceTreeShellAsync();
        this.workspaces = snapshot.Workspaces;
    }

    private Task StartNewChatAsync(Guid workspaceId) => this.StartNewChatRequested.InvokeAsync(workspaceId);
}