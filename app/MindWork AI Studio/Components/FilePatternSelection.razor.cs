using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// A file-pattern field with shared, collapsible help and optional, always-visible exclusions.
/// Callers supply their existing fields, preserving validation and managed-configuration locks.
/// </summary>
public partial class FilePatternSelection : MSGComponentBase
{
    /// <summary>
    /// How many lines a field for file patterns or exclusions grows to. A longer list scrolls.
    /// </summary>
    public const int MAX_FIELD_LINES = 6;

    [Parameter, EditorRequired]
    public RenderFragment AllowedContent { get; set; } = null!;

    /// <summary>
    /// When supplied, shows the existing exclusion field below the inclusion field.
    /// Both groups stay visible and use the same help control.
    /// </summary>
    [Parameter]
    public RenderFragment? ExcludedContent { get; set; }

    private bool HasExclusions => this.ExcludedContent is not null;

    private bool showHelp;

    private void ToggleHelp() => this.showHelp = !this.showHelp;
}
