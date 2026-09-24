using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Provider.HuggingFace;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ERIClient.DataModel;

using Lua;

using Host = AIStudio.Provider.SelfHosted.Host;

namespace AIStudio.Tools.PluginSystem;

/// <summary>Checks the fields used by the creation forms before leaving the paste dialog.</summary>
public static class ConfigurationSnippetImportValidation
{
    public static void Validate(string section, LuaTable table)
    {
        ConfigurationImportFields.ValidateExportId(table);
        switch (section)
        {
            case "PROFILES":
                ConfigurationImportFields.String(table, "Name");
                ConfigurationImportFields.String(table, "NeedToKnow");
                ConfigurationImportFields.String(table, "Actions");
                break;
            case "LLM_PROVIDERS":
            case "EMBEDDING_PROVIDERS":
            case "TRANSCRIPTION_PROVIDERS":
                ValidateProvider(section, table);
                break;
            case "CHAT_TEMPLATES":
                ValidateChatTemplate(table);
                break;
            case "DATA_SOURCES":
                ValidateERIDataSource(table);
                break;
            case "DOCUMENT_ANALYSIS_POLICIES":
                ConfigurationImportFields.String(table, "PolicyName");
                ConfigurationImportFields.String(table, "PolicyDescription");
                ConfigurationImportFields.String(table, "AnalysisRules");
                ConfigurationImportFields.String(table, "OutputRules");
                ConfigurationImportFields.Enum<ConfidenceLevel>(table, "MinimumProviderConfidence");
                ConfigurationImportFields.Strings(table, "AllowedToolIds");
                ConfigurationImportFields.String(table, "PreselectedProvider", required: false);
                ConfigurationImportFields.String(table, "PreselectedProfile", required: false);
                ConfigurationImportFields.Bool(table, "HidePolicyDefinition");
                break;
            default:
                throw new FormatException("This configuration section cannot be imported here.");
        }
    }

    private static void ValidateProvider(string section, LuaTable table)
    {
        var model = ConfigurationImportFields.Table(table, "Model");
        ConfigurationImportFields.String(model, "Id");
        ConfigurationImportFields.String(model, "DisplayName");
        ConfigurationImportFields.String(table, section == "LLM_PROVIDERS" ? "InstanceName" : "Name");
        ConfigurationImportFields.Enum<LLMProviders>(table, "UsedLLMProvider");
        ConfigurationImportFields.Enum<Host>(table, "Host");
        ConfigurationImportFields.String(table, "Hostname");
        if (table.TryGetValue("HFInferenceProvider", out _))
            ConfigurationImportFields.Enum<HFInferenceProvider>(table, "HFInferenceProvider");
        if (section != "TRANSCRIPTION_PROVIDERS")
            ConfigurationImportFields.String(table, "TokenizerPath", required: false);
        if (section == "LLM_PROVIDERS")
            ConfigurationImportFields.String(table, "AdditionalJsonApiParameters", required: false);
        if (section == "EMBEDDING_PROVIDERS")
        {
            ConfigurationImportFields.Int(table, "TokenLimit", EmbeddingProvider.DEFAULT_TOKEN_LIMIT);
            ConfigurationImportFields.Int(table, "EmbeddingBatchSize", EmbeddingProvider.DEFAULT_EMBEDDING_BATCH_SIZE);
        }
        ConfigurationImportFields.Credential(table, "APIKey", out _);
    }

    private static void ValidateChatTemplate(LuaTable table)
    {
        ConfigurationImportFields.String(table, "Name");
        ConfigurationImportFields.String(table, "SystemPrompt");
        ConfigurationImportFields.String(table, "PredefinedUserPrompt", required: false);
        ConfigurationImportFields.Bool(table, "AllowProfileUsage");
        var messages = ConfigurationImportFields.Table(table, "ExampleConversation");
        for (var index = 1; index <= messages.ArrayLength; index++)
        {
            if (messages[index].Type is not LuaValueType.Table || !messages[index].TryRead<LuaTable>(out var message))
                throw new FormatException("An example conversation entry is not a table.");
            ConfigurationImportFields.Enum<ChatRole>(message, "Role");
            if (string.IsNullOrWhiteSpace(ConfigurationImportFields.String(message, "Content")))
                throw new FormatException("An example conversation message is empty.");
        }
        if (table.TryGetValue("ToolIds", out _))
            ConfigurationImportFields.Strings(table, "ToolIds");
        if (table.TryGetValue("DataSourceOptions", out _))
        {
            var options = ConfigurationImportFields.Table(table, "DataSourceOptions");
            ConfigurationImportFields.Bool(options, "DisableDataSources");
            ConfigurationImportFields.Bool(options, "AutomaticDataSourceSelection");
            ConfigurationImportFields.Bool(options, "AutomaticValidation");
            if (options.TryGetValue("PreselectedDataSourceIds", out _))
                ConfigurationImportFields.Strings(options, "PreselectedDataSourceIds");
        }
        if (!ChatTemplate.TryParseChatTemplateTable(0, table, Guid.Empty, string.Empty, out _))
            throw new FormatException("The chat template fields are malformed.");
        ConfigurationImportFields.Strings(table, "FileAttachments");
    }

    private static void ValidateERIDataSource(LuaTable table)
    {
        if (ConfigurationImportFields.String(table, "Type") != "ERI_V1")
            throw new FormatException("This data source is not an ERI v1 data source.");
        ConfigurationImportFields.String(table, "Name");
        ConfigurationImportFields.String(table, "Hostname");
        var port = ConfigurationImportFields.Int(table, "Port");
        if (port is < 1 or > 65535)
            throw new FormatException("The 'Port' field must be between 1 and 65535.");
        var authMethod = ConfigurationImportFields.Enum<AuthMethod>(table, "AuthMethod");
        if (authMethod is AuthMethod.KERBEROS)
            throw new FormatException("Kerberos data sources cannot be imported from configuration snippets.");
        ConfigurationImportFields.Enum<DataSourceSecurity>(table, "SecurityPolicy");
        ConfigurationImportFields.String(table, "SelectedRetrievalId");
        var maxMatches = ConfigurationImportFields.Int(table, "MaxMatches", 10);
        if (maxMatches is < 1 or > ushort.MaxValue)
            throw new FormatException("The 'MaxMatches' field is outside the allowed range.");
        var secretName = authMethod switch
        {
            AuthMethod.TOKEN => "Token",
            AuthMethod.USERNAME_PASSWORD => "Password",
            _ => string.Empty,
        };
        if (!string.IsNullOrEmpty(secretName))
            ConfigurationImportFields.Credential(table, secretName, out _);
        ConfigurationImportFields.String(table, "Username", required: false);
    }
}
