using System.Text;

using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.Media;
using AIStudio.Tools.Rust;

namespace AIStudio.Assistants.BatchProcessing;

public partial class AssistantBatchProcessing
{
    /// <summary>
    /// What one unit of the batch gives to the model: the text of a file, an image, or the merged
    /// texts and images of a subfolder.
    /// </summary>
    private readonly record struct BatchInput(string Text, IReadOnlyList<FileAttachmentImage> Images);

    /// <summary>
    /// Loads a document through the Rust content stream, resolves a persistent
    /// transcript for an audio or video file, prepares an image for the model,
    /// or merges all files of a subfolder.
    /// </summary>
    /// <returns>The input for the model, or <c>null</c> when the file already got its final status.</returns>
    private async Task<BatchInput?> LoadInputContentAsync(BatchProcessingFileResult fileResult, CancellationToken token)
    {
        if (fileResult.IsFolder)
            return await this.LoadFolderContentAsync(fileResult, token);

        if (IsImage(fileResult.FilePath))
            return await this.LoadImageAsync(fileResult, token) is { } image ? new BatchInput(string.Empty, [image]) : null;

        var text = IsTranscribableMedia(fileResult.FilePath)
            ? await this.LoadMediaTranscriptAsync(fileResult, token)
            : await this.LoadDocumentContentAsync(fileResult, token);

        return text is null ? null : new BatchInput(text, []);
    }

    /// <summary>
    /// Loads every file of a subfolder like a file of its own and merges them into one input.
    /// </summary>
    /// <remarks>
    /// One file which cannot be read lets the whole subfolder fail, naming that file: an answer
    /// about a part of the folder would look complete, and continuing the run would skip it. The
    /// images of the folder go along with the request, so they have to fit the number of images
    /// the model takes at once.
    /// </remarks>
    /// <returns>The merged input, or <c>null</c> when the subfolder already got its final status.</returns>
    private async Task<BatchInput?> LoadFolderContentAsync(BatchProcessingFileResult folderResult, CancellationToken token)
    {
        var numImages = folderResult.MemberFiles.Count(IsImage);
        if (numImages > 1 && this.ProviderSettings.SupportsImageInput() && numImages > this.ImagesTheModelTakesAtOnce())
        {
            this.FinishFileResult(folderResult, BatchProcessingFileStatus.FAILED, string.Format(T("This folder contains {0} images, but the selected model accepts at most {1} images at once."), numImages, this.ImagesTheModelTakesAtOnce()));
            return null;
        }

        var mergedText = new StringBuilder();
        var images = new List<FileAttachmentImage>();
        foreach (var memberFile in folderResult.MemberFiles)
        {
            var pathInFolder = Path.GetRelativePath(folderResult.FilePath, memberFile);
            var member = new BatchProcessingFileResult
            {
                FilePath = memberFile,
                FileName = Path.GetFileName(memberFile),
                RelativePath = Path.Join(folderResult.RelativePath, pathInFolder),
            };

            var memberInput = await this.LoadInputContentAsync(member, token);
            if (memberInput is null)
            {
                var message = member.Status is BatchProcessingFileStatus.CANCELED ? member.Message : $"{pathInFolder}: {member.Message}";
                this.FinishFileResult(folderResult, member.Status, message);
                return null;
            }

            mergedText.AppendLineLF(memberInput.Value.Images.Count > 0
                ? BuildFolderImagePart(pathInFolder)
                : BuildFolderTextPart(pathInFolder, memberInput.Value.Text));

            images.AddRange(memberInput.Value.Images);
        }

        return new BatchInput(mergedText.ToString(), images);
    }

    /// <summary>
    /// How many images the selected model takes in one message.
    /// </summary>
    /// <returns>1 for a model which takes a single image, and int.MaxValue when nobody knows a limit.</returns>
    private int ImagesTheModelTakesAtOnce()
    {
        var profile = this.ProviderSettings.GetModelProfile();
        if (!profile.HasAny(Capability.MULTIPLE_IMAGE_INPUT))
            return 1;

        return profile.Images.MaxInOneMessage ?? int.MaxValue;
    }

