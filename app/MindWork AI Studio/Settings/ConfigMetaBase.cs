namespace AIStudio.Settings;

/// <summary>
/// The type-independent part of the configuration metadata: which configuration plugin manages
/// the setting, and in which way.
/// </summary>
/// <remarks>
/// The managed state lives here so that it can be processed without knowing the setting's type,
/// e.g. when cleaning up settings whose configuration plugin was removed.
/// </remarks>
public abstract record ConfigMetaBase(string SettingName) : IConfig
{
    protected static SettingsManager SettingsManagerAccess => Program.SERVICE_PROVIDER.GetRequiredService<SettingsManager>();

    protected static ILogger Log => Program.LOGGER_FACTORY.CreateLogger(nameof(ConfigMetaBase));

    /// <summary>
    /// The persisted name of the configuration setting.
    /// </summary>
    public string SettingName { get; } = SettingName;

    /// <summary>
    /// Indicates whether the configuration is locked by a configuration plugin.
    /// </summary>
    public bool IsLocked { get; private set; }

    /// <summary>
    /// The ID of the plugin that locked this configuration.
    /// </summary>
    public Guid LockedByConfigPluginId { get; private set; }

    /// <summary>
    /// How this setting is managed by a configuration plugin, if at all.
    /// </summary>
    public ManagedConfigurationMode? ManagedMode { get; private set; }

    /// <summary>
    /// The ID of the plugin that currently provides an editable default value.
    /// </summary>
    public Guid EditableDefaultByConfigPluginId { get; private set; }

    /// <summary>
    /// The configuration plugins which contribute to this setting.
    /// </summary>
    /// <remarks>
    /// Contributions are additive, so several configuration plugins may contribute at the same time
    /// and each of them keeps its own contribution. An organization might enable one preview feature
    /// for everybody and another one for a single department, for example.
    /// </remarks>
    public abstract IReadOnlyCollection<Guid> ContributingConfigPluginIds { get; }

    /// <summary>
    /// Indicates whether at least one configuration plugin contributes to this setting.
    /// </summary>
    public bool HasPluginContribution => this.ContributingConfigPluginIds.Count > 0;

    /// <summary>
    /// Locks the configuration state, indicating that it is controlled by a specific plugin.
    /// </summary>
    /// <param name="pluginId">The ID of the plugin that is locking this configuration.</param>
    public void LockConfiguration(Guid pluginId)
    {
        this.IsLocked = true;
        this.LockedByConfigPluginId = pluginId;
        this.ManagedMode = ManagedConfigurationMode.LOCKED;
        this.EditableDefaultByConfigPluginId = Guid.Empty;
        SettingsManagerAccess.ConfigurationData.ManagedLockedConfigurations[this.SettingName] = pluginId;
    }

    /// <summary>
    /// Restores persisted locked configuration metadata after settings were loaded.
    /// </summary>
    public void RestoreLockedConfiguration()
    {
        if (this.IsLocked || this.ManagedMode is not null)
            return;

        if (!SettingsManagerAccess.ConfigurationData.ManagedLockedConfigurations.TryGetValue(this.SettingName, out var pluginId) || pluginId == Guid.Empty)
            return;

        this.IsLocked = true;
        this.LockedByConfigPluginId = pluginId;
        this.ManagedMode = ManagedConfigurationMode.LOCKED;
        this.EditableDefaultByConfigPluginId = Guid.Empty;
    }

    /// <summary>
    /// Resets the locked state of the configuration, allowing it to be modified again.
    /// This will also reset the property to its default value.
    /// </summary>
    public void ResetLockedConfiguration()
    {
        SettingsManagerAccess.ConfigurationData.ManagedLockedConfigurations.Remove(this.SettingName);

        this.IsLocked = false;
        this.LockedByConfigPluginId = Guid.Empty;

        if (this.ManagedMode is ManagedConfigurationMode.LOCKED)
            this.ManagedMode = null;

        this.RestoreUserValueOrDefault();
    }

    /// <summary>
    /// Unlocks the configuration state without changing the current value.
    /// </summary>
    public void UnlockConfiguration()
    {
        SettingsManagerAccess.ConfigurationData.ManagedLockedConfigurations.Remove(this.SettingName);
        
        this.IsLocked = false;
        this.LockedByConfigPluginId = Guid.Empty;
        
        if (this.ManagedMode is ManagedConfigurationMode.LOCKED)
            this.ManagedMode = null;
    }

    /// <summary>
    /// Marks the setting as having an editable default provided by a configuration plugin.
    /// </summary>
    public void SetEditableDefaultConfiguration(Guid pluginId)
    {
        SettingsManagerAccess.ConfigurationData.ManagedLockedConfigurations.Remove(this.SettingName);
        
        this.IsLocked = false;
        this.LockedByConfigPluginId = Guid.Empty;
        this.ManagedMode = ManagedConfigurationMode.EDITABLE_DEFAULT;
        this.EditableDefaultByConfigPluginId = pluginId;
    }

