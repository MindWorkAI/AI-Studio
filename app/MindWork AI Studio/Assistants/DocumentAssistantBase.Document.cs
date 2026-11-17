using AIStudio.Tools.AssistantSessions;
using AIStudio.Tools.DocumentSegments;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Security;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants;

public abstract partial class DocumentAssistantBase<TSettings>
{
    /// <summary>
    /// How many texts go through the prompt injection filter in one request to the runtime.
    /// </summary>
    private const int FILTER_CHUNK_SIZE = 200;

    private static readonly IReadOnlySet<string> NO_TOOLS = new HashSet<string>();

    private static readonly AssistantSessionStateKey<string> DOCUMENT_PATH_STATE_KEY = new(nameof(documentPath));
    private static readonly AssistantSessionStateKey<EditedDocument?> EDITED_DOCUMENT_STATE_KEY = new(nameof(editedDocument));
    private static readonly AssistantSessionStateKey<SegmentProgress?> DOCUMENT_PROGRESS_STATE_KEY = new(nameof(documentProgress));

    [Inject]
    private PromptInjectionGuardService PromptInjectionGuard { get; init; } = null!;

    private string documentPath = string.Empty;
    private EditedDocument? editedDocument;
    private SegmentProgress? documentProgress;
    private bool isSavingDocument;

    /// <summary>
    /// Whether the run which just ended worked on a document, which the user gets to save right away.
    /// </summary>
    private bool offerSavingDocument;

    private string DocumentProgressText => this.documentProgress is { } progress
        ? string.Format(this.TB("{0} of {1} sections done …"), progress.DoneSegments, progress.TotalSegments)
        : this.TB("Reading the document …");

    #region Overrides of AssistantBase

    protected override async Task OnRunFinishedAsync(AssistantSessionStatus status)
    {
        var offerSaving = this.offerSavingDocument;
        this.offerSavingDocument = false;
        if (offerSaving && status is AssistantSessionStatus.COMPLETED && this.Mode is AssistantContentMode.DOCUMENT)
            await this.SaveEditedDocumentAsync();

        await base.OnRunFinishedAsync(status);
    }

    #endregion

    private string? ValidateDocument(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return this.TB("Please choose a document.");

        if (!FileTypes.IsAllowedPath(path, FileTypes.EDITABLE_DOCUMENT))
            return this.TB("This kind of file is not supported. Please choose a Word, PowerPoint, LaTeX, Markdown, or HTML file.");

        if (DocumentSegmentFormats.For(path) is null)
            return this.TB("Documents of this kind are not supported yet.");

        return null;
    }

    /// <summary>
    /// Lets the model work on every text of the chosen document; the user saves the new document afterward.
    /// </summary>
    /// <remarks>
    /// The submit action of the assistant calls this while it works on a document.
    /// </remarks>
    protected async Task EditDocumentAsync()
    {
        await this.Form!.ValidateAsync();
        if (!this.InputIsValid || DocumentSegmentFormats.For(this.documentPath) is not { } format)
            return;

        var token = this.CancellationTokenSource?.Token ?? CancellationToken.None;
        var sourcePath = this.documentPath;
        var request = this.CreateDocumentRequest();

        this.editedDocument = null;
        await this.PublishDocumentProgressAsync(null);

        IReadOnlyList<DocumentSegment> segments;
        try
        {
            segments = await format.ReadAsync(sourcePath, token);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            this.Logger.LogError(e, "The assistant '{AssistantTitle}' could not read the document '{DocumentPath}'.", this.Title, sourcePath);
            await this.MessageBus.SendError(new(Icons.Material.Filled.Description, this.TB("The document could not be read. Please check that it is a valid file and that no other program locks it.")));
            return;
        }

        var segmentsWithWords = segments.Where(segment => segment.HasWords).DistinctBy(segment => segment.Key).ToList();
        if (segmentsWithWords.Count == 0)
        {
            await this.MessageBus.SendWarning(new(Icons.Material.Filled.Description, this.TB("The document holds no text.")));
            return;
        }

        // The texts of the document are content of a file, which the user hears about once, when the whole run is over:
        await using var promptInjectionScope = this.PromptInjectionGuard.BeginAction();
        var filteredKeys = await this.FindFilteredSegmentsAsync(segmentsWithWords, sourcePath, promptInjectionScope);

        var batches = SegmentBatchPlanner.Plan(segmentsWithWords.Where(segment => !filteredKeys.Contains(segment.Key)));
        var transformer = new SegmentTransformer(request.Instructions, (batchRequest, requestToken) => this.AskInPrivateThreadAsync(request.SystemPrompt, batchRequest, requestToken), this.Logger);
        var outcomes = new Dictionary<string, SegmentOutcome>(await transformer.TransformAsync(batches, progress => this.PublishDocumentProgressAsync(progress), token), StringComparer.Ordinal);
        foreach (var key in filteredKeys)
            outcomes[key] = SegmentOutcome.FILTERED;

        var results = new SegmentResults(outcomes);
        this.Logger.LogInformation("The assistant '{AssistantTitle}' worked on {TransformedCount} sections of '{DocumentPath}' in {BatchCount} batches; {FilteredCount} were filtered, and {FailedCount} failed.", this.Title, results.CountOf(SegmentStatus.TRANSFORMED), sourcePath, batches.Count, results.CountOf(SegmentStatus.FILTERED), results.CountOf(SegmentStatus.FAILED));

        this.editedDocument = new(sourcePath, results, request.LanguageTag, request.Label);
        this.offerSavingDocument = true;
    }

