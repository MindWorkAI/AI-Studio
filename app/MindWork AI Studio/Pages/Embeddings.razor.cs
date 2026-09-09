using AIStudio.Components;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Pages;

public partial class Embeddings : MSGComponentBase
{
    private static readonly int[] PAGE_SIZE_OPTIONS = [10, 25, 50, 100];

    [Inject]
    private DataSourceEmbeddingService DataSourceEmbeddingService { get; init; } = null!;

    [Inject]
    private NavigationManager NavigationManager { get; init; } = null!;

    [Inject]
    private RustService RustService { get; init; } = null!;

    [Inject]
    private ILogger<Embeddings> Logger { get; init; } = null!;

    private IReadOnlyList<DataSourceEmbeddingStatus> Statuses { get; set; } = [];

    private int TotalIndexedFiles => this.Statuses.Sum(status => status.IndexedFiles);

    private int TotalPendingFiles => this.Statuses.Sum(status => Math.Max(0, status.TotalFiles - status.IndexedFiles - status.FailedFiles - status.PermanentlySkippedFiles));

    private int TotalFailedFiles => this.Statuses.Sum(status => status.FailedFiles);

    private int TotalPermanentlySkippedFiles => this.Statuses.Sum(status => status.PermanentlySkippedFiles);

    protected override async Task OnInitializedAsync()
    {
        //
        // This page belongs to the local RAG preview feature. Unlike the other preview pages, it
        // has a route of its own, so it can be reached by typing the address even while the feature
        // is switched off. There is nothing to show in that case.
        //
        if (!PreviewFeatures.PRE_RAG_2024.IsEnabled(this.SettingsManager))
        {
            this.NavigationManager.NavigateTo(Routes.HOME);
            return;
        }

        this.ApplyFilters([], [ Event.RAG_EMBEDDING_STATUS_CHANGED, Event.CONFIGURATION_CHANGED ]);
        await base.OnInitializedAsync();
        this.ReloadStatuses();
    }

    protected override Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        if (triggeredEvent is Event.RAG_EMBEDDING_STATUS_CHANGED or Event.CONFIGURATION_CHANGED)
        {
            this.ReloadStatuses();
            this.StateHasChanged();
        }

