using System.Linq.Expressions;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolSettingsService(SettingsManager settingsManager, RustService rustService)
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
    /// the value the user saved, then a default an organization pre-filled. Secrets never come
    /// from a configuration file — they live in the operating system's keyring.
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
            if (fieldDefinition.Secret)
            {
                var response = await rustService.GetSecret(new ToolSettingsSecretId(definition.Id, fieldName), SecretStoreType.TOOL_SETTINGS, isTrying: true);
                if (response.Success)
                    values[fieldName] = await response.Secret.Decrypt(Program.ENCRYPTION);

                continue;
            }

            var managedKey = ManagedSettingKey(definition.Id, fieldName);
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
        await MessageBus.INSTANCE.SendMessage<object?>(null, Event.CONFIGURATION_CHANGED, null);
    }

    /// <summary>
    /// Whether an organization fixed this setting, which makes it read-only for the user.
    /// </summary>
    public bool IsFieldLocked(ToolDefinition definition, string fieldName) =>
        settingsManager.ConfigurationData.Tools.LockedToolSettings.ContainsKey(ManagedSettingKey(definition.Id, fieldName));
}
