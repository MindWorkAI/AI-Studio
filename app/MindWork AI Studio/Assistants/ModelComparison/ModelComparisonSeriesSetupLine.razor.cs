using AIStudio.Components;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ToolCallingSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// One line with the setup both models of a comparison series work with: the profile, the tools, the data sources, and
/// how both chats search them.
/// </summary>
/// <remarks>
/// The user needs this when deciding which chat helped more: unneeded sources in an answer may come from classic RAG,
/// which searches with every message, rather than from the model. A part which no longer exists is named as such, so
/// that the line still tells what the series began with.
/// </remarks>
public partial class ModelComparisonSeriesSetupLine : MSGComponentBase
{
    /// <summary>
    /// Gets or sets the series.
    /// </summary>
    [Parameter, EditorRequired]
    public ModelComparisonSeries Series { get; set; } = null!;

    /// <summary>
    /// Gets or sets the classes of the line.
    /// </summary>
    [Parameter]
    public string Class { get; set; } = string.Empty;

    [Inject]
    private ToolRegistry ToolRegistry { get; init; } = null!;

    /// <summary>
    /// The series which the texts below were built from.
    /// </summary>
    private ModelComparisonSeries? shownSeries;

    private string profileText = string.Empty;
    private string toolsText = string.Empty;
    private string dataSourcesText = string.Empty;

    #region Overrides of ComponentBase

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        if (ReferenceEquals(this.shownSeries, this.Series))
            return;

        this.shownSeries = this.Series;
        this.profileText = this.ProfileText();
        this.toolsText = await this.ToolsTextAsync();
        this.dataSourcesText = this.DataSourcesText();
    }

    #endregion

    private string ProfileText()
    {
        if (string.IsNullOrWhiteSpace(this.Series.ProfileId) || string.Equals(this.Series.ProfileId, Profile.NO_PROFILE.Id, StringComparison.OrdinalIgnoreCase))
            return T("No profile");

        var profile = this.SettingsManager.GetProfileById(this.Series.ProfileId);
        return profile == Profile.NO_PROFILE
            ? T("Profile: no longer available")
            : string.Format(T("Profile: {0}"), profile.Name);
    }

    private async Task<string> ToolsTextAsync()
    {
        var toolIds = this.Series.ToolIds.Distinct(StringComparer.Ordinal).ToList();
        if (toolIds.Count == 0)
            return T("No tools");

        List<string> names = [];
        foreach (var toolId in toolIds)
            if (await this.ToolRegistry.GetCatalogItemAsync(toolId) is { } item)
                names.Add(item.DisplayName);

        return string.Format(T("Tools: {0}"), this.ListOf(names, toolIds.Count - names.Count));
    }

    private string DataSourcesText()
    {
        if (this.Series.DataSourceIds.Count == 0)
            return T("No data sources");

        var names = this.Series.DataSourceIds
            .Select(dataSourceId => this.SettingsManager.ConfigurationData.DataSources.FirstOrDefault(dataSource => dataSource.Id == dataSourceId)?.Name)
            .OfType<string>()
            .ToList();

        var retrieval = this.Series.RetrievalMode is DataSourceRetrievalMode.EVERY_MESSAGE ? T("classic RAG") : T("Semantic Search");
        return string.Format(T("Data sources: {0} ({1})"), this.ListOf(names, this.Series.DataSourceIds.Count - names.Count), retrieval);
    }

    /// <summary>
    /// Lists names, followed by how many no longer exist.
    /// </summary>
    private string ListOf(List<string> names, int missingCount)
    {
        if (missingCount > 0)
            names.Add(string.Format(T("{0} no longer available"), missingCount));

        return string.Join(", ", names);
    }
}