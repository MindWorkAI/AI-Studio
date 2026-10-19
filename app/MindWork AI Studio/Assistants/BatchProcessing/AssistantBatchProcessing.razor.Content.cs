using AIStudio.Chat;
using AIStudio.Tools.Media;
using AIStudio.Tools.Rust;

namespace AIStudio.Assistants.BatchProcessing;

public partial class AssistantBatchProcessing
{
    /// <summary>
    /// The image formats we hand to the model. The providers accept these, while other formats,
    /// such as TIFF or HEIC, would end in an error of the provider which tells the user nothing.
    /// </summary>
    private static readonly HashSet<string> SENDABLE_IMAGE_EXTENSIONS = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    /// <summary>
    /// What one file of the batch gives to the model: either text or an image.
    /// </summary>
    private readonly record struct BatchInput(string Text, FileAttachmentImage? Image);

    /// <summary>
    /// Loads a document through the Rust content stream, resolves a persistent
    /// transcript for an audio or video file, or prepares an image for the model.
    /// </summary>
    /// <returns>The input for the model, or <c>null</c> when the file already got its final status.</returns>
    private async Task<BatchInput?> LoadInputContentAsync(BatchProcessingFileResult fileResult, CancellationToken token)
    {
        if (IsImage(fileResult.FilePath))
            return this.LoadImage(fileResult) is { } image ? new BatchInput(string.Empty, image) : null;

        var text = IsTranscribableMedia(fileResult.FilePath)
            ? await this.LoadMediaTranscriptAsync(fileResult, token)
            : await this.LoadDocumentContentAsync(fileResult, token);

        return text is null ? null : new BatchInput(text, null);
    }

    /// <summary>
    /// Checks an image before it goes to the model.
    /// </summary>
    /// <remarks>
    /// The request itself would only skip an image it cannot send, with a message, and ask the model
    /// anyway. The model would then answer about nothing, and the file would count as done, so that
    /// continuing the run would skip it as well.
    /// </remarks>
    private FileAttachmentImage? LoadImage(BatchProcessingFileResult fileResult)
    {
        if (!SENDABLE_IMAGE_EXTENSIONS.Contains(Path.GetExtension(fileResult.FilePath)))
        {
            this.FinishFileResult(fileResult, BatchProcessingFileStatus.FAILED, T("This image format cannot be given to the model. Please convert the image to JPG, PNG, WebP, or GIF."));
            return null;
        }

        long fileSize;
        try
        {
            fileSize = new FileInfo(fileResult.FilePath).Length;
        }
        catch (Exception e)
        {
            this.FinishFileResult(fileResult, BatchProcessingFileStatus.FAILED, string.Format(T("Was not able to read the file: {0}"), e.Message), e);
            return null;
        }

        if (fileSize > IImageSourceExtensions.MAX_IMAGE_SIZE_BYTES)
        {
            this.FinishFileResult(fileResult, BatchProcessingFileStatus.FAILED, T("The image is larger than 10 MB and cannot be given to the model."));
            return null;
        }

        return new FileAttachmentImage(fileResult.FileName, fileResult.FilePath, fileSize);
    }

    private async Task<string?> LoadDocumentContentAsync(BatchProcessingFileResult fileResult, CancellationToken token)
    {
        FileExtractionResult extraction;
        try
        {
            extraction = await this.RustService.ReadArbitraryFileData(fileResult.FilePath, int.MaxValue, token: token);
        }
        catch (Exception e)
        {
            this.FinishFileResult(fileResult, BatchProcessingFileStatus.FAILED, string.Format(T("Was not able to read the file: {0}"), e.Message), e);
            return null;
        }

        return await this.TakeExtractedContentAsync(fileResult, extraction);
    }

