namespace AIStudio.Settings;

public enum ManagedConfigurationMode
{
    /// <summary>
    /// The configuration is locked by a configuration plugin. The user cannot change the value of this setting, and it will be overridden by the plugin on each update.
    /// </summary>
    LOCKED,
    
    /// <summary>
    /// The configuration has an editable default provided by a configuration plugin. The user can change the value of this setting.
    /// </summary>
    EDITABLE_DEFAULT,
}