        return Task.CompletedTask;
    }

    private void ReloadStatuses()
    {
        this.Statuses = this.DataSourceEmbeddingService
            .GetStatuses()
            .OrderBy(status => status.SortOrder)
            .ThenBy(status => status.DataSourceName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Color GetStatusColor(DataSourceEmbeddingStatus status) => status.State switch
    {
        DataSourceEmbeddingState.RUNNING => Color.Warning,
        DataSourceEmbeddingState.QUEUED => Color.Info,
        DataSourceEmbeddingState.FAILED => Color.Error,
        DataSourceEmbeddingState.COMPLETED when status.FailedFiles > 0 => Color.Warning,
        DataSourceEmbeddingState.COMPLETED => Color.Success,
        _ => Color.Default,
    };

    /// <summary>
    /// What a group of failures has in common.
    /// </summary>
    /// <remarks>
    /// Also the key the failures are grouped by: two of them belong together exactly when the
    /// list would say the same thing about both.
    /// </remarks>
    private sealed record FailureCause(int Priority, string Title, string Icon, Color Color, bool IsPermanent, bool NeedsProviderSettings, bool ShowsMessagePerFile);

    private sealed record FailureGroup(FailureCause Cause, string EmbeddingProviderName, IReadOnlyList<DataSourceEmbeddingFailure> Failures);

    /// <summary>
    /// Puts the failures of a data source into one group per cause, the most pressing one first.
    /// </summary>
    /// <remarks>
    /// Within a priority, the largest group comes first: it is the one telling the user the most
    /// about their folder.
    /// </remarks>
    private IReadOnlyList<FailureGroup> GetFailureGroups(DataSourceEmbeddingStatus status) => status.Failures
        .GroupBy(this.GetFailureCause)
        .Select(group => new FailureGroup(group.Key, GetEmbeddingProviderName(group), group.OrderBy(failure => failure.FilePath, StringComparer.OrdinalIgnoreCase).ToList()))
        .OrderBy(group => group.Cause.Priority)
        .ThenByDescending(group => group.Failures.Count)
        .ThenBy(group => group.Cause.Title, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private FailureCause GetFailureCause(DataSourceEmbeddingFailure failure)
    {
        //
        // Anything the provider answered comes first: it stops the entire data source, while a
        // file nobody can read costs that one file:
        //
        if (failure.FailureReason is not ProviderRequestFailureReason.NONE)
        {
            var isFixedInSettings = failure.FailureReason.IsFixedInProviderSettings();
            return new FailureCause(isFixedInSettings ? 0 : 1, failure.FailureReason.GetName(), isFixedInSettings ? Icons.Material.Filled.Key : Icons.Material.Filled.CloudOff, Color.Error, false, isFixedInSettings, true);
        }

        //
        // Codes without a name of their own carry everything they know in the message of the
        // single file, which is why those groups show that message per file:
        //
        var causeName = failure.ExtractionCode.GetIndexingCauseName();
        var hasCauseName = !string.IsNullOrWhiteSpace(causeName);
        var title = hasCauseName ? causeName : T("Other cause");

        return failure.IsPermanent
            ? new FailureCause(3, title, Icons.Material.Filled.SkipNext, Color.Default, true, false, !hasCauseName)
            : new FailureCause(2, title, Icons.Material.Filled.ReportProblem, Color.Warning, false, false, !hasCauseName);
    }

    private static string GetEmbeddingProviderName(IEnumerable<DataSourceEmbeddingFailure> failures) => failures
        .Select(failure => failure.EmbeddingProviderName)
        .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? string.Empty;

    private static string GetGroupHeader(FailureGroup group) => $"{group.Cause.Title} ({group.Failures.Count})";

    private static string GetFileName(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return string.IsNullOrWhiteSpace(fileName) ? filePath : fileName;
    }

    private static string GetOccurrenceText(DataSourceEmbeddingFailure failure) => failure.OccurredAtUtc > DateTimeOffset.MinValue ? failure.OccurredAtUtc.ToLocalTime().ToString("g") : string.Empty;

    /// <remarks>
    /// A failure which was not about one file, such as a folder which is gone, carries the name of
    /// the data source instead of a path. There is nothing to show for those.
    /// </remarks>
    private static bool CanShowInFileManager(DataSourceEmbeddingFailure failure) => !string.IsNullOrWhiteSpace(failure.FilePath) && Path.IsPathRooted(failure.FilePath);

    /// <summary>
    /// Opens the file browser of the system and selects the file in it.
    /// </summary>
    /// <remarks>
    /// Reading that a file could not be indexed is where the work starts, not where it ends: the
    /// file has to be opened, replaced, or run through an OCR. This is the same way out the log
    /// viewer offers for the log files.
    /// </remarks>
    private async Task ShowInFileManager(DataSourceEmbeddingFailure failure)
    {
        OpenPathResponse response;
        try
        {
            response = await this.RustService.TryOpenPathInRuntimeFileManager(failure.FilePath);
        }
        catch (Exception e)
        {
            this.Logger.LogWarning(e, "Could not show a file of the embedding failure list in the file manager.");
            await this.MessageBus.SendError(new(Icons.Material.Filled.Folder, T("Could not open the file location.")));
            return;
        }

        if (response.Success)
            return;

        var issue = string.IsNullOrWhiteSpace(response.Issue) ? T("Unknown error") : response.Issue;
        await this.MessageBus.SendError(new(Icons.Material.Filled.Folder, string.Format(T("Could not open the file location: {0}"), issue)));
    }

    private bool CanRefresh(DataSourceEmbeddingStatus status)
    {
        return this.DataSourceEmbeddingService.CanRefreshDataSource(status.DataSourceId) &&
            status.State is not DataSourceEmbeddingState.RUNNING and not DataSourceEmbeddingState.QUEUED &&
            (status.State is DataSourceEmbeddingState.FAILED || status.FailedFiles > 0);
    }

    /// <summary>
    /// Takes the user to the settings, where the embedding providers are configured.
    /// </summary>
    /// <remarks>
    /// Offered only for the failures a setting fixes, such as a rejected API key. Reading what
    /// went wrong and then having to find the right page is where people give up.
    /// </remarks>
    private void OpenEmbeddingProviderSettings() => this.NavigationManager.NavigateTo(Routes.SETTINGS);

    private async Task RefreshDataSource(DataSourceEmbeddingStatus status)
    {
        await this.DataSourceEmbeddingService.RetryDataSourceAsync(status.DataSourceId);
        this.ReloadStatuses();
        await this.InvokeAsync(this.StateHasChanged);
    }
}
