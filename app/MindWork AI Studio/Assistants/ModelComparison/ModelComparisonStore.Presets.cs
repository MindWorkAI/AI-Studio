namespace AIStudio.Assistants.ModelComparison;

public sealed partial class ModelComparisonStore
{
    /// <summary>
    /// The folder of the presets, one file per preset.
    /// </summary>
    private const string PRESETS_DIRECTORY_NAME = "presets";

    /// <summary>
    /// Gets the folder of the presets.
    /// </summary>
    private string PresetsDirectory => Path.Combine(this.RootDirectory, PRESETS_DIRECTORY_NAME);

    /// <summary>
    /// Gets the file of a preset.
    /// </summary>
    /// <param name="presetId">The ID of the preset.</param>
    private string PresetPath(Guid presetId) => Path.Combine(this.PresetsDirectory, $"{presetId:D}.json");

    /// <summary>
    /// Lists the presets, sorted by name.
    /// </summary>
    /// <remarks>
    /// A preset this version cannot read is left out, but stays on the disk, see ModelComparisonJson.
    /// </remarks>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The presets.</returns>
    public async Task<IReadOnlyList<ModelComparisonPreset>> ListPresetsAsync(CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        if (!Directory.Exists(this.PresetsDirectory))
            return [];

        List<ModelComparisonPreset> presets = [];
        foreach (var path in Directory.EnumerateFiles(this.PresetsDirectory, "*.json"))
        {
            token.ThrowIfCancellationRequested();
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "D", out var presetId))
                continue;

            // The ID inside the file must not lead to another file than the one it was read from:
            if (await this.ReadJsonAsync<ModelComparisonPreset>(path, token) is { } preset)
                presets.Add(preset with { Id = presetId });
        }

        return [.. presets.OrderBy(preset => preset.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>
    /// Stores a preset, as a new one or in place of the one with the same ID.
    /// </summary>
    /// <param name="preset">The preset.</param>
    /// <param name="token">The cancellation token.</param>
    /// <exception cref="ArgumentException">The preset has no ID or no name.</exception>
    public async Task SavePresetAsync(ModelComparisonPreset preset, CancellationToken token = default)
    {
        if (preset.Id == Guid.Empty)
            throw new ArgumentException("A preset needs an ID.", nameof(preset));

        if (string.IsNullOrWhiteSpace(preset.Name))
            throw new ArgumentException("A preset needs a name.", nameof(preset));

        await this.InitializeAsync(token);
        var gate = this.GetLock(preset.Id);
        await gate.WaitAsync(token);
        try
        {
            await WriteJsonAtomicAsync(this.PresetPath(preset.Id), preset with { Name = preset.Name.Trim() }, token);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Deletes a preset.
    /// </summary>
    /// <param name="presetId">The ID of the preset.</param>
    /// <param name="token">The cancellation token.</param>
    public async Task DeletePresetAsync(Guid presetId, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var gate = this.GetLock(presetId);
        await gate.WaitAsync(token);
        try
        {
            var path = this.PresetPath(presetId);
            if (File.Exists(path))
                File.Delete(path);
        }
        finally
        {
            gate.Release();
            this.ForgetLock(presetId);
        }
    }
}