using System.Text;
using System.Text.Json;

namespace AIStudio.Assistants.BeyondFactsLab;

public sealed partial class BeyondFactsLabStore
{
    /// <summary>
    /// The folder of a preparation which holds its materials.
    /// </summary>
    private const string MATERIALS_DIRECTORY_NAME = "materials";

    /// <summary>
    /// The extension of the file which describes a material.
    /// </summary>
    private const string MATERIAL_FILE_EXTENSION = ".json";

    /// <summary>
    /// The extension of the file which holds the text of a material.
    /// </summary>
    private const string MATERIAL_TEXT_FILE_EXTENSION = ".md";

    /// <summary>
    /// How many characters a read of the text of a material takes at once.
    /// </summary>
    private const int MATERIAL_TEXT_READ_BLOCK = 65_536;

    /// <summary>
    /// Lists the materials of a preparation, also those this version cannot open, in the order the person added them.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The materials; those this version cannot open come last.</returns>
    public async Task<IReadOnlyList<BflMaterialEntry>> ListMaterialsAsync(Guid projectId, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        return await this.ListMaterialsWithoutInitializeAsync(projectId, token);
    }

    /// <summary>
    /// Adds a material to a preparation, unless it has one with the same content already, and counts that as a
    /// change of the preparation.
    /// </summary>
    /// <remarks>
    /// The store gives the material its ID, the time it was added, and the length of its text. It keeps only the
    /// name of the document, never a path, even when the name passed contains one.
    /// </remarks>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="material">What the lab knows about the document: its name, size, hash, and how it was read.</param>
    /// <param name="text">The text of the document, after the prompt injection filter.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The new material, or the one with the same content which the preparation had already.</returns>
    /// <exception cref="ArgumentException">The name, the hash, or the text is empty.</exception>
    /// <exception cref="FileNotFoundException">The preparation does not exist.</exception>
    /// <exception cref="InvalidDataException">This version cannot open the preparation, so it does not write it either.</exception>
    public async Task<BflMaterialAddition> AddMaterialAsync(Guid projectId, BflMaterial material, string text, CancellationToken token = default)
    {
        var name = MaterialName(material.Name);
        if (name.Length is 0)
            throw new ArgumentException("A material needs a name.", nameof(material));

        if (string.IsNullOrWhiteSpace(material.SourceHash))
            throw new ArgumentException("A material needs the hash of its document.", nameof(material));

        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("A material needs a text.", nameof(text));

        await this.InitializeAsync(token);
        var gate = this.GetLock(projectId);
        await gate.WaitAsync(token);
        try
        {
            var project = await this.LoadRequiredWithoutInitializeAsync(projectId, token);
            var existing = await this.ListMaterialsWithoutInitializeAsync(projectId, token);
            if (existing.FirstOrDefault(entry => string.Equals(entry.Material?.SourceHash, material.SourceHash, StringComparison.OrdinalIgnoreCase))?.Material is { } duplicate)
                return new(duplicate, IsDuplicate: true);

            var stored = new BflMaterial
            {
                MaterialId = Guid.NewGuid(),
                Name = name,
                AddedAtUtc = DateTimeOffset.UtcNow,
                SourceBytes = Math.Max(0, material.SourceBytes),
                SourceHash = material.SourceHash.ToLowerInvariant(),
                TextLength = text.Length,
                IsIncomplete = material.IsIncomplete,
                IsShortened = material.IsShortened,
                FailedPages = [.. material.FailedPages],
                FilteredPassages = Math.Max(0, material.FilteredPassages),
            };

            // The text comes first: a material whose description exists always has its text, even after a crash in between.
            await WriteTextAtomicAsync(this.MaterialTextPath(projectId, stored.MaterialId), text, token);
            await WriteTextAtomicAsync(this.MaterialPath(projectId, stored.MaterialId), JsonSerializer.Serialize(stored, JSON_OPTIONS), token);

            project.ModifiedAtUtc = DateTimeOffset.UtcNow;
            await this.StoreProjectAsync(project, token);
            return new(stored, IsDuplicate: false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Deletes a material of a preparation, also when this version cannot open it, and counts that as a change of the
    /// preparation.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="materialId">The ID of the material.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The preparation with its new time of change.</returns>
    /// <exception cref="FileNotFoundException">The preparation does not exist.</exception>
    /// <exception cref="InvalidDataException">This version cannot open the preparation, so it does not write it either.</exception>
    public async Task<BflProject> DeleteMaterialAsync(Guid projectId, Guid materialId, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var gate = this.GetLock(projectId);
        await gate.WaitAsync(token);
        try
        {
            var project = await this.LoadRequiredWithoutInitializeAsync(projectId, token);

            // The description goes first: a text left behind by a crash in between belongs to no material and is never read.
            DeleteIfExists(this.MaterialPath(projectId, materialId));
            DeleteIfExists(this.MaterialTextPath(projectId, materialId));

            project.ModifiedAtUtc = DateTimeOffset.UtcNow;
            await this.StoreProjectAsync(project, token);
            return project;
        }
        finally
        {
            gate.Release();
        }

        static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    /// <summary>
    /// Reads the beginning of the text of a material, at most as many characters as given.
    /// </summary>
    /// <remarks>
    /// Only what a prompt takes is read, never more: a long document would otherwise lie in memory as a whole with
    /// every request.
    /// </remarks>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="materialId">The ID of the material.</param>
    /// <param name="maxCharacters">How many characters to read at most.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The beginning of the text; the whole text when it is not longer.</returns>
    /// <exception cref="InvalidDataException">The text cannot be read, e.g., because the person deleted the material meanwhile.</exception>
    public async Task<string> ReadMaterialTextAsync(Guid projectId, Guid materialId, int maxCharacters, CancellationToken token = default)
    {
        if (maxCharacters <= 0)
            return string.Empty;

        await this.InitializeAsync(token);
        try
        {
            await using var stream = new FileStream(this.MaterialTextPath(projectId, materialId), FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, true);
            using var reader = new StreamReader(stream, new UTF8Encoding(false));
            var buffer = new char[Math.Min(maxCharacters, MATERIAL_TEXT_READ_BLOCK)];
            var text = new StringBuilder();
            while (text.Length < maxCharacters)
            {
                var read = await reader.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, maxCharacters - text.Length)), token);
                if (read is 0)
                    break;

                text.Append(buffer, 0, read);
            }

            return text.ToString();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException("AI Studio cannot read the text of the material.", exception);
        }
    }

    /// <summary>
    /// Lists the materials of a preparation without the first start of the store.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="token">The cancellation token.</param>
    private async Task<IReadOnlyList<BflMaterialEntry>> ListMaterialsWithoutInitializeAsync(Guid projectId, CancellationToken token)
    {
        var directory = this.MaterialsDirectory(projectId);
        if (!Directory.Exists(directory))
            return [];

        List<BflMaterialEntry> materials = [];
        foreach (var path in Directory.EnumerateFiles(directory, $"*{MATERIAL_FILE_EXTENSION}"))
        {
            token.ThrowIfCancellationRequested();
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "D", out var materialId))
                continue;

            materials.Add(new(materialId, await this.LoadMaterialAsync(projectId, materialId, path, token)));
        }

