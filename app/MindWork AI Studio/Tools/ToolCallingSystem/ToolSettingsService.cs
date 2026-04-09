using AIStudio.Settings;
using AIStudio.Tools.Services;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolSettingsService(SettingsManager settingsManager, RustService rustService)
{
    public async Task<Dictionary<string, string>> GetSettingsAsync(ToolDefinition definition)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var storedValues = settingsManager.ConfigurationData.Tools.Settings.GetValueOrDefault(definition.Id);
        foreach (var property in definition.SettingsSchema.Properties)
        {
            var fieldName = property.Key;
            var fieldDefinition = property.Value;
            if (fieldDefinition.Secret)
            {
                var response = await rustService.GetSecret(new ToolSettingsSecretId(definition.Id, fieldName), isTrying: true);
                if (response.Success)
                    values[fieldName] = await response.Secret.Decrypt(Program.ENCRYPTION);

                continue;
            }

            if (storedValues?.TryGetValue(fieldName, out var storedValue) is true)
                values[fieldName] = storedValue;
        }

        return values;
    }

    public async Task<ToolConfigurationState> GetConfigurationStateAsync(ToolDefinition definition)
    {
        var values = await this.GetSettingsAsync(definition);
        var missing = new List<string>();
        foreach (var requiredField in definition.SettingsSchema.Required)
        {
            if (!values.TryGetValue(requiredField, out var value) || string.IsNullOrWhiteSpace(value))
                missing.Add(requiredField);
        }

        return new ToolConfigurationState
        {
            IsConfigured = missing.Count == 0,
            MissingRequiredFields = missing,
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

            if (fieldDefinition.Secret)
            {
                var secretId = new ToolSettingsSecretId(definition.Id, fieldName);
                if (string.IsNullOrWhiteSpace(value))
                    await rustService.DeleteSecret(secretId);
                else
                    await rustService.SetSecret(secretId, value);

                continue;
            }

            storedValues[fieldName] = value;
        }

        await settingsManager.StoreSettings();
        await MessageBus.INSTANCE.SendMessage<object?>(null, Event.CONFIGURATION_CHANGED, null);
    }
}
