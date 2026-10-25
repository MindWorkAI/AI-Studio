using AIStudio.Tools.AssistantSessions;
using AIStudio.Tools.PowerPoint;
using AIStudio.Tools.Security;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.SlideBuilder;

/// <summary>
/// The part of the Slide Assistant which offers the LLM a folder of pictures and icons to choose from.
/// </summary>
/// <remarks>
/// The LLM reads only the names of the pictures, never the pictures themselves, and need not use
/// any of them; see PictureLibrary. The library is read anew for each planning, so that a large
/// one offers another random sample each time, and once whenever the user chooses a folder, to tell
/// how many pictures it holds.
/// </remarks>
public partial class SlideAssistant
{
    [Inject]
    private ThreadSafeRandom RandomSource { get; init; } = null!;

    private string pictureLibraryPath = string.Empty;
    private static readonly AssistantSessionStateKey<string> PICTURE_LIBRARY_PATH_STATE_KEY = new(nameof(pictureLibraryPath));

    /// <summary>
    /// How many pictures the chosen library holds, for the hint below the field; it holds no sample.
    /// </summary>
    private PictureLibraryContent pictureLibraryOverview = PictureLibraryContent.NONE;

    private bool isReadingPictureLibrary;
    private CancellationTokenSource? pictureLibraryReading;

    /// <summary>
    /// What the library offers to the planning under way, while the system prompt is written for it.
    /// </summary>
    private PictureLibraryContent planningLibrary = PictureLibraryContent.NONE;

    private async Task OnPictureLibraryChanged(string path)
    {
        this.pictureLibraryPath = path;
        await this.ReadPictureLibraryOverviewAsync();
        if (this.Form is not null)
            await this.Form.Validate();
    }

    private async Task UseNoPictureLibrary() => await this.OnPictureLibraryChanged(string.Empty);

    private string? ValidatePictureLibrary(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        return Directory.Exists(path) ? null : T("The folder does not exist.");
    }

    /// <summary>
    /// Counts the pictures of the chosen library for the hint, and cancels a count still running for another folder.
    /// </summary>
    private async Task ReadPictureLibraryOverviewAsync()
    {
        if (this.pictureLibraryReading is { } previousReading)
        {
            await previousReading.CancelAsync();
            previousReading.Dispose();
        }

        this.pictureLibraryOverview = PictureLibraryContent.NONE;
        var path = this.pictureLibraryPath;
        if (path.Length == 0 || !Directory.Exists(path))
        {
            this.pictureLibraryReading = null;
            return;
        }

        var reading = new CancellationTokenSource();
        this.pictureLibraryReading = reading;
        this.isReadingPictureLibrary = true;
        this.StateHasChanged();
        try
        {
            var overview = await PictureLibrary.ReadAsync(path, this.RandomSource, 0, this.ReportInaccessibleLibraryFolder, reading.Token);
            if (!reading.IsCancellationRequested)
                this.pictureLibraryOverview = overview;
        }
        catch (OperationCanceledException)
        {
            // Another folder was chosen meanwhile, and its count is under way.
        }
        catch (Exception e)
        {
            this.Logger.LogWarning(e, "The picture library '{FolderPath}' cannot be read.", path);
        }
        finally
        {
            if (ReferenceEquals(this.pictureLibraryReading, reading))
                this.isReadingPictureLibrary = false;
        }
    }

    /// <summary>
    /// Reads the library for a planning, with a new random sample when it is large.
    /// </summary>
    /// <returns>What the library offers; nothing when the user chose no library or it cannot be read.</returns>
    private async Task<PictureLibraryContent> ReadPictureLibraryForPlanningAsync()
    {
        var path = this.pictureLibraryPath;
        if (path.Length == 0 || !Directory.Exists(path))
            return PictureLibraryContent.NONE;

        try
        {
            return await PictureLibrary.ReadAsync(path, this.RandomSource, PictureLibrary.MAX_LIBRARY_PICTURES, this.ReportInaccessibleLibraryFolder, this.CancellationTokenSource?.Token ?? CancellationToken.None);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            this.Logger.LogWarning(e, "The picture library '{FolderPath}' cannot be read, so the slides are planned without it.", path);
            return PictureLibraryContent.NONE;
        }
    }

