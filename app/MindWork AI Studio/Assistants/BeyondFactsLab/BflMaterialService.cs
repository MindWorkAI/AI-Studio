using System.Collections.Concurrent;

using AIStudio.Tools.Rust;
using AIStudio.Tools.Security;
using AIStudio.Tools.Services;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Adds documents to the material of a preparation of the BeyondFacts Lab, and removes them.
/// </summary>
/// <remarks>
/// A singleton, so that reading a long document goes on while the person looks at another page: the lab shows the
/// material again when it comes back and listens to MaterialsChanged. The lab reads each document once, through the
/// prompt injection filter of the runtime, and keeps only the filtered text; see BflMaterial.
/// </remarks>
/// <param name="store">The store of the lab.</param>
/// <param name="rustService">The runtime, which reads the documents.</param>
/// <param name="guardService">The prompt injection filter, which tells the person what it removed.</param>
/// <param name="messageBus">The message bus, for what the person needs to know.</param>
/// <param name="logger">The logger.</param>
public sealed class BflMaterialService(BeyondFactsLabStore store, RustService rustService, PromptInjectionGuardService guardService, MessageBus messageBus, ILogger<BflMaterialService> logger)
{
    /// <summary>
    /// How many characters of the text of a document the lab keeps at most.
    /// </summary>
    /// <remarks>
    /// The size of a file says little about its text: slides with many pictures take hundreds of megabytes for a few
    /// pages of text. So the lab limits the text instead, to what a conversation can ever take of one material, see
    /// BflMaterialBudget. It keeps the beginning and leaves out the rest, which never reaches memory either.
    /// </remarks>
    public const int MAX_TEXT_CHARACTERS = BflMaterialBudget.MAX_CHARACTERS;

    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(BflMaterialService).Namespace, nameof(BflMaterialService));

    private static string TA(string fallbackEN) => Tools.PluginSystem.I18N.I.TA(fallbackEN, typeof(BflMaterialService).Namespace, nameof(BflMaterialService));

    /// <summary>
    /// Serializes the imports of each preparation, so that two of them never check the same document at once.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> importLocks = new();

    /// <summary>
    /// The name of the document the lab reads right now, by the ID of its preparation.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, string> documentsInProgress = new();

    /// <summary>
    /// Happens whenever the material of a preparation changed, or an import began or ended.
    /// </summary>
    /// <remarks>
    /// Called on the thread of the import. A page renders through InvokeAsync.
    /// </remarks>
    public event Func<Guid, Task>? MaterialsChanged;

    /// <summary>
    /// Gets the name of the document the lab reads for a preparation right now.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <returns>The name, or null while the lab reads nothing for the preparation.</returns>
    public string? DocumentInProgress(Guid projectId) => this.documentsInProgress.GetValueOrDefault(projectId);

    /// <summary>
    /// Reads documents and adds them to the material of a preparation.
    /// </summary>
    /// <remarks>
    /// One action for the prompt injection filter, so that the person hears once about what it removed from all of
    /// them. A document the preparation has already and one which cannot be read are left out, and the person learns
    /// why; of a long document, the lab keeps the beginning, see MAX_TEXT_CHARACTERS. The method itself does not fail.
    /// </remarks>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="paths">The documents.</param>
    /// <param name="pandocAvailability">Makes sure Pandoc is there for the formats which need it; it belongs to the window of the person.</param>
    public async Task ImportAsync(Guid projectId, IReadOnlyList<string> paths, PandocAvailabilityService pandocAvailability)
    {
        if (paths.Count is 0)
            return;

        var gate = this.importLocks.GetOrAdd(projectId, _ => new(1, 1));
        await gate.WaitAsync();
        try
        {
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
                await messageBus.SendError(new(Icons.Material.Filled.Description, TA("AI Studio could not read the material of this preparation. Please try again.")));
                return;
            }

            await using var promptInjectionScope = guardService.BeginAction();
            foreach (var path in paths)
            {
                this.documentsInProgress[projectId] = BeyondFactsLabStore.MaterialName(path);
                await this.NotifyAsync(projectId);
                await this.ImportDocumentAsync(projectId, path, knownHashes, pandocAvailability, promptInjectionScope);
            }
        }
        finally
        {
            this.documentsInProgress.TryRemove(projectId, out _);
            gate.Release();
            await this.NotifyAsync(projectId);
        }
    }

    /// <summary>
    /// Deletes a material of a preparation. The document itself stays where it is.
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
            await messageBus.SendError(new(Icons.Material.Filled.DeleteForever, TA("AI Studio could not delete the material. Please try again.")));
            return false;
        }

        await this.NotifyAsync(projectId);
        return true;
    }

    /// <summary>
    /// Reads one document and adds it to the material of a preparation.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="path">The document.</param>
    /// <param name="knownHashes">The hashes of the documents the preparation has already; the new one joins them.</param>
    /// <param name="pandocAvailability">Makes sure Pandoc is there for the formats which need it.</param>
    /// <param name="promptInjectionScope">The action the document is read for.</param>
    private async Task ImportDocumentAsync(Guid projectId, string path, HashSet<string> knownHashes, PandocAvailabilityService pandocAvailability, PromptInjectionReportingScope promptInjectionScope)
    {
        var name = BeyondFactsLabStore.MaterialName(path);

        // The page lets the person choose documents only, see AttachDocuments; this keeps out whatever else arrives:
        if (!FileTypes.IsAllowedPath(path, FileTypes.DOCUMENT))
        {
            logger.LogWarning("The BeyondFacts Lab does not read the file '{Path}', since it is no document.", path);
            return;
        }

        long size;
        string hash;
        try
        {
            size = new FileInfo(path).Length;
            hash =await FileContentHash.ComputeAsync(path, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "The BeyondFacts Lab could not open the file '{Path}'.", path);
            await messageBus.SendError(new(Icons.Material.Filled.Description, string.Format(TB("AI Studio could not open the document '{0}'."), name)));
            return;
        }

        if (!knownHashes.Add(hash))
        {
            await messageBus.SendInfo(new(Icons.Material.Filled.Description, string.Format(TB("The material of this preparation contains '{0}' already."), name)));
            return;
        }

        // Failures, missing pages, and a misleading file extension are reported there:
        var extraction = await UserFile.LoadFileData(path, rustService, pandocAvailability, promptInjectionScope, MAX_TEXT_CHARACTERS);
        if (!extraction.HasUsableContent)
        {
            knownHashes.Remove(hash);
            return;
        }

        var material = new BflMaterial
        {
            Name = name,
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
                await messageBus.SendInfo(new(Icons.Material.Filled.Description, string.Format(TB("The material of this preparation contains '{0}' already."), addition.Material.Name)));
            else if (addition.Material.IsShortened)
                await messageBus.SendInfo(new(Icons.Material.Filled.ContentCut, string.Format(TB("The lab keeps only the beginning of '{0}': a conversation never takes more of one material."), addition.Material.Name)));
        }
        catch (FileNotFoundException)
        {
            // The person deleted the preparation while the lab read the document; nothing is left to add it to.
            knownHashes.Remove(hash);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            knownHashes.Remove(hash);
            logger.LogError(exception, "The BeyondFacts Lab could not store the material '{Name}' of the preparation '{ProjectId}'.", name, projectId);
            await messageBus.SendError(new(Icons.Material.Filled.SaveAs, string.Format(TA("AI Studio could not save the material '{0}'. Please try again."), name)));
        }
    }

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