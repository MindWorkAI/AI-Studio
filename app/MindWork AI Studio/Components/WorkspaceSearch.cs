namespace AIStudio.Components;

/// <summary>
/// The search of the workspace sidebar, which its field in the header of the sidebar and the list
/// below share: the text the user typed, how many results the list marks and which one is the current
/// one, and whether the list still searches the messages of the chats.
/// </summary>
/// <remarks>
/// The chat page owns the search and hands it to both, which talk through it. A text passed through
/// the page would have the page render anew with every key the user types, the open chat included.
/// The search also outlives the list, which the page creates anew whenever the sidebar shows up again.
/// </remarks>
public sealed class WorkspaceSearch
{
    /// <summary>
    /// What the user typed into the field.
    /// </summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>
    /// Whether the list searches the contents of the chats, after it showed the chats found by name.
    /// </summary>
    public bool IsSearchingContents { get; private set; }

    /// <summary>
    /// How many results the list marks: workspaces found by their names, and chats.
    /// </summary>
    public int ResultCount { get; private set; }

    /// <summary>
    /// Which of the results is the current one, counted from 1, or 0 when there is none.
    /// </summary>
    public int CurrentResult { get; private set; }

    /// <summary>
    /// Why the search found the current result.
    /// </summary>
    public WorkspaceSearchReason CurrentReason { get; private set; } = WorkspaceSearchReason.NONE;

    /// <summary>
    /// Tells the list that the text changed, which then searches anew.
    /// </summary>
    public event Action? TextChanged;

    /// <summary>
    /// Tells the field that the list started or stopped searching the contents.
    /// </summary>
    public event Action? IsSearchingContentsChanged;

    /// <summary>
    /// Tells the field how many results there are, which is the current one, and why it was found.
    /// </summary>
    public event Action? ResultsChanged;

    /// <summary>
    /// Asks the list to step to the next result, with 1, or the previous one, with -1.
    /// </summary>
    public event Action<int>? StepRequested;

    public void Step(int direction) => this.StepRequested?.Invoke(direction);

    public void SetResults(int count, int current, WorkspaceSearchReason reason)
    {
        if (count == this.ResultCount && current == this.CurrentResult && reason == this.CurrentReason)
            return;

        this.ResultCount = count;
        this.CurrentResult = current;
        this.CurrentReason = reason;
        this.ResultsChanged?.Invoke();
    }

    public void SetText(string? text)
    {
        text ??= string.Empty;
        if (text == this.Text)
            return;

        this.Text = text;
        this.TextChanged?.Invoke();
    }

    public void SetSearchingContents(bool isSearching)
    {
        if (isSearching == this.IsSearchingContents)
            return;

        this.IsSearchingContents = isSearching;
        this.IsSearchingContentsChanged?.Invoke();
    }
}