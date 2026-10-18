using AIStudio.Tools.Media;

namespace AIStudio.Assistants.BatchProcessing;

public partial class AssistantBatchProcessing
{
    /// <summary>
    /// Loads a document through the Rust content stream or resolves a persistent
    /// transcript for an audio or video file.
    /// </summary>
    private Task<string?> LoadInputContentAsync(BatchProcessingFileResult fileResult, CancellationToken token)
    {
        return IsTranscribableMedia(fileResult.FilePath)
            ? this.LoadMediaTranscriptAsync(fileResult, token)
            : this.LoadDocumentContentAsync(fileResult, token);
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

    private static bool HasReusableTranscript(string mediaFilePath) => MediaSidecarTranscripts.HasReusableTranscript(mediaFilePath);
}