    /// <summary>
    /// All files a unit of the batch consists of: the file itself, or the files of the subfolder.
    /// </summary>
    private static IReadOnlyList<string> FilesOf(BatchProcessingFileResult fileResult) => fileResult.IsFolder ? fileResult.MemberFiles : [fileResult.FilePath];

    /// <summary>
    /// Prepares an image for the model before the request is built.
    /// </summary>
    /// <remarks>
    /// The request itself would only skip an image it cannot send, with a message, and ask the model
    /// anyway. The model would then answer about nothing, and the file would count as done, so that
    /// continuing the run would skip it as well. Preparing the image here lets it fail as a file
    /// instead, with the reason, e.g., an image format AI Studio cannot decode. The request then finds
    /// the prepared image in the cache of ModelImageService, so it is not prepared twice. A failed
    /// image is processed again when the run is continued, e.g., with a model that can read images.
    ///
    /// How large an image may be needs no check of its own either: the preparation scales it down
    /// until it fits the limit of the provider, and fails only when even that is not enough.
    ///
    /// How many images a model accepts needs no check here: every file gets a thread of its own, see
    /// CallAIAsync, so the request of a single image carries exactly that one, and SupportsImageInput
    /// already refuses a limit of zero. Only a subfolder sends several, which LoadFolderContentAsync checks.
    ///
    /// Unlike the text of documents and transcripts, an image does not pass the prompt injection
    /// filter of the runtime, which reads text only. Images in a chat do not either. Instructions
    /// within an image therefore meet only the safeguards behind the filter: the system prompt tells
    /// the model to ignore them, a thread which read internal content, such as a mailbox, may no
    /// longer send data out, and without free address choice, its default, the Read Web Page tool
    /// refuses an address which appears only in the image.
    /// </remarks>
    private async Task<FileAttachmentImage?> LoadImageAsync(BatchProcessingFileResult fileResult, CancellationToken token)
    {
        if (!this.ProviderSettings.SupportsImageInput())
        {
            this.FinishFileResult(fileResult, BatchProcessingFileStatus.FAILED, T("The selected model cannot read images. Select a model with image support to process this image."));
            return null;
        }

        PreparedModelImage prepared;
        FileAttachmentImage image;
        try
        {
            image = new FileAttachmentImage(fileResult.FileName, fileResult.FilePath, new FileInfo(fileResult.FilePath).Length);
            prepared = await this.ModelImageService.PrepareAsync(image, this.ProviderSettings, token);
        }
        catch (OperationCanceledException)
        {
            this.FinishFileResult(fileResult, BatchProcessingFileStatus.CANCELED, T("The batch run was canceled."));
            return null;
        }
        catch (Exception e)
        {
            this.FinishFileResult(fileResult, BatchProcessingFileStatus.FAILED, string.Format(T("Was not able to read the file: {0}"), e.Message), e);
            return null;
        }

        if (prepared.IsPrepared)
            return image;

        this.FinishFileResult(fileResult, BatchProcessingFileStatus.FAILED, prepared.ErrorCode.ToUserMessage(fileResult.FileName));
        return null;
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
            await this.MessageBus.SendWarning(new(AppIcons.DOCUMENT, extraction.ToPartialUserMessage(fileResult.FileName)));
        }

        if (extraction.HasExtensionMismatch)
        {
            this.Logger.LogWarning("The batch file '{FilePath}' is actually a '{DetectedFormat}'.", fileResult.FilePath, extraction.DetectedFormat);
            await this.MessageBus.SendWarning(new(AppIcons.FILE_TYPE_MISMATCH, extraction.ToExtensionMismatchUserMessage(fileResult.FileName)));
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