    private void ReportInaccessibleLibraryFolder(string folderPath, Exception e) => this.Logger.LogWarning(e, "The folder '{FolderPath}' of the picture library cannot be read.", folderPath);

    /// <summary>
    /// What the chosen library holds, below the field; empty while there is nothing to tell.
    /// </summary>
    private string PictureLibraryHint
    {
        get
        {
            if (this.isReadingPictureLibrary)
                return T("Counting the pictures of the folder...");

            if (this.pictureLibraryPath.Length == 0 || !Directory.Exists(this.pictureLibraryPath))
                return string.Empty;

            var library = this.pictureLibraryOverview;
            var hints = new List<string>
            {
                library.PictureCount == 0
                    ? T("The folder holds no PNG or JPEG pictures.")
                    : string.Format(T("{0} pictures, {1} of them icons."), library.PictureCount.CompactCount(), library.IconCount.CompactCount()),
            };

            if (library.SkippedFileCount > 0)
                hints.Add(string.Format(T("{0} other files are left out, because they are neither PNG nor JPEG."), library.SkippedFileCount.CompactCount()));

            if (library.PictureCount > PictureLibrary.MAX_LIBRARY_PICTURES)
                hints.Add(string.Format(T("For each planning, the LLM gets a random {0} of them."), MaxLibraryPictures));

            return string.Join(" ", hints);
        }
    }

    /// <summary>
    /// The most pictures of a library the LLM reads about, as the user reads the number.
    /// </summary>
    private static string MaxLibraryPictures => PictureLibrary.MAX_LIBRARY_PICTURES.ToString("N0", Tools.PluginSystem.I18N.I.Culture);

    /// <summary>
    /// The pictures of the library by their numbers, which follow those of the pictures for the slides, for the user's request.
    /// </summary>
    /// <remarks>
    /// The names come from a folder, which may be a network drive anybody can write to, so they pass
    /// the prompt injection filter like the texts of a template.
    /// </remarks>
    /// <param name="guardService">The prompt injection filter.</param>
    /// <param name="promptInjectionScope">The action of the request, which reports what the filter removed.</param>
    /// <param name="library">What the library offers for this request.</param>
    /// <param name="firstNumber">The number of the first picture of the library.</param>
    /// <param name="folderPath">The library folder.</param>
    /// <returns>The section of the request; empty without pictures in the library.</returns>
    private static async Task<string> PromptPictureLibraryAsync(PromptInjectionGuardService guardService, PromptInjectionReportingScope promptInjectionScope, PictureLibraryContent library, int firstNumber, string folderPath)
    {
        if (library.Pictures.Count == 0)
            return string.Empty;

        var list = string.Join(Environment.NewLine, library.Pictures.Select((picture, index) => $"- Picture {firstNumber + index}: {picture.Name}{(picture.IsIcon ? " (icon)" : string.Empty)}"));
        var sanitizedList = await guardService.SanitizeAsync(list, PromptInjectionSource.FileContent(folderPath), promptInjectionScope);
        return $"""

                # PICTURE_LIBRARY
                {sanitizedList}
                """;
    }

    /// <summary>
    /// The rules for the pictures of the library; empty without them.
    /// </summary>
    private string PromptPictureLibraryRules() => this.planningLibrary.Pictures.Count == 0
        ? string.Empty
        : """
              - PICTURE_LIBRARY lists further pictures by their numbers and names; you know only their names. Where a name fits the content of a slide with a picture slot, you may place that picture there by its number. You need not use any of them, and you use each one at most once.
              - Pictures marked "(icon)" are icons. An icon may appear on several slides, e.g., to mark each risk the same way, and suits picture slots of any size, except those marked as backdrop.
          """;

    /// <inheritdoc />
    protected override void DisposeResources()
    {
        this.pictureLibraryReading?.Cancel();
        this.pictureLibraryReading?.Dispose();
        this.pictureLibraryReading = null;
        base.DisposeResources();
    }
}
