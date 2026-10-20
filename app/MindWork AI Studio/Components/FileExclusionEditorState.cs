namespace AIStudio.Components;

/// <summary>
/// Presents the existing exclusion string as two editable groups without losing the other group
/// or letting delayed parameter echoes overwrite newer edits.
/// </summary>
internal sealed class FileExclusionEditorState
{
    private const string PATTERN_SEPARATOR = "; ";

    private string? receivedText;
    private readonly HashSet<string> pendingValues = new(StringComparer.Ordinal);

    public string Text { get; private set; } = string.Empty;

    public string FolderText { get; private set; } = string.Empty;

    public string FileText { get; private set; } = string.Empty;

    public bool IsValid => FileExclusions.IsValidFolders(this.FolderText) && FileExclusions.IsValidFiles(this.FileText);

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

        if (string.Equals(text, this.receivedText, StringComparison.Ordinal))
            return false;

        this.receivedText = text;
        if (string.Equals(text, this.Text, StringComparison.Ordinal))
        {
            this.pendingValues.Clear();
            return false;
        }

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

    public void RestoreFolders(string defaults) => this.EditFolders(string.Join(PATTERN_SEPARATOR, FileExclusions.Parse(defaults).Where(IsFolder)));

    public void RestoreFiles(string defaults) => this.EditFiles(string.Join(PATTERN_SEPARATOR, FileExclusions.Parse(defaults).Where(pattern => !IsFolder(pattern))));

    private void Regroup()
    {
        var patterns = FileExclusions.Parse(this.Text);
        this.FolderText = string.Join(PATTERN_SEPARATOR, patterns.Where(IsFolder));
        this.FileText = string.Join(PATTERN_SEPARATOR, patterns.Where(pattern => !IsFolder(pattern)));
    }

    private static bool IsFolder(string pattern) => pattern.EndsWith(FileExclusions.FOLDER_SUFFIX);

    private void UpdateText()
    {
        this.Text = string.Join(PATTERN_SEPARATOR, FileExclusions.Parse(this.FolderText)
            .Concat(FileExclusions.Parse(this.FileText))
            .Distinct(StringComparer.OrdinalIgnoreCase));
        this.pendingValues.Add(this.Text);
    }
}
