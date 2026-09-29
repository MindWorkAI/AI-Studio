using AIStudio.Provider;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// One row of a data source list: what the source is called, how confidential it is, and whether it
/// can be used right now.
/// </summary>
/// <remarks>
/// A data source which cannot be used stays in the list instead of disappearing from it, but cannot
/// be picked, and the tooltip says why. The tool selection next to it in the chat answers the same
/// question the same way.
///
/// Why it cannot be used decides what the row says and which icon it wears: an index being built
/// anew is a matter of waiting, an index which cannot be read is a matter of acting. Both are the
/// same row otherwise, which is why this is one component with a reason rather than two components.
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
    /// Why this data source cannot be picked right now, if it cannot.
    /// </summary>
    [Parameter]
    public DataSourceBlockReason BlockReason { get; set; } = DataSourceBlockReason.NONE;

    private bool IsBlocked => this.BlockReason is not DataSourceBlockReason.NONE;

    private string GetBlockedTooltip() => this.BlockReason switch
    {
        DataSourceBlockReason.AWAITING_REINDEX => T("This data source is waiting to be indexed again. Until that is finished, it cannot be searched."),
        DataSourceBlockReason.NEEDS_REPAIR => T("The index of this data source cannot be read anymore. Open your data source settings with the gear icon above, then use the repair action there."),
        _ => string.Empty,
    };

    private string GetBlockedIcon() => this.BlockReason switch
    {
        DataSourceBlockReason.NEEDS_REPAIR => Icons.Material.Filled.ReportProblem,
        _ => Icons.Material.Filled.HourglassTop,
    };

    private Color GetBlockedIconColor() => this.BlockReason switch
    {
        DataSourceBlockReason.NEEDS_REPAIR => Color.Error,
        _ => Color.Warning,
    };

    private string GetConfidenceIconStyle(IInternalDataSource dataSource) => $"{dataSource.ConfidenceLevel.SetColorStyle(this.SettingsManager)} flex-shrink: 0;";
}