    /// <summary>
    /// Hands on what the runtime read for a file, or records why there is nothing to hand on.
    /// </summary>
    private async Task<string?> TakeExtractedContentAsync(BatchProcessingFileResult fileResult, FileExtractionResult extraction)
    {
        //
        // The user stopped the batch run while we were reading this file. That says nothing about
        // the file, so it gets the same status as a cancelled AI request instead of a failure:
        //
        if (extraction.ErrorCode is FileExtractionErrorCode.CANCELLED)
        {
            this.FinishFileResult(fileResult, BatchProcessingFileStatus.CANCELED, T("The batch run was canceled."));
            return null;
        }

        if (!extraction.HasUsableContent)
        {
            this.Logger.LogError("Reading the batch file '{FilePath}' failed: code={ErrorCode}, message='{ErrorMessage}'.", fileResult.FilePath, extraction.ErrorCode, extraction.ErrorMessage);
            this.FinishFileResult(fileResult, BatchProcessingFileStatus.FAILED, extraction.ToUserMessage(fileResult.FileName));
            return null;
        }

        if (extraction.Outcome is FileExtractionOutcome.PARTIAL)
        {
            this.Logger.LogWarning("Parts of the batch file '{FilePath}' could not be read: pages={FailedPages}.", fileResult.FilePath, string.Join(", ", extraction.FailedPages));
            await this.MessageBus.SendWarning(new(Icons.Material.Filled.Description, extraction.ToPartialUserMessage(fileResult.FileName)));
        }

        if (extraction.HasExtensionMismatch)
        {
            this.Logger.LogWarning("The batch file '{FilePath}' is actually a '{DetectedFormat}'.", fileResult.FilePath, extraction.DetectedFormat);
            await this.MessageBus.SendWarning(new(Icons.Material.Filled.RuleFolder, extraction.ToExtensionMismatchUserMessage(fileResult.FileName)));
        }

        if (!string.IsNullOrWhiteSpace(extraction.Content))
            return extraction.Content;

        this.FinishFileResult(fileResult, BatchProcessingFileStatus.FAILED, T("Was not able to extract any text from this file."));
        return null;
    }

    private async Task<string?> LoadMediaTranscriptAsync(BatchProcessingFileResult fileResult, CancellationToken token)
    {
        var provider = this.MediaTranscriptionService.GetUsableProvider();
        var transcript = await PersistentMediaTranscript.ReadOrCreateAsync(MediaSidecarTranscripts.INSTANCE, fileResult.FilePath, provider, this.MediaTranscriptionService.TranscribeAsync, this.Logger, token);
        if (transcript.Status is PersistentMediaTranscriptStatus.CANCELLED)
        {
            this.FinishFileResult(fileResult, BatchProcessingFileStatus.CANCELED, T("The batch run was canceled."));
            return null;
        }

        if (transcript.Status is not PersistentMediaTranscriptStatus.READY)
        {
            this.FinishFileResult(fileResult, BatchProcessingFileStatus.FAILED, transcript.UserMessage);
            return null;
        }

        //
        // Through the runtime like every document, so its filter for prompt injections checks the
        // transcript as well. Like the documents of a run, a filtered transcript is reported right
        // away instead of when the run ends, which may be hours later:
        //
        FileExtractionResult extraction;
        try
        {
            extraction = await PersistentMediaTranscript.ReadTranscriptAsync(this.RustService, transcript, fileResult.FilePath, reportingScope: null, token);
        }
        catch (Exception e)
        {
            this.FinishFileResult(fileResult, BatchProcessingFileStatus.FAILED, string.Format(T("Was not able to read the existing transcript: {0}"), e.Message), e);
            return null;
        }

        return await this.TakeExtractedContentAsync(fileResult, extraction);
    }

    private static bool IsTranscribableMedia(string filePath) => PersistentMediaTranscript.IsMediaPath(filePath);

    private static bool IsImage(string filePath) => FileTypes.IsAllowedPath(filePath, FileTypes.IMAGE);

    private static bool HasReusableTranscript(string mediaFilePath) => MediaSidecarTranscripts.HasReusableTranscript(mediaFilePath);
}