        return
        [
            .. materials
                .OrderBy(entry => entry.IsAvailable ? 0 : 1)
                .ThenBy(entry => entry.Material?.AddedAtUtc ?? DateTimeOffset.MaxValue)
                .ThenBy(entry => entry.MaterialId)
        ];
    }

    /// <summary>
    /// Reads the description of a material.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="materialId">The ID the file names.</param>
    /// <param name="path">The file of the description.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The material, or null when it is damaged, has no text, or a newer version stored it.</returns>
    private async Task<BflMaterial?> LoadMaterialAsync(Guid projectId, Guid materialId, string path, CancellationToken token)
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, true);
            var material = await JsonSerializer.DeserializeAsync<BflMaterial>(stream, JSON_OPTIONS, token);
            if (material is not null && IsValidMaterial(material, materialId) && File.Exists(this.MaterialTextPath(projectId, materialId)))
                return material;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning(exception, "Could not open the material '{MaterialId}' of the preparation '{ProjectId}' of the BeyondFacts Lab.", materialId, projectId);
            return null;
        }

        logger.LogWarning("The material '{MaterialId}' of the preparation '{ProjectId}' of the BeyondFacts Lab is not valid, or its text is missing.", materialId, projectId);
        return null;
    }

    /// <summary>
    /// Checks a material which was read from a file.
    /// </summary>
    /// <param name="material">The material.</param>
    /// <param name="expectedMaterialId">The ID its file names.</param>
    private static bool IsValidMaterial(BflMaterial material, Guid expectedMaterialId) =>
        material.SchemaVersion is >= 1 and <= BflMaterial.CURRENT_SCHEMA_VERSION &&
        material.MaterialId == expectedMaterialId &&
        !string.IsNullOrWhiteSpace(material.Name) &&
        !string.IsNullOrWhiteSpace(material.SourceHash) &&
        material is { SourceBytes: >= 0, TextLength: >= 0, FilteredPassages: >= 0 } &&
        !IsNull(material.FailedPages);

    /// <summary>
    /// Gets the name under which the lab keeps a document: its file name without any folder, cleaned like the name of
    /// a preparation.
    /// </summary>
    /// <remarks>
    /// Both kinds of separators count, whatever the system: a path from Windows on a Mac still names its folders.
    /// </remarks>
    /// <param name="name">The name of the document, possibly with a path.</param>
    /// <returns>The cleaned name, empty when nothing remains.</returns>
    internal static string MaterialName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        return SanitizeName(name[(name.LastIndexOfAny(['/', '\\']) + 1)..]);
    }

    /// <summary>
    /// Gets the folder of the materials of a preparation.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    private string MaterialsDirectory(Guid projectId) => Path.Combine(this.ProjectDirectory(projectId), MATERIALS_DIRECTORY_NAME);

    /// <summary>
    /// Gets the file which describes a material.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="materialId">The ID of the material.</param>
    private string MaterialPath(Guid projectId, Guid materialId) => Path.Combine(this.MaterialsDirectory(projectId), $"{materialId:D}{MATERIAL_FILE_EXTENSION}");

    /// <summary>
    /// Gets the file which holds the text of a material.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="materialId">The ID of the material.</param>
    private string MaterialTextPath(Guid projectId, Guid materialId) => Path.Combine(this.MaterialsDirectory(projectId), $"{materialId:D}{MATERIAL_TEXT_FILE_EXTENSION}");
}