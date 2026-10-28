namespace AIStudio.Components;

/// <summary>
/// Presents the existing exclusion string as two editable groups without losing the other group
/// or letting delayed parameter echoes overwrite newer edits.
/// </summary>
internal sealed class FileExclusionEditorState
{
    private string? receivedText;
    private readonly HashSet<string> pendingValues = new(StringComparer.Ordinal);

    public string Text { get; private set; } = string.Empty;

    public string FolderText { get; private set; } = string.Empty;

    public string FileText { get; private set; } = string.Empty;

    public bool IsValid => FileExclusions.IsValidFolders(this.FolderText) && FileExclusions.IsValidFiles(this.FileText);

    /// <summary>
    /// Takes over the text of the parent, which renders again after every published edit and passes
    /// that edit back, possibly only after the user typed on. Such an echo must not replace the newer
    /// draft; any other text does, e.g., a change by a configuration plugin.
    /// </summary>
    /// <param name="text">The text the parent passes in.</param>
    /// <param name="discardDraft">Whether to take over the text in any case, e.g., while the editor is disabled.</param>
    /// <returns>Whether the text replaced the draft.</returns>
    public bool Receive(string? text, bool discardDraft = false)
    {
        text ??= string.Empty;
        if (discardDraft)
        {
            var changed = !string.Equals(text, this.Text, StringComparison.Ordinal);
            this.receivedText = text;
            this.Reset(text);
            return changed;
        }

        // The parent passes the same text with every render; only a different one counts:
        if (string.Equals(text, this.receivedText, StringComparison.Ordinal))
            return false;

        this.receivedText = text;

        // The parent caught up with the draft. Its renders arrive in order, so no older echo follows:
        if (string.Equals(text, this.Text, StringComparison.Ordinal))
        {
            this.pendingValues.Clear();
            return false;
        }

        // The echo of an earlier edit, which a later one replaced already:
        if (this.pendingValues.Remove(text))
            return false;

        this.Reset(text);
        return true;
    }

    public void Reset(string text)
    {
        this.pendingValues.Clear();
        this.Text = text;
        this.Regroup();
    }

    public void EditFolders(string? text)
    {
        this.FolderText = text ?? string.Empty;
        this.UpdateText();
    }

    public void EditFiles(string? text)
    {
        this.FileText = text ?? string.Empty;
        this.UpdateText();
    }

    public void RestoreFolders(string defaults) => this.EditFolders(FileExclusions.Group(defaults).Folders);

    public void RestoreFiles(string defaults) => this.EditFiles(FileExclusions.Group(defaults).Files);

    private void Regroup()
    {
        var groups = FileExclusions.Group(this.Text);
        this.FolderText = groups.Folders;
        this.FileText = groups.Files;
    }

    private void UpdateText()
    {
        this.Text = string.Join(FileExclusions.ENTRY_SEPARATOR, FileExclusions.Parse(this.FolderText)
            .Concat(FileExclusions.Parse(this.FileText))
            .Distinct(StringComparer.OrdinalIgnoreCase));
        this.pendingValues.Add(this.Text);
    }
}
