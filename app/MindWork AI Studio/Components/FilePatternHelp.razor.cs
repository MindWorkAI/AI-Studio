using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Examples of file patterns, shared by the fields for files to include and for exclusions, so both
/// explain the patterns in the same words.
/// </summary>
public partial class FilePatternHelp : MSGComponentBase
{
    /// <summary>
    /// Shows the selectors and wildcards which only the files to include accept, such as :text:.
    /// </summary>
    [Parameter]
    public bool ShowInclusionExamples { get; set; } = true;

    /// <summary>
    /// Shows an entry which excludes folders.
    /// </summary>
    [Parameter]
    public bool ShowExclusionExamples { get; set; }
}