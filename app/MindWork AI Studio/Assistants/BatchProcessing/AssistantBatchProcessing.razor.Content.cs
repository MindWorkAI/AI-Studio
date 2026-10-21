using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Tools.Media;
using AIStudio.Tools.Rust;

namespace AIStudio.Assistants.BatchProcessing;

public partial class AssistantBatchProcessing
{
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
    /// continuing the run would skip it as well. A failed image is processed again when the run is
    /// continued, e.g., with a model that can read images.
    ///
    /// How many images a model accepts needs no check of its own: every file gets a thread of its own,
    /// see CallAIAsync, so each request carries exactly one image, in every round with the model. A
    /// limit of one image is therefore always met, and SupportsImageInput already refuses a limit of zero.
    ///
    /// Unlike the text of documents and transcripts, an image does not pass the prompt injection
    /// filter of the runtime, which reads text only. Images in a chat do not either. Instructions
    /// within an image therefore meet only the safeguards behind the filter: the system prompt tells
    /// the model to ignore them, a thread which read internal content, such as a mailbox, may no
    /// longer send data out, and without free address choice, its default, the Read Web Page tool
    /// refuses an address which appears only in the image.
    /// </remarks>
    private FileAttachmentImage? LoadImage(BatchProcessingFileResult fileResult)
    {
        if (!this.ProviderSettings.SupportsImageInput())
        {
            this.FinishFileResult(fileResult, BatchProcessingFileStatus.FAILED, T("The selected model cannot read images. Select a model with image support to process this image."));
            return null;
        }

        if (!FileTypes.IsAllowedPath(fileResult.FilePath, FileTypes.MODEL_IMAGE))
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

        var maxImageSizeBytes = IImageSourceExtensions.MaxImageSizeBytesFor(this.ProviderSettings.UsedLLMProvider);
        if (fileSize > maxImageSizeBytes)
        {
            this.FinishFileResult(fileResult, BatchProcessingFileStatus.FAILED, string.Format(T("The image is larger than {0} MB, which is the most the selected provider accepts for one image."), (maxImageSizeBytes / 1_000_000d).ToString("0.#", Tools.PluginSystem.I18N.I.Culture)));
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