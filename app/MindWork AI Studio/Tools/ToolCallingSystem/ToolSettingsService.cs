using System.Linq.Expressions;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolSettingsService(SettingsManager settingsManager, RustService rustService)
{
    private static readonly Dictionary<(string ToolId, string FieldName), ManagedToolSetting> MANAGED_SETTINGS = new()
    {
        [(ToolSelectionRules.WEB_SEARCH_TOOL_ID, "baseUrl")] = CreateManagedToolSetting(x => x.WebSearchBaseUrl, (tools, value) => tools.WebSearchBaseUrl = value),
        [(ToolSelectionRules.WEB_SEARCH_TOOL_ID, "defaultLanguage")] = CreateManagedToolSetting(x => x.WebSearchDefaultLanguage),
        [(ToolSelectionRules.WEB_SEARCH_TOOL_ID, "defaultSafeSearch")] = CreateManagedToolSetting(x => x.WebSearchDefaultSafeSearch),
        [(ToolSelectionRules.WEB_SEARCH_TOOL_ID, "maxResults")] = CreateManagedToolSetting(x => x.WebSearchMaxResults),
        [(ToolSelectionRules.WEB_SEARCH_TOOL_ID, "timeoutSeconds")] = CreateManagedToolSetting(x => x.WebSearchTimeoutSeconds),
        [(ToolSelectionRules.WEB_SEARCH_TOOL_ID, "maxTotalContentCharacters")] = CreateManagedToolSetting(x => x.WebSearchMaxTotalContentCharacters),
        [(ToolSelectionRules.WEB_SEARCH_TOOL_ID, "minContentCharactersPerResult")] = CreateManagedToolSetting(x => x.WebSearchMinContentCharactersPerResult),
        [(ToolSelectionRules.WEB_SEARCH_TOOL_ID, "pageTimeoutSeconds")] = CreateManagedToolSetting(x => x.WebSearchPageTimeoutSeconds),
        [(ToolSelectionRules.WEB_SEARCH_TOOL_ID, "retrievalTimeoutSeconds")] = CreateManagedToolSetting(x => x.WebSearchRetrievalTimeoutSeconds),
        [(ToolSelectionRules.READ_WEB_PAGE_TOOL_ID, "timeoutSeconds")] = CreateManagedToolSetting(x => x.ReadWebPageTimeoutSeconds),
        [(ToolSelectionRules.READ_WEB_PAGE_TOOL_ID, "maxContentCharacters")] = CreateManagedToolSetting(x => x.ReadWebPageMaxContentCharacters),
        [(ToolSelectionRules.READ_WEB_PAGE_TOOL_ID, "allowedPrivateHosts")] = CreateManagedToolSetting(x => x.ReadWebPageAllowedPrivateHosts, (tools, value) => tools.ReadWebPageAllowedPrivateHosts = value),
    };

    public async Task<Dictionary<string, string>> GetSettingsAsync(ToolDefinition definition)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var storedValues = settingsManager.ConfigurationData.Tools.Settings.GetValueOrDefault(definition.Id);
        foreach (var property in definition.SettingsSchema.Properties)
        {
            var fieldName = property.Key;
            var fieldDefinition = property.Value;
            if (TryGetManagedSetting(definition, fieldName, out var managedSetting))
            {
                var meta = managedSetting.GetMeta();
                if (meta?.IsLocked is true || managedSetting.SetLegacyLocalValue is not null)
                    values[fieldName] = managedSetting.GetValue(settingsManager.ConfigurationData.Tools);
                else if (storedValues?.TryGetValue(fieldName, out var managedStoredValue) is true)
                    values[fieldName] = managedStoredValue;
                else if (meta?.ManagedMode is ManagedConfigurationMode.EDITABLE_DEFAULT)
                    values[fieldName] = managedSetting.GetValue(settingsManager.ConfigurationData.Tools);

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

            if (TryGetManagedSetting(definition, fieldName, out var managedSetting))
            {
                if (managedSetting.GetMeta()?.IsLocked is true)
                    continue;

                if (managedSetting.SetLegacyLocalValue is not null)
                    managedSetting.SetLegacyLocalValue(settingsManager.ConfigurationData.Tools, value);
                else
                    storedValues[fieldName] = value;

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

    public bool IsFieldLocked(ToolDefinition definition, string fieldName) =>
        TryGetManagedSetting(definition, fieldName, out var managedSetting) &&
        managedSetting.GetMeta()?.IsLocked is true;

    private static bool TryGetManagedSetting(ToolDefinition definition, string fieldName, out ManagedToolSetting managedSetting) =>
        MANAGED_SETTINGS.TryGetValue((definition.Id, fieldName), out managedSetting!);

    private static ManagedToolSetting CreateManagedToolSetting(
        Expression<Func<DataTools, string>> propertyExpression,
        Action<DataTools, string>? setLegacyLocalValue = null)
    {
        var getValue = propertyExpression.Compile();
        return new ManagedToolSetting(
            getValue,
            () => ManagedConfiguration.TryGet(x => x.Tools, propertyExpression, out var meta) ? meta : null,
            setLegacyLocalValue);
    }

    private sealed record ManagedToolSetting(
        Func<DataTools, string> GetValue,
        Func<ConfigMeta<DataTools, string>?> GetMeta,
        Action<DataTools, string>? SetLegacyLocalValue);
}
