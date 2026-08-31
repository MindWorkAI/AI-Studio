using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

using AIStudio.Settings;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines <c>VisualBriefingStore</c> for the visual briefing feature.
/// </summary>
public sealed partial class VisualBriefingStore(
    VisualBriefingArtifactService artifactService,
    ILogger<VisualBriefingStore> logger,
    VisualBriefingStorageOptions? storageOptions = null)
{
    /// <summary>Defines the project manifest filename.</summary>
    private const string MANIFEST_FILE_NAME = "manifest.json";
    
    /// <summary>Defines the last-selection filename.</summary>
    private const string SELECTION_FILE_NAME = "selection.json";
    
    /// <summary>Defines the intermediate-artifact directory.</summary>
    private const string ARTIFACTS_DIRECTORY_NAME = "artifacts";
    
    /// <summary>Defines the evidence-artifact directory.</summary>
    private const string EVIDENCE_ARTIFACTS_DIRECTORY_NAME = "evidence";
    
    /// <summary>Defines the plan-artifact directory.</summary>
    private const string PLAN_ARTIFACTS_DIRECTORY_NAME = "plan";
    
    /// <summary>Defines the content-artifact directory.</summary>
    private const string CONTENT_ARTIFACTS_DIRECTORY_NAME = "content";
    
    /// <summary>Defines the presentation-artifact directory.</summary>
    private const string PRESENTATION_ARTIFACTS_DIRECTORY_NAME = "presentation";
    
    /// <summary>Defines the build-history directory.</summary>
    private const string BUILDS_DIRECTORY_NAME = "builds";
    
    /// <summary>Defines the immutable-version directory.</summary>
    private const string VERSIONS_DIRECTORY_NAME = "versions";
    
    /// <summary>Defines the persistent-transcript directory.</summary>
    private const string TRANSCRIPTS_DIRECTORY_NAME = "transcripts";
    
    /// <summary>Gets the shared persistence JSON options.</summary>
    private static readonly JsonSerializerOptions JSON_OPTIONS = VisualBriefingJson.Persistence;
    
    /// <summary>Stores per-project process locks.</summary>
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> briefingLocks = [];
    
    /// <summary>
    /// Serializes store initialization.
    /// </summary>
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    
    /// <summary>
    /// Serializes last-selection writes.
    /// </summary>
    private readonly SemaphoreSlim selectionLock = new(1, 1);
    
    /// <summary>Tracks whether initialization and reconciliation completed.</summary>
    private bool initialized;

    /// <summary>
    /// Defines <c>RootDirectory</c> for the visual briefing feature.
    /// </summary>
    private string RootDirectory => Path.Combine(
        storageOptions?.DataDirectory ??
        SettingsManager.DataDirectory ??
        throw new InvalidOperationException("The AI Studio data directory is not initialized."),
        "visualBriefings");

    /// <summary>
    /// Reads a JSON file while treating malformed persisted diagnostics as unavailable.
    /// </summary>
    /// <typeparam name="T">The JSON model type.</typeparam>
    /// <param name="path">The file path.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The parsed value, or <see langword="null"/>.</returns>
    private static async Task<T?> ReadJsonAsync<T>(string path, CancellationToken token)
        where T : class
    {
        if (!File.Exists(path))
            return null;
        
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, true);
            return await JsonSerializer.DeserializeAsync<T>(stream, JSON_OPTIONS, token);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes an immutable intermediate artifact without replacing an existing file.
    /// </summary>
    /// <param name="path">The artifact path.</param>
    /// <param name="json">The serialized artifact.</param>
    /// <param name="token">The cancellation token.</param>
    private static async Task WriteImmutableArtifactAsync(
        string path,
        string json,
        CancellationToken token)
    {
        await WriteTextAtomicAsync(path, json, token, overwrite: false);
    }

    /// <summary>
    /// Defines <c>WriteTextAtomicAsync</c> for the visual briefing feature.
    /// </summary>
    private static async Task WriteTextAtomicAsync(
        string targetPath,
        string content,
        CancellationToken token,
        bool overwrite = true)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var temporaryPath = $"{targetPath}.tmp-{Guid.NewGuid():N}";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, content, new UTF8Encoding(false), token);
            await using (var stream = new FileStream(temporaryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4_096, true))
                await stream.FlushAsync(token);
            File.Move(temporaryPath, targetPath, overwrite);
        }
        finally
        {
            TryDeleteFile(temporaryPath);
        }
    }

    /// <summary>
    /// Defines <c>TryDeleteFile</c> for the visual briefing feature.
    /// </summary>
    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Startup and rollback cleanup are best effort.
        }
    }

    /// <summary>
    /// Defines <c>PathComparer</c> for the visual briefing feature.
    /// </summary>
    private static StringComparer PathComparer() => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    /// <summary>
    /// Defines <c>T</c> for the visual briefing feature.
    /// </summary>
    private static bool IsNull<T>(T? value) => value is null;

    /// <summary>
    /// Defines <c>GetLock</c> for the visual briefing feature.
    /// </summary>
    private SemaphoreSlim GetLock(Guid briefingId) => this.briefingLocks.GetOrAdd(briefingId, _ => new(1, 1));

    /// <summary>
    /// Drops the lock of a briefing which does not exist anymore.
    /// </summary>
    /// <remarks>
    /// Otherwise, this dictionary keeps one entry per briefing the app ever touched. We do not
    /// dispose the semaphore: another operation might still wait on it, and disposing it under
    /// their feet would turn a deleted briefing into an exception somewhere else.
    /// </remarks>
    private void ForgetLock(Guid briefingId) => this.briefingLocks.TryRemove(briefingId, out _);

    /// <summary>
    /// Defines <c>BriefingDirectory</c> for the visual briefing feature.
    /// </summary>
    private string BriefingDirectory(Guid briefingId) => Path.Combine(this.RootDirectory, briefingId.ToString("D"));

    /// <summary>
    /// Defines <c>ManifestPath</c> for the visual briefing feature.
    /// </summary>
    private string ManifestPath(Guid briefingId) => Path.Combine(this.BriefingDirectory(briefingId), MANIFEST_FILE_NAME);

    /// <summary>
    /// Defines <c>SelectionPath</c> for the visual briefing feature.
    /// </summary>
    private string SelectionPath() => Path.Combine(this.RootDirectory, SELECTION_FILE_NAME);

    /// <summary>
    /// Defines <c>VersionsDirectory</c> for the visual briefing feature.
    /// </summary>
    private string VersionsDirectory(Guid briefingId) => Path.Combine(this.BriefingDirectory(briefingId), VERSIONS_DIRECTORY_NAME);

    /// <summary>
    /// Defines <c>TranscriptsDirectory</c> for the visual briefing feature.
    /// </summary>
    private string TranscriptsDirectory(Guid briefingId) => Path.Combine(this.BriefingDirectory(briefingId), TRANSCRIPTS_DIRECTORY_NAME);

    /// <summary>
    /// Defines <c>ArtifactsDirectory</c> for the visual briefing feature.
    /// </summary>
    private string ArtifactsDirectory(Guid briefingId) => Path.Combine(this.BriefingDirectory(briefingId), ARTIFACTS_DIRECTORY_NAME);

    /// <summary>
    /// Defines <c>EvidenceArtifactsDirectory</c> for the visual briefing feature.
    /// </summary>
    private string EvidenceArtifactsDirectory(Guid briefingId) =>
        Path.Combine(this.ArtifactsDirectory(briefingId), EVIDENCE_ARTIFACTS_DIRECTORY_NAME);

    /// <summary>
    /// Defines <c>PlanArtifactsDirectory</c> for the visual briefing feature.
    /// </summary>
    private string PlanArtifactsDirectory(Guid briefingId) =>
        Path.Combine(this.ArtifactsDirectory(briefingId), PLAN_ARTIFACTS_DIRECTORY_NAME);

    /// <summary>
    /// Defines <c>ContentArtifactsDirectory</c> for the visual briefing feature.
    /// </summary>
    private string ContentArtifactsDirectory(Guid briefingId) =>
        Path.Combine(this.ArtifactsDirectory(briefingId), CONTENT_ARTIFACTS_DIRECTORY_NAME);

    /// <summary>
    /// Defines <c>PresentationArtifactsDirectory</c> for the visual briefing feature.
    /// </summary>
    private string PresentationArtifactsDirectory(Guid briefingId) =>
        Path.Combine(this.ArtifactsDirectory(briefingId), PRESENTATION_ARTIFACTS_DIRECTORY_NAME);

    /// <summary>
    /// Defines <c>BuildsDirectory</c> for the visual briefing feature.
    /// </summary>
    private string BuildsDirectory(Guid briefingId) => Path.Combine(this.BriefingDirectory(briefingId), BUILDS_DIRECTORY_NAME);

    /// <summary>
    /// Defines <c>EvidenceArtifactPath</c> for the visual briefing feature.
    /// </summary>
    private string EvidenceArtifactPath(Guid briefingId, Guid artifactId) =>
        Path.Combine(this.EvidenceArtifactsDirectory(briefingId), $"{artifactId:D}.json");

    /// <summary>
    /// Defines <c>PlanArtifactPath</c> for the visual briefing feature.
    /// </summary>
    private string PlanArtifactPath(Guid briefingId, Guid artifactId) =>
        Path.Combine(this.PlanArtifactsDirectory(briefingId), $"{artifactId:D}.json");

    /// <summary>
    /// Defines <c>ContentArtifactPath</c> for the visual briefing feature.
    /// </summary>
    private string ContentArtifactPath(Guid briefingId, Guid artifactId) =>
        Path.Combine(this.ContentArtifactsDirectory(briefingId), $"{artifactId:D}.json");

    /// <summary>
    /// Defines <c>PresentationArtifactPath</c> for the visual briefing feature.
    /// </summary>
    private string PresentationArtifactPath(Guid briefingId, Guid artifactId) =>
        Path.Combine(this.PresentationArtifactsDirectory(briefingId), $"{artifactId:D}.json");

    /// <summary>
    /// Defines <c>BuildPath</c> for the visual briefing feature.
    /// </summary>
    private string BuildPath(Guid briefingId, Guid buildId) =>
        Path.Combine(this.BuildsDirectory(briefingId), $"{buildId:D}.json");

    /// <summary>
    /// Defines <c>TranscriptPath</c> for the visual briefing feature.
    /// </summary>
    private string TranscriptPath(Guid briefingId, Guid sourceId) => Path.Combine(this.TranscriptsDirectory(briefingId), $"{sourceId:D}.md");

    /// <summary>
    /// Defines <c>VersionPath</c> for the visual briefing feature.
    /// </summary>
    private string VersionPath(Guid briefingId, VisualBriefingVersion version) => Path.Combine(this.VersionsDirectory(briefingId), version.FileName);
}