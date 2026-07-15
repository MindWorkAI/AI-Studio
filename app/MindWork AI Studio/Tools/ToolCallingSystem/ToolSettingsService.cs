using AIStudio.Settings;
using AIStudio.Tools.Services;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolSettingsService(SettingsManager settingsManager, RustService rustService)
{
    private const string WEB_SEARCH_BASE_URL_FIELD = "baseUrl";
    private const string READ_WEB_PAGE_ALLOWED_PRIVATE_HOSTS_FIELD = "allowedPrivateHosts";

    public async Task<Dictionary<string, string>> GetSettingsAsync(ToolDefinition definition)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var storedValues = settingsManager.ConfigurationData.Tools.Settings.GetValueOrDefault(definition.Id);
        foreach (var property in definition.SettingsSchema.Properties)
        {
            var fieldName = property.Key;
            var fieldDefinition = property.Value;
            if (IsWebSearchBaseUrlField(definition, fieldName))
            {
                values[fieldName] = settingsManager.ConfigurationData.Tools.WebSearchBaseUrl;
                continue;
            }

            if (IsReadWebPageAllowedPrivateHostsField(definition, fieldName))
            {
                values[fieldName] = settingsManager.ConfigurationData.Tools.ReadWebPageAllowedPrivateHosts;
                continue;
            }

            if (fieldDefinition.Secret)
            {
                var response = await rustService.GetSecret(new ToolSettingsSecretId(definition.Id, fieldName), SecretStoreType.TOOL_SETTINGS, isTrying: true);
                if (response.Success)
                    values[fieldName] = await response.Secret.Decrypt(Program.ENCRYPTION);

                continue;
            }

            if (storedValues?.TryGetValue(fieldName, out var storedValue) is true)
                values[fieldName] = storedValue;
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

            if (IsWebSearchBaseUrlField(definition, fieldName))
            {
                if (!IsWebSearchBaseUrlLocked())
                    settingsManager.ConfigurationData.Tools.WebSearchBaseUrl = value;

                continue;
            }

            if (IsReadWebPageAllowedPrivateHostsField(definition, fieldName))
            {
                if (!IsReadWebPageAllowedPrivateHostsLocked())
                    settingsManager.ConfigurationData.Tools.ReadWebPageAllowedPrivateHosts = value;

                continue;
            }

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

    private static bool IsWebSearchBaseUrlField(ToolDefinition definition, string fieldName) =>
        definition.Id.Equals(ToolSelectionRules.WEB_SEARCH_TOOL_ID, StringComparison.Ordinal) &&
        fieldName.Equals(WEB_SEARCH_BASE_URL_FIELD, StringComparison.Ordinal);

    private static bool IsWebSearchBaseUrlLocked() =>
        ManagedConfiguration.TryGet(x => x.Tools, x => x.WebSearchBaseUrl, out var meta) && meta.IsLocked;

    private static bool IsReadWebPageAllowedPrivateHostsField(ToolDefinition definition, string fieldName) =>
        definition.Id.Equals(ToolSelectionRules.READ_WEB_PAGE_TOOL_ID, StringComparison.Ordinal) &&
        fieldName.Equals(READ_WEB_PAGE_ALLOWED_PRIVATE_HOSTS_FIELD, StringComparison.Ordinal);

    private static bool IsReadWebPageAllowedPrivateHostsLocked() =>
        ManagedConfiguration.TryGet(x => x.Tools, x => x.ReadWebPageAllowedPrivateHosts, out var meta) && meta.IsLocked;
}