    /// <summary>
    /// Clears the editable-default state without changing the current value.
    /// </summary>
    public void ClearEditableDefaultConfiguration()
    {
        if (this.ManagedMode is ManagedConfigurationMode.EDITABLE_DEFAULT)
            this.ManagedMode = null;

        this.EditableDefaultByConfigPluginId = Guid.Empty;
    }

    /// <summary>
    /// Removes the contribution of one configuration plugin without changing the current value.
    /// </summary>
    /// <param name="configPluginId">The configuration plugin whose contribution is removed.</param>
    /// <returns>True when that plugin had a contribution, otherwise false.</returns>
    public abstract bool RemovePluginContribution(Guid configPluginId);

    /// <summary>
    /// Indicates whether the value the user had chosen before a configuration plugin took over
    /// this setting is still available.
    /// </summary>
    public bool HasUserValueSnapshot => SettingsManagerAccess.ConfigurationData.ManagedUserValueSnapshots.ContainsKey(this.SettingName);

    /// <summary>
    /// Remembers the current value as the user's value, so that it can be restored once no
    /// configuration plugin manages this setting anymore.
    /// </summary>
    /// <remarks>
    /// Only an unmanaged setting holds a value which belongs to the user. When one configuration
    /// plugin takes a setting over from another, the current value belongs to the previous plugin,
    /// so the snapshot of the user's value must survive that handover untouched.<br/><br/>
    /// The persisted editable default counts as managed as well: unlike a locked setting, it is not
    /// restored into the in-memory state when the settings are loaded, so right after a start it is
    /// the only evidence that a configuration plugin is already in charge.
    /// </remarks>
    public void CaptureUserValueSnapshot()
    {
        if (this.ManagedMode is not null || SettingsManagerAccess.ConfigurationData.ManagedEditableDefaults.ContainsKey(this.SettingName))
            return;

        var snapshots = SettingsManagerAccess.ConfigurationData.ManagedUserValueSnapshots;
        if (snapshots.ContainsKey(this.SettingName))
            return;

        snapshots[this.SettingName] = this.SerializeCurrentValueAsJson();
    }

    /// <summary>
    /// Restores the value the user had chosen before a configuration plugin took over this setting.
    /// </summary>
    /// <remarks>
    /// The snapshot is consumed either way: when it cannot be applied, keeping it would mean trying
    /// the same broken value again on every start.
    /// </remarks>
    /// <returns>True when a snapshot was available and could be applied, otherwise false.</returns>
    private bool TryRestoreUserValueSnapshot()
    {
        var snapshots = SettingsManagerAccess.ConfigurationData.ManagedUserValueSnapshots;
        if (!snapshots.Remove(this.SettingName, out var snapshot))
            return false;

        return this.TrySetValueFromJson(snapshot);
    }

    /// <summary>
    /// Drops the snapshot of the user's value without changing the current value.
    /// </summary>
    /// <returns>True when a snapshot was dropped, otherwise false.</returns>
    public bool ClearUserValueSnapshot() => SettingsManagerAccess.ConfigurationData.ManagedUserValueSnapshots.Remove(this.SettingName);

    /// <summary>
    /// Serializes the current value the same way the managed states record it.
    /// </summary>
    /// <remarks>
    /// This is meant for comparisons, e.g. to tell whether the user has changed an editable default
    /// in the meantime. It is not meant for restoring a value: the representation is lossy.
    /// </remarks>
    public abstract string SerializeCurrentValue();

    /// <summary>
    /// Restores the user's value, or falls back to the default value when no snapshot is available.
    /// </summary>
    /// <remarks>
    /// Settings which a configuration plugin managed before this app version has no snapshot, and
    /// neither has a setting whose value the user never changed. The default value is the best
    /// answer in both cases.
    /// </remarks>
    private void RestoreUserValueOrDefault()
    {
        if (this.TryRestoreUserValueSnapshot())
            return;

        this.Reset();
    }

    /// <summary>
    /// Serializes the current value as JSON, so that it can be restored without losing information.
    /// </summary>
    protected abstract string SerializeCurrentValueAsJson();

    /// <summary>
    /// Applies a value which was serialized by SerializeCurrentValueAsJson.
    /// </summary>
    /// <param name="json">The serialized value.</param>
    /// <returns>True when the value could be applied, otherwise false.</returns>
    protected abstract bool TrySetValueFromJson(string json);

    /// <summary>
    /// Resets the configuration property to its default value.
    /// </summary>
    protected abstract void Reset();
}