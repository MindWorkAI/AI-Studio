using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed partial class ToolSettingsService(SettingsManager settingsManager, RustService rustService, ILogger<ToolSettingsService> logger)
{
    /// <summary>
    /// Builds the key under which an organization's configuration addresses one tool setting.
    /// </summary>
    private static string ManagedSettingKey(string toolId, string fieldName) => $"{toolId}.{fieldName}";

    /// <summary>
    /// Reads the effective settings of one tool.
    /// </summary>
    /// <remarks>
    /// Three sources, in this order: a value an organization locked wins over everything, then
    /// the value the user saved, then a default an organization pre-filled.<br/><br/>
    /// A secret knows only two of them. It comes from the operating system's keyring, where what
    /// the user typed lives, or — locked — from the organization's configuration, encrypted with
    /// the enterprise secret. There is deliberately no pre-filled default for a secret: a
    /// pre-filled value is one the user may save as their own, which would copy the
    /// organization's key into their keyring, where removing the configuration plugin could no
    /// longer take it back.
    /// </remarks>
    public async Task<Dictionary<string, string>> GetSettingsAsync(ToolDefinition definition)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var storedValues = settingsManager.ConfigurationData.Tools.Settings.GetValueOrDefault(definition.Id);
        var lockedSettings = settingsManager.ConfigurationData.Tools.LockedToolSettings;
        var defaultSettings = settingsManager.ConfigurationData.Tools.DefaultToolSettings;

        foreach (var property in definition.SettingsSchema.Properties)
        {
            var fieldName = property.Key;
            var fieldDefinition = property.Value;
            var managedKey = ManagedSettingKey(definition.Id, fieldName);
            if (fieldDefinition.Secret)
            {
                //
                // A locked secret belongs to the organization, and the user's own is then not
                // even read: whoever fixed this field decided which key is used, and reaching
                // for another one would undo that decision. The keyring keeps what the user
                // typed, untouched, which is what hands it back when the plugin is gone.
                //
                if (lockedSettings.TryGetValue(managedKey, out var lockedSecret))
                {
                    if (this.TryDecryptManagedSecret(definition.Id, fieldName, lockedSecret, out var managedSecret))
                        values[fieldName] = managedSecret;

                    continue;
                }

                var response = await rustService.GetSecret(new ToolSettingsSecretId(definition.Id, fieldName), SecretStoreType.TOOL_SETTINGS, isTrying: true);
                if (response.Success)
                    values[fieldName] = await response.Secret.Decrypt(Program.ENCRYPTION);

                continue;
            }

            if (lockedSettings.TryGetValue(managedKey, out var lockedValue))
                values[fieldName] = lockedValue;
            else if (storedValues?.TryGetValue(fieldName, out var storedValue) is true)
                values[fieldName] = storedValue;
            else if (defaultSettings.TryGetValue(managedKey, out var defaultValue))
                values[fieldName] = defaultValue;
        }

        return values;
    }

    public async Task<ToolConfigurationState> GetConfigurationStateAsync(
        ToolDefinition definition,
        IToolImplementation? implementation = null,
        CancellationToken token = default)
    {
        var values = await this.GetSettingsAsync(definition);
        return await this.ValidateSettingsAsync(definition, values, implementation, token);
    }

    public async Task<ToolConfigurationState> ValidateSettingsAsync(
        ToolDefinition definition,
        IReadOnlyDictionary<string, string> values,
        IToolImplementation? implementation = null,
        CancellationToken token = default)
    {
        var missing = new List<string>();
        foreach (var requiredField in definition.SettingsSchema.Required)
        {
            if (!values.TryGetValue(requiredField, out var value) || string.IsNullOrWhiteSpace(value))
                missing.Add(requiredField);
        }

        if (missing.Count > 0)
        {
            return new ToolConfigurationState
            {
                IsConfigured = false,
                MissingRequiredFields = missing,
            };
        }

        if (implementation is not null)
        {
            var validationState = await implementation.ValidateConfigurationAsync(definition, values, token);
            if (validationState is not null && !validationState.IsConfigured)
                return validationState;
        }

        return new ToolConfigurationState
        {
            IsConfigured = true,
        };
    }

    public async Task SaveSettingsAsync(ToolDefinition definition, IReadOnlyDictionary<string, string> values)
    {
        if (!settingsManager.ConfigurationData.Tools.Settings.TryGetValue(definition.Id, out var storedValues))
        {
            storedValues = new Dictionary<string, string>(StringComparer.Ordinal);
            settingsManager.ConfigurationData.Tools.Settings[definition.Id] = storedValues;
        }

        foreach (var property in definition.SettingsSchema.Properties)
        {
            var fieldName = property.Key;
            var fieldDefinition = property.Value;
            values.TryGetValue(fieldName, out var value);
            value ??= string.Empty;

            // A locked setting belongs to the organization; whatever the dialog sent for it is
            // discarded rather than stored where it would never be read again:
            if (this.IsFieldLocked(definition, fieldName))
                continue;

            if (fieldDefinition.Secret)
            {
                var secretId = new ToolSettingsSecretId(definition.Id, fieldName);
                if (string.IsNullOrWhiteSpace(value))
                    await rustService.DeleteSecret(secretId, SecretStoreType.TOOL_SETTINGS);
                else
                    await rustService.SetSecret(secretId, value, SecretStoreType.TOOL_SETTINGS);

                continue;
            }

            storedValues[fieldName] = value;
        }

        await settingsManager.StoreSettings();
        await MessageBus.INSTANCE.SendMessage<object?>(null, Event.CONFIGURATION_CHANGED);
    }

    /// <summary>
    /// Whether an organization fixed this setting, which makes it read-only for the user.
    /// </summary>
    public bool IsFieldLocked(ToolDefinition definition, string fieldName) =>
        settingsManager.ConfigurationData.Tools.LockedToolSettings.ContainsKey(ManagedSettingKey(definition.Id, fieldName));

    /// <summary>
    /// Decrypts a secret an organization deployed through a configuration plugin.
    /// </summary>
    /// <remarks>
    /// The value arrives encrypted with the enterprise secret and is decrypted here, on the way
    /// to the tool, rather than copied into the keyring. A configuration file holding ciphertext
    /// is worth nothing without that secret, which lives outside every file AI Studio deploys —
    /// in the registry or an environment variable.<br/><br/>
    /// Only the encrypted form is accepted: a plaintext secret in a configuration file would be
    /// readable by everyone the file reaches, so it is refused rather than used. That is the same
    /// rule the LLM providers and the data sources follow for their keys.<br/><br/>
    /// A secret that cannot be decrypted leaves the field empty, which makes the tool count as
    /// unconfigured and say so. The alternative — searching with somebody else's key — would be
    /// worse than not searching.
    /// </remarks>
    private bool TryDecryptManagedSecret(string toolId, string fieldName, string? encryptedSecret, out string secret)
    {
        secret = string.Empty;
        var managedKey = ManagedSettingKey(toolId, fieldName);
        if (string.IsNullOrWhiteSpace(encryptedSecret))
            return false;

        if (!EnterpriseEncryption.IsEncrypted(encryptedSecret))
        {
            logger.LogWarning("The managed tool setting '{ManagedKey}' holds a plaintext secret. Only encrypted secrets, starting with 'ENC:v1:', are supported.", managedKey);
            return false;
        }

        var encryption = PluginFactory.EnterpriseEncryption;
        if (encryption?.IsAvailable is not true)
        {
            logger.LogWarning("The managed tool setting '{ManagedKey}' holds an encrypted secret, but no enterprise encryption secret is configured.", managedKey);
            return false;
        }

        if (!encryption.TryDecrypt(encryptedSecret, out var decryptedSecret))
        {
            logger.LogWarning("Failed to decrypt the managed tool setting '{ManagedKey}'. The enterprise encryption secret may be the wrong one.", managedKey);
            return false;
        }

        secret = decryptedSecret;
        return true;
    }
}