    /// <summary>
    /// Passes the texts of the document through the prompt injection filter.
    /// </summary>
    /// <returns>The texts the filter changed, which stay as they are and never reach the model.</returns>
    private async Task<HashSet<string>> FindFilteredSegmentsAsync(IReadOnlyList<DocumentSegment> segments, string sourcePath, PromptInjectionReportingScope scope)
    {
        var filteredKeys = new HashSet<string>(StringComparer.Ordinal);
        var source = PromptInjectionSource.FileContent(sourcePath);
        foreach (var chunk in segments.Chunk(FILTER_CHUNK_SIZE))
        {
            var sanitizedTexts = await this.PromptInjectionGuard.SanitizeAsync(chunk.Select(segment => new PromptInjectionText(segment.Key, source)).ToList(), scope);
            for (var index = 0; index < chunk.Length; index++)
            {
                if (!string.Equals(sanitizedTexts[index], chunk[index].Key, StringComparison.Ordinal))
                    filteredKeys.Add(chunk[index].Key);
            }
        }

        return filteredKeys;
    }

    private async Task PublishDocumentProgressAsync(SegmentProgress? progress)
    {
        this.documentProgress = progress;
        await this.CheckpointAssistantSession();
        await this.RefreshAssistantUIAsync();
    }

    private async Task SaveEditedDocumentAsync()
    {
        if (this.isSavingDocument || this.editedDocument is not { } document || DocumentSegmentFormats.For(document.SourcePath) is not { } format)
            return;

        var suggestedName = $"{Path.GetFileNameWithoutExtension(document.SourcePath)} ({FileExportFormatExtensions.ToFileNameFragment(document.Label)}){Path.GetExtension(document.SourcePath)}";
        var response = await this.RustService.SaveFile(this.DocumentSaveText, [format.FileType], suggestedName);
        if (response.UserCancelled)
            return;

        var targetPath = response.SaveFilePath;
        if (IsSameFile(targetPath, document.SourcePath))
        {
            await this.MessageBus.SendWarning(new(Icons.Material.Filled.Description, this.TB("Please choose another file than the original document, which stays as it is.")));
            return;
        }

        this.isSavingDocument = true;
        await this.RefreshAssistantUIAsync();

        await using var feedback = new FileExportFeedback();
        feedback.ShowRunning(targetPath);
        try
        {
            var result = await format.WriteAsync(document.SourcePath, targetPath, document.Results, document.LanguageTag, this.Logger, CancellationToken.None);
            if (result.Outcome is SegmentWriteOutcome.SOURCE_CHANGED)
            {
                this.Logger.LogWarning("The document '{DocumentPath}' changed since the assistant '{AssistantTitle}' worked on it, so the new document was not saved.", document.SourcePath, this.Title);
                await this.MessageBus.SendError(new(Icons.Material.Filled.Description, this.TB("The original document has changed in the meantime. Please start again.")));
                return;
            }

            this.Logger.LogInformation("The assistant '{AssistantTitle}' saved '{TargetPath}' with {ExactCount} sections in full and {SimplifiedCount} simplified.", this.Title, targetPath, result.ExactCount, result.SimplifiedCount);
            if (result.SimplifiedCount > 0)
                await this.MessageBus.SendWarning(new(Icons.Material.Filled.FormatClear, string.Format(this.TB("In {0} sections, the AI model did not keep all marks of the formatting, so parts of their formatting, such as links or bold words, are missing."), result.SimplifiedCount)));

            await feedback.SucceededAsync(Icons.Material.Filled.Description, this.TB("The new document was saved."), targetPath, true);
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "The assistant '{AssistantTitle}' could not save the new document '{TargetPath}'.", this.Title, targetPath);
            await this.MessageBus.SendError(new(Icons.Material.Filled.Description, this.TB("The new document could not be saved. Please check that the original document can be read and that the target file is not open in another program.")));
        }
        finally
        {
            this.isSavingDocument = false;
            await this.RefreshAssistantUIAsync();
        }
    }

    private static bool IsSameFile(string first, string second) => string.Equals(
        Path.GetFullPath(first),
        Path.GetFullPath(second),
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}