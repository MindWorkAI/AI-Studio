using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// A compact, two-group editor for the exclusion string shared by folder-based features.
/// </summary>
public partial class FileExclusionEditor : MSGComponentBase
{
    private static readonly Dictionary<string, object?> INPUT_ATTRIBUTES = new()
    {
        ["spellcheck"] = false,
        ["autocorrect"] = "off",
        ["autocapitalize"] = "off",
    };

    [Parameter]
    public string? Text { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> TextChanged { get; set; }

    [Parameter]
    public string Label { get; set; } = string.Empty;

    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// Displays both groups with copy buttons instead of editing and reset controls.
    /// </summary>
    [Parameter]
    public bool ReadOnly { get; set; }

    [Parameter]
    public bool ShowReset { get; set; } = true;

    [Parameter]
    public bool ShowHelp { get; set; } = true;

    [Parameter]
    public bool ShowHeader { get; set; } = true;

    [Parameter]
    public string ResetValue { get; set; } = FileExclusions.DEFAULT_PATTERNS;

    [Parameter]
    public string ResetButtonText { get; set; } = string.Empty;

    /// <summary>
    /// Immediately persists the combined value after restoring just one of its groups.
    /// Ordinary edits continue through TextChanged.
    /// </summary>
    [Parameter]
    public EventCallback<string> ResetRequested { get; set; }

    private readonly FileExclusionEditorState state = new();
    private readonly string folderInputId = $"exclusion-folders-{Guid.NewGuid():N}";
    private readonly string fileInputId = $"exclusion-files-{Guid.NewGuid():N}";
    private bool showHelp;

    private string EditorLabel => string.IsNullOrWhiteSpace(this.Label) ? T("Excluded files and folders") : this.Label;

    private string RestoreLabel => string.IsNullOrWhiteSpace(this.ResetButtonText) ? T("Restore default") : this.ResetButtonText;

    private string FolderRestoreLabel => $"{this.RestoreLabel}: {T("Folders")}";

    private string FileRestoreLabel => $"{this.RestoreLabel}: {T("Files")}";

    protected override void OnParametersSet()
    {
        this.state.Receive(this.Text, discardDraft: this.Disabled || this.ReadOnly);
        base.OnParametersSet();
    }

    protected Task ChangeFoldersAsync(string text) => this.ChangePatternsAsync(text, folders: true);

    protected Task ChangeFilesAsync(string text) => this.ChangePatternsAsync(text, folders: false);

    private async Task ChangePatternsAsync(string text, bool folders)
    {
        if (this.Disabled || this.ReadOnly)
            return;

        var previousText = this.state.Text;
        var wasValid = this.state.IsValid;
        if (folders)
            this.state.EditFolders(text);
        else
            this.state.EditFiles(text);

        // Moving a rule to the correct field can keep the combined text identical. It must still
        // publish the now-valid draft, because the previous, invalid arrangement was not sent.
        if (this.state.IsValid && (!wasValid || !string.Equals(previousText, this.state.Text, StringComparison.Ordinal)))
            await this.TextChanged.InvokeAsync(this.state.Text);
    }

    protected async Task RestoreDefaultsAsync(bool folders)
    {
        if (this.Disabled || this.ReadOnly || !this.ShowReset)
            return;

        if (folders)
            this.state.RestoreFolders(this.ResetValue);
        else
            this.state.RestoreFiles(this.ResetValue);

        if (!this.state.IsValid)
            return;

        if (this.ResetRequested.HasDelegate)
            await this.ResetRequested.InvokeAsync(this.state.Text);
        else
            await this.TextChanged.InvokeAsync(this.state.Text);
    }

    private void ToggleHelp() => this.showHelp = !this.showHelp;
}
