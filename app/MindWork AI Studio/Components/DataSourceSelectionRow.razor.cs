using AIStudio.Provider;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// One row of a data source list: what the source is called, how confidential it is, and whether it
/// can be used right now.
/// </summary>
/// <remarks>
/// A data source waiting for its index stays in the list instead of disappearing from it, but
/// cannot be picked, and the tooltip says why. The tool selection next to it in the chat answers
/// the same question the same way.
///
/// The tooltip sits around the list item rather than inside it: a disabled item has its pointer
/// events switched off and would swallow the hover.
/// </remarks>
public partial class DataSourceSelectionRow : MSGComponentBase
{
    /// <summary>
    /// The data source this row stands for.
    /// </summary>
    [Parameter]
    public required IDataSource DataSource { get; set; }

    /// <summary>
    /// Whether this data source has to be indexed anew before it can answer a search.
    /// </summary>
    [Parameter]
    public bool IsAwaitingReindex { get; set; }

    private string GetConfidenceIconStyle(IInternalDataSource dataSource) => $"{dataSource.ConfidenceLevel.SetColorStyle(this.SettingsManager)} flex-shrink: 0;";
}