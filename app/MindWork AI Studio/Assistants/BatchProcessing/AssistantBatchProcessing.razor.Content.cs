using System.Text;

using AIStudio.Tools.Media;
using AIStudio.Tools.Rust;

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
            : this.LoadDocumentContentAsync(fileResult);
    }

    private async Task<string?> LoadDocumentContentAsync(BatchProcessingFileResult fileResult)
    {
        FileExtractionResult extraction;
        try
        {
            extraction = await this.RustService.ReadArbitraryFileData(fileResult.FilePath, int.MaxValue);
        }
        catch (Exception e)
        {
            this.FinishFileResult(fileResult, BatchProcessingFileStatus.FAILED, string.Format(T("Was not able to read the file: {0}"), e.Message), e);
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
        var transcriptFilePath = GetTranscriptFilePath(fileResult.FilePath);
        if (File.Exists(transcriptFilePath))
        {
            try
            {
                var existingTranscript = await File.ReadAllTextAsync(transcriptFilePath, token);
                if (!string.IsNullOrWhiteSpace(existingTranscript))
                {
                    this.Logger.LogInformation("Reusing the existing batch transcript '{TranscriptFilePath}' for media file '{MediaFilePath}'.", transcriptFilePath, fileResult.FilePath);
                    return existingTranscript;
                }

                this.Logger.LogWarning("The existing batch transcript '{TranscriptFilePath}' for media file '{MediaFilePath}' is empty and will be replaced.", transcriptFilePath, fileResult.FilePath);
            }
            catch (OperationCanceledException)
            {
                this.FinishFileResult(fileResult, BatchProcessingFileStatus.CANCELED, T("The batch run was canceled."));
                return null;
            }
            catch (Exception e)
            {
                this.FinishFileResult(fileResult, BatchProcessingFileStatus.FAILED, string.Format(T("Was not able to read the existing transcript: {0}"), e.Message), e);
                return null;
            }
        }

        if (!this.MediaTranscriptionService.HasUsableTranscriptionProvider)
        {
            this.FinishFileResult(fileResult, BatchProcessingFileStatus.FAILED, T("No usable transcription provider is configured."));
            return null;
        }

        var transcription = await this.MediaTranscriptionService.TranscribeAsync(fileResult.FilePath, token);
        if (transcription.Status is MediaTranscriptionResultStatus.CANCELLED)
        {
            this.FinishFileResult(fileResult, BatchProcessingFileStatus.CANCELED, T("The batch run was canceled."));
            return null;
        }

        if (transcription.Status is not MediaTranscriptionResultStatus.SUCCEEDED)
        {
            this.FinishFileResult(fileResult, BatchProcessingFileStatus.FAILED, transcription.UserMessage);
            return null;
        }

        if (string.IsNullOrWhiteSpace(transcription.Text))
        {
            this.FinishFileResult(fileResult, BatchProcessingFileStatus.FAILED, T("The transcription provider returned an empty transcript."));
            return null;
        }

        return await this.StoreMediaTranscriptAsync(fileResult, transcriptFilePath, transcription.Text);
    }

    private async Task<string?> StoreMediaTranscriptAsync(BatchProcessingFileResult fileResult, string transcriptFilePath, string transcript)
    {
        var tempFilePath = transcriptFilePath + ".tmp";
        try
        {
            // Complete the small persistence step even if cancellation arrived
            // after transcription, so the expensive provider result can be
            // reused when the interrupted batch is continued.
            await File.WriteAllTextAsync(tempFilePath, transcript, new UTF8Encoding(false), CancellationToken.None);
            File.Move(tempFilePath, transcriptFilePath, true);
            this.Logger.LogInformation("Stored the batch transcript '{TranscriptFilePath}' next to media file '{MediaFilePath}'.", transcriptFilePath, fileResult.FilePath);
            return transcript;
        }
        catch (Exception e)
        {
            this.FinishFileResult(fileResult, BatchProcessingFileStatus.FAILED, string.Format(T("Was not able to store the transcript next to the media file: {0}"), e.Message), e);
            return null;
        }
        finally
        {
            try
            {
                if (File.Exists(tempFilePath))
                    File.Delete(tempFilePath);
            }
            catch (Exception e)
            {
                this.Logger.LogWarning(e, "Was not able to remove the temporary batch transcript '{TempFilePath}'.", tempFilePath);
            }
        }
    }

    private static bool IsTranscribableMedia(string filePath) => FileTypes.IsAllowedPath(filePath, FileTypes.AUDIO, FileTypes.VIDEO);

    private static string GetTranscriptFilePath(string mediaFilePath) => mediaFilePath + TRANSCRIPT_FILE_SUFFIX;

    private static bool HasReusableTranscript(string mediaFilePath)
    {
        var transcriptFilePath = GetTranscriptFilePath(mediaFilePath);
        try
        {
            return File.Exists(transcriptFilePath) && new FileInfo(transcriptFilePath).Length > 0;
        }
        catch
        {
            // The concrete read error is reported when the affected file is
            // processed. Here we only decide whether a provider is required.
            return File.Exists(transcriptFilePath);
        }
    }
}