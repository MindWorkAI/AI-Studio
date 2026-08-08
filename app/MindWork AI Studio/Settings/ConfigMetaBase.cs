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

        this.Reset();
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
    /// Resets the configuration property to its default value.
    /// </summary>
    protected abstract void Reset();
}