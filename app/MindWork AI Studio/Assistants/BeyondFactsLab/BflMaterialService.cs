using System.Collections.Concurrent;

using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.AssistantSessions;
using AIStudio.Tools.Media;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Security;
using AIStudio.Tools.Services;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Adds documents, recordings, and images to the material of a preparation of the BeyondFacts Lab, and removes them.
/// </summary>
/// <remarks>
/// A singleton, so that reading a long document or transcribing a recording goes on while the person looks at another
/// page: the lab shows the material again when it comes back and listens to MaterialsChanged. The lab reads each file
/// once. Of a document or a recording, it keeps only the text which passed the prompt injection filter of the runtime;
/// of an image, a copy prepared for models; see BflMaterial.
/// </remarks>
/// <param name="store">The store of the lab.</param>
/// <param name="rustService">The runtime, which reads the documents and transcripts and prepares the images.</param>
/// <param name="guardService">The prompt injection filter, which tells the person what it removed.</param>
/// <param name="messageBus">The message bus, for what the person needs to know.</param>
/// <param name="settingsManager">The settings, which name the transcription provider and the confidence the lab demands.</param>
/// <param name="mediaTranscriptionService">The transcription of recordings.</param>
/// <param name="sessionService">The registry of running assistant sessions, which shows that the lab is at work.</param>
/// <param name="logger">The logger.</param>
public sealed class BflMaterialService(BeyondFactsLabStore store, RustService rustService, PromptInjectionGuardService guardService, MessageBus messageBus, SettingsManager settingsManager, MediaTranscriptionService mediaTranscriptionService, AssistantSessionService sessionService, ILogger<BflMaterialService> logger)
{
    /// <summary>
    /// How many characters of the text of a document or a transcript the lab keeps at most.
    /// </summary>
    /// <remarks>
    /// The size of a file says little about its text: slides with many pictures take hundreds of megabytes for a few
    /// pages of text. So the lab limits the text instead, to what a conversation can ever take of one material, see
    /// BflMaterialBudget. It keeps the beginning and leaves out the rest, which never reaches memory either. The
    /// transcript of a recording of three hours takes less than half of it.
    /// </remarks>
    public const int MAX_TEXT_CHARACTERS = BflMaterialBudget.MAX_CHARACTERS;

    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(BflMaterialService).Namespace, nameof(BflMaterialService));

    private static string TA(string fallbackEN) => Tools.PluginSystem.I18N.I.TA(fallbackEN, typeof(BflMaterialService).Namespace, nameof(BflMaterialService));

    /// <summary>
    /// Serializes the imports of each preparation, so that two of them never check the same file at once.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> importLocks = new();

    /// <summary>
    /// The file the lab reads right now, by the ID of its preparation.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, BflMaterialInProgress> materialsInProgress = new();

    /// <summary>
    /// Happens whenever the material of a preparation changed, or an import began or ended.
    /// </summary>
    /// <remarks>
    /// Called on the thread of the import. A page renders through InvokeAsync.
    /// </remarks>
    public event Func<Guid, Task>? MaterialsChanged;

    /// <summary>
    /// Gets the file the lab reads for a preparation right now.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <returns>The file, or null while the lab reads nothing for the preparation.</returns>
    public BflMaterialInProgress? MaterialInProgress(Guid projectId) => this.materialsInProgress.GetValueOrDefault(projectId);

    /// <summary>
    /// Gets the confidence a transcription provider needs for the recordings of the lab: the same as the providers of
    /// its conversations, since a recording is as confidential as a paper.
    /// </summary>
    public ConfidenceLevel MinimumTranscriptionConfidence => settingsManager.GetMinimumConfidenceLevel(Tools.Components.BEYOND_FACTS_LAB_ASSISTANT);

    /// <summary>
    /// Gets what the person needs to know when no transcription provider may hear the recordings of the lab.
    /// </summary>
    public string NoTranscriptionProviderMessage => string.Format(TA("Recordings need a transcription provider with at least the confidence '{0}'. Please select one in the transcription settings of AI Studio."), this.MinimumTranscriptionConfidence.Name);

    /// <summary>
    /// Gets the transcription provider which may hear the recordings of the lab.
    /// </summary>
    /// <returns>The provider of AI Studio, or null when there is none or it falls short of MinimumTranscriptionConfidence.</returns>
    public TranscriptionProvider? UsableTranscriptionProvider()
    {
        var provider = mediaTranscriptionService.GetUsableProvider();
        return provider is not null && settingsManager.IsProviderUsable(provider.UsedLLMProvider, this.MinimumTranscriptionConfidence) ? provider : null;
    }

    /// <summary>
    /// Reads documents, recordings, and images and adds them to the material of a preparation.
    /// </summary>
    /// <remarks>
    /// One action for the prompt injection filter, so that the person hears once about what it removed from all of
    /// them. A file the preparation has already and one which cannot be read are left out, and the person learns why;
    /// of a long text, the lab keeps the beginning, see MAX_TEXT_CHARACTERS. The method itself does not fail.
    /// </remarks>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="paths">The documents, recordings, and images.</param>
    /// <param name="pandocAvailability">Makes sure Pandoc is there for the formats which need it; it belongs to the window of the person.</param>
    public async Task ImportAsync(Guid projectId, IReadOnlyList<string> paths, PandocAvailabilityService pandocAvailability)
    {
        if (paths.Count is 0)
            return;

        var gate = this.importLocks.GetOrAdd(projectId, _ => new(1, 1));
        await gate.WaitAsync();
        var sessionKey = ImportSessionKey(projectId);
        var sessionId = Guid.Empty;
        try
        {
            // The navigation and the tile of the lab show that the lab is at work, as while it answers in a conversation.
            // Nothing cancels an import, so the source stays unused; the registry disposes of it when the import ends:
            sessionId = (await sessionService.TryBeginAsync(sessionKey, Tools.Components.BEYOND_FACTS_LAB_ASSISTANT.Name, new CancellationTokenSource(), null, new(StringComparer.Ordinal))).SessionId;

            HashSet<string> knownHashes;
            try
            {
                knownHashes = (await store.ListMaterialsAsync(projectId))
                    .Select(entry => entry.Material?.SourceHash)
                    .OfType<string>()
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogError(exception, "Could not list the material of the preparation '{ProjectId}' of the BeyondFacts Lab.", projectId);
                await messageBus.SendError(new(AppIcons.DOCUMENT, TA("AI Studio could not read the material of this preparation. Please try again.")));
                return;
            }

            await using var promptInjectionScope = guardService.BeginAction();
            foreach (var path in paths)
            {
                // The page lets the person choose documents, recordings, and images only, see AttachDocuments; this keeps out whatever else arrives:
                if (KindOf(path) is not { } kind)
                {
                    logger.LogWarning("The BeyondFacts Lab does not read the file '{Path}', since it is no document, recording, or image for models.", path);
                    continue;
                }

                this.materialsInProgress[projectId] = new(BeyondFactsLabStore.MaterialName(path), kind);
                await this.NotifyAsync(projectId);
                await this.ImportFileAsync(projectId, path, kind, knownHashes, pandocAvailability, promptInjectionScope);
            }
        }
        finally
        {
            this.materialsInProgress.TryRemove(projectId, out _);
            if (sessionId != Guid.Empty)
            {
                await sessionService.CompleteAsync(sessionKey, sessionId, AssistantSessionStatus.COMPLETED, string.Empty, null, new(StringComparer.Ordinal));

                // The material shows what came of the import; the registry only showed that the lab was at work:
                await sessionService.ClearAsync(sessionKey);
            }

            gate.Release();
            await this.NotifyAsync(projectId);
        }
    }

    /// <summary>
    /// Deletes a material of a preparation. The file itself stays where it is.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="materialId">The ID of the material.</param>
    /// <returns>False when the material could not be deleted; the person learned why.</returns>
    public async Task<bool> DeleteAsync(Guid projectId, Guid materialId)
    {
        try
        {
            await store.DeleteMaterialAsync(projectId, materialId);
        }
        catch (FileNotFoundException)
        {
            // The person deleted the preparation meanwhile, with all its material.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            logger.LogError(exception, "Could not delete the material '{MaterialId}' of the preparation '{ProjectId}' of the BeyondFacts Lab.", materialId, projectId);
            await messageBus.SendError(new(AppIcons.DELETE, TA("AI Studio could not delete the material. Please try again.")));
            return false;
        }

        await this.NotifyAsync(projectId);
        return true;
    }

    /// <summary>
    /// Gets what a file becomes as material.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>The kind of material, or null for a file the lab does not read.</returns>
    private static BflMaterialKind? KindOf(string path)
    {
        if (FileTypes.IsAllowedPath(path, FileTypes.DOCUMENT))
            return BflMaterialKind.DOCUMENT;

        if (PersistentMediaTranscript.IsMediaPath(path))
            return BflMaterialKind.RECORDING;

        // Only the formats the runtime prepares for models, not HEIC, AVIF, or SVG:
        if (FileTypes.IsAllowedPath(path, FileTypes.MODEL_IMAGE))
            return BflMaterialKind.IMAGE;

        return null;
    }

    /// <summary>
    /// Reads one document, recording, or image and adds it to the material of a preparation.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="path">The file.</param>
    /// <param name="kind">What the file becomes.</param>
    /// <param name="knownHashes">The hashes of the files the preparation has already; the new one joins them.</param>
    /// <param name="pandocAvailability">Makes sure Pandoc is there for the formats which need it.</param>
    /// <param name="promptInjectionScope">The action the file is read for.</param>
    private async Task ImportFileAsync(Guid projectId, string path, BflMaterialKind kind, HashSet<string> knownHashes, PandocAvailabilityService pandocAvailability, PromptInjectionReportingScope promptInjectionScope)
    {
        var name = BeyondFactsLabStore.MaterialName(path);

        long size;
        string hash;
        try
        {
            size = new FileInfo(path).Length;
            hash = await FileContentHash.ComputeAsync(path, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "The BeyondFacts Lab could not open the file '{Path}'.", path);
            await messageBus.SendError(new(AppIcons.DOCUMENT, string.Format(TB("AI Studio could not open the file '{0}'."), name)));
            return;
        }

        // Before anything is read, and above all before a recording is paid for:
        if (!knownHashes.Add(hash))
        {
            await messageBus.SendInfo(new(AppIcons.DOCUMENT, string.Format(TB("The material of this preparation contains '{0}' already."), name)));
            return;
        }

        if (kind is BflMaterialKind.IMAGE)
        {
            if (!await this.AddImageAsync(projectId, path, name, size, hash))
                knownHashes.Remove(hash);

            return;
        }

        // Failures, missing pages, and a misleading file extension are reported there:
        var extraction = kind is BflMaterialKind.RECORDING
            ? await this.ReadRecordingAsync(path, name, promptInjectionScope)
            : await UserFile.LoadFileData(path, rustService, pandocAvailability, promptInjectionScope, MAX_TEXT_CHARACTERS);

        if (!extraction.HasUsableContent)
        {
            knownHashes.Remove(hash);
            return;
        }

        var material = new BflMaterial
        {
            Name = name,
            Kind = kind,
            SourceBytes = size,
            SourceHash = hash,
            IsIncomplete = extraction.Outcome is FileExtractionOutcome.PARTIAL,
            IsShortened = extraction.IsShortened,
            FailedPages = [.. extraction.FailedPages],
            FilteredPassages = extraction.PromptInjectionRedactedCount,
        };

        try
        {
            var addition = await store.AddMaterialAsync(projectId, material, extraction.Content);
            if (addition.IsDuplicate)
                await messageBus.SendInfo(new(AppIcons.DOCUMENT, string.Format(TB("The material of this preparation contains '{0}' already."), addition.Material.Name)));
            else if (addition.Material.IsShortened)
                await messageBus.SendInfo(new(AppIcons.CONTENT_SHORTENED, string.Format(TB("The lab keeps only the beginning of '{0}': a conversation never takes more of one material."), addition.Material.Name)));
        }
        catch (FileNotFoundException)
        {
            // The person deleted the preparation while the lab read the file; nothing is left to add it to.
            knownHashes.Remove(hash);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            knownHashes.Remove(hash);
            logger.LogError(exception, "The BeyondFacts Lab could not store the material '{Name}' of the preparation '{ProjectId}'.", name, projectId);
            await messageBus.SendError(new(AppIcons.SAVE_FAILED, string.Format(TA("AI Studio could not save the material '{0}'. Please try again."), name)));
        }
    }

    /// <summary>
    /// Prepares an image for models and adds the copy to the material of a preparation.
    /// </summary>
    /// <remarks>
    /// The runtime turns the image upright, scales it down to the edge models take, and encodes it anew as JPEG or PNG,
    /// which leaves out its metadata, such as the place where a photo was taken. The copy is as large as any provider
    /// takes; a provider which takes less gets a smaller one when the lab sends it, see ModelImageService. Like an image
    /// in a chat, it does not pass the prompt injection filter, which reads text only; see BflConversationService.WithImages.
    /// </remarks>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="path">The image.</param>
    /// <param name="name">The name of the image as the person knows it.</param>
    /// <param name="size">The size of the image in bytes.</param>
    /// <param name="hash">The hash of the image.</param>
    /// <returns>True when the preparation has the image now, also as a material it had already.</returns>
    private async Task<bool> AddImageAsync(Guid projectId, string path, string name, long size, string hash)
    {
        var prepared = await rustService.PrepareImageForModelAsync(path, IImageSourceExtensions.MAX_IMAGE_SIZE_BYTES);
        if (!prepared.IsPrepared)
        {
            logger.LogWarning("The BeyondFacts Lab could not prepare the image '{Path}': {ErrorCode}.", path, prepared.ErrorCode);
            await messageBus.SendError(new(AppIcons.IMAGE, prepared.ErrorCode.ToUserMessage(name)));
            return false;
        }

        var material = new BflMaterial
        {
            Name = name,
            Kind = BflMaterialKind.IMAGE,
            Image = new()
            {
                Extension = prepared.MimeType is "image/png" ? "png" : "jpg",
                Width = (int)prepared.Width,
                Height = (int)prepared.Height,
            },
            SourceBytes = size,
            SourceHash = hash,
        };

        try
        {
            var addition = await store.AddImageMaterialAsync(projectId, material, Convert.FromBase64String(prepared.Base64));
            if (addition.IsDuplicate)
                await messageBus.SendInfo(new(AppIcons.IMAGE, string.Format(TB("The material of this preparation contains '{0}' already."), addition.Material.Name)));

            return true;
        }
        catch (FileNotFoundException)
        {
            // The person deleted the preparation while the lab prepared the image; nothing is left to add it to.
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or FormatException or ArgumentException)
        {
            logger.LogError(exception, "The BeyondFacts Lab could not store the image '{Name}' of the preparation '{ProjectId}'.", name, projectId);
            await messageBus.SendError(new(AppIcons.SAVE_FAILED, string.Format(TA("AI Studio could not save the material '{0}'. Please try again."), name)));
            return false;
        }
    }

    /// <summary>
    /// Transcribes a recording and reads its transcript through the prompt injection filter.
    /// </summary>
    /// <remarks>
    /// The transcription provider is decided here again and passed on as it is, so that a change of the settings since
    /// the person confirmed cannot send the recording to a provider the lab does not trust. The transcript waits in a
    /// BflTranscriptLocation, which is gone afterward: the material keeps the filtered text only.
    /// </remarks>
    /// <param name="path">The recording.</param>
    /// <param name="name">The name of the recording as the person knows it.</param>
    /// <param name="promptInjectionScope">The action the recording is read for.</param>
    /// <returns>The filtered text of the transcript; a failure the person learned about already.</returns>
    private async Task<FileExtractionResult> ReadRecordingAsync(string path, string name, PromptInjectionReportingScope promptInjectionScope)
    {
        if (this.UsableTranscriptionProvider() is not { } provider)
        {
            await messageBus.SendWarning(new(AppIcons.VOICE_TRANSCRIPTION, this.NoTranscriptionProviderMessage));
            return FileExtractionResult.Failed(FileExtractionErrorCode.INVALID_REQUEST, "No transcription provider may hear the recordings of the BeyondFacts Lab.");
        }

        if (SettingsManager.DataDirectory is not { } dataDirectory)
            return FileExtractionResult.Failed(FileExtractionErrorCode.INTERNAL, "The data directory of AI Studio is not known yet.");

        var location = new BflTranscriptLocation(dataDirectory);
        try
        {
            var transcript = await PersistentMediaTranscript.ReadOrCreateAsync(location, path, provider, mediaTranscriptionService.TranscribeAsync, logger, CancellationToken.None);
            if (transcript.Status is not PersistentMediaTranscriptStatus.READY)
            {
                logger.LogWarning("The BeyondFacts Lab could not transcribe the recording '{Path}': {Status}.", path, transcript.Status);
                await messageBus.SendError(new(AppIcons.VOICE_TRANSCRIPTION, string.Format(TB("AI Studio could not transcribe the recording '{0}': {1}"), name, transcript.UserMessage)));
                return FileExtractionResult.Failed(FileExtractionErrorCode.INTERNAL, transcript.UserMessage);
            }

            var extraction = await PersistentMediaTranscript.ReadTranscriptAsync(rustService, transcript, path, promptInjectionScope, CancellationToken.None, MAX_TEXT_CHARACTERS);
            if (!extraction.HasUsableContent)
            {
                logger.LogError("The BeyondFacts Lab could not read the transcript of the recording '{Path}': code={ErrorCode}, message='{ErrorMessage}'.", path, extraction.ErrorCode, extraction.ErrorMessage);
                await messageBus.SendError(new(AppIcons.VOICE_TRANSCRIPTION, extraction.ToUserMessage(name)));
            }

            return extraction;
        }
        finally
        {
            if (!location.TryDelete())
                logger.LogWarning("Could not delete the transcript in '{Folder}'; the next start of AI Studio removes it.", location.FolderPath);
        }
    }

    /// <summary>
    /// Gets the slot in the registry of assistant sessions which shows that the lab reads material for a preparation.
    /// </summary>
    /// <remarks>
    /// The conversations of the steps have slots of their own, keyed by the folders of their steps, see
    /// BflStepThreadLocation.Key; no step folder is called "materials".
    /// </remarks>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <returns>The key of the slot.</returns>
    internal static AssistantSessionKey ImportSessionKey(Guid projectId) => new(Tools.Components.BEYOND_FACTS_LAB_ASSISTANT, $"{projectId:D}/materials");

    /// <summary>
    /// Tells every page which listens that the material of a preparation changed.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    private async Task NotifyAsync(Guid projectId)
    {
        if (this.MaterialsChanged is not { } handlers)
            return;

        foreach (var handler in handlers.GetInvocationList().Cast<Func<Guid, Task>>())
        {
            try
            {
                await handler(projectId);
            }
            catch (Exception exception)
            {
                // A page which went away meanwhile must not stop the import:
                logger.LogDebug(exception, "A page of the BeyondFacts Lab could not show a change of the material.");
            }
        }
    }
}