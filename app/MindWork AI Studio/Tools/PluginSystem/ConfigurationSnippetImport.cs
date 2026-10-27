using System.Diagnostics.CodeAnalysis;

using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ERIClient.DataModel;
using AIStudio.Tools.ToolCallingSystem;

using Lua;

namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Turns a configuration snippet which somebody exported into a configuration object of the user's own.
/// </summary>
/// <remarks>
/// The snippet is read by the same parsers which read configuration plugins, so an import accepts
/// what a configuration plugin accepts, and there is no second description of the fields to keep in
/// step with the first. The parsers run as a dry run, which stores nothing. Afterward, the object is
/// made local: it gets an ID of its own and belongs to no configuration plugin. What the snippet
/// refers to beyond itself, a file or another configuration object, may not exist on this computer;
/// the user hears about it instead of losing the whole snippet.
/// </remarks>
public static class ConfigurationSnippetImport
{
    private const string FILE_ATTACHMENTS_FIELD = "FileAttachments";

    private const string API_KEY_FIELD = "APIKey";

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ConfigurationSnippetImport).Namespace, nameof(ConfigurationSnippetImport));

    /// <summary>
    /// Reads a pasted configuration snippet and makes its configuration object one of the user's own.
    /// </summary>
    /// <remarks>
    /// Secrets are decrypted with the encryption secret of the organization, if this computer has
    /// one. What the object refers to in the settings is checked by ResolveReferencesAsync.
    /// </remarks>
    /// <param name="snippet">The pasted snippet.</param>
    /// <param name="type">The type of configuration object the snippet must describe.</param>
    /// <param name="result">The imported configuration object, when the snippet could be imported.</param>
    /// <param name="issue">Why the snippet could not be imported, otherwise empty.</param>
    /// <returns>True when the snippet could be imported.</returns>
    public static bool TryImport(string snippet, PluginConfigurationObjectType type, [NotNullWhen(true)] out ConfigurationSnippetImportResult? result, out string issue)
    {
        result = null;
        if (!ConfigurationSnippetParser.TryParse(snippet, type, out var table, out issue))
            return false;

        //
        // The parser of chat templates looks up every attachment, on a network share as well. Since
        // a snippet comes from somebody else, that lookup would reach out to whatever share they
        // named as soon as the snippet was pasted. Hence, the attachments are taken out beforehand
        // and checked here, without touching a share:
        //
        var attachmentPaths = type is PluginConfigurationObjectType.CHAT_TEMPLATE ? TakeAttachmentPaths(table) : [];
        if (!PluginConfigurationObjectDescriptors.Get(type).TryParseSnippet(table, out var configObject))
        {
            issue = DescribeInvalidSnippet(type, table);
            return false;
        }

        var hints = new List<string>();
        switch (configObject)
        {
            case DataSourceERI_V1 dataSource:
                if (dataSource.AuthMethod is AuthMethod.KERBEROS)
                {
                    issue = TB("Data sources which sign in with Kerberos cannot be imported yet. Please add them yourself.");
                    return false;
                }

                // The parser already decrypted the secret once, so it succeeds here as well:
                TryReadSecret(table, DataSourceERI_V1.GetSecretFieldName(dataSource.AuthMethod), out var dataSourceSecret);
                result = new(MakeLocal(dataSource), dataSourceSecret, hints, []);
                return true;

            //
            // A user-provided API key, and asking the user for it, is what an organization grants for a
            // provider it manages. A provider of the user's own has nothing else, so neither option carries
            // any meaning here:
            //
            case Settings.Provider provider:
                result = new(MakeLocal(provider) with { TokenizerPath = KeepLocalTokenizer(provider.TokenizerPath, hints), AllowUserProvidedAPIKey = false, PromptForUserAPIKey = false }, ReadAPIKey(table, hints), hints, []);
                return true;

            case EmbeddingProvider provider:
                result = new(MakeLocal(provider) with { TokenizerPath = KeepLocalTokenizer(provider.TokenizerPath, hints), AllowUserProvidedAPIKey = false, PromptForUserAPIKey = false }, ReadAPIKey(table, hints), hints, []);
                return true;

            case TranscriptionProvider provider:
                result = new(MakeLocal(provider) with { AllowUserProvidedAPIKey = false, PromptForUserAPIKey = false }, ReadAPIKey(table, hints), hints, []);
                return true;

            //
            // A snippet may name any file of this computer, a private key just as well as a report.
            // Hence, the user gets to see which files the template would attach to every chat:
            //
            case ChatTemplate template:
                var localPaths = attachmentPaths.Where(IsLocalFile).ToList();
                if (localPaths.Count > 0)
                    hints.Add(string.Format(TB("The chat template attaches these files from this computer to every chat started with it: {0}. Please check that you want to share them."), string.Join(", ", localPaths)));

                result = new(MakeLocal(template) with { FileAttachments = localPaths.Select(FileAttachment.FromPath).ToList() }, string.Empty, hints, attachmentPaths.Except(localPaths).ToList());
                return true;

            case DataDocumentAnalysisPolicy policy:
                result = new(MakeLocal(policy) with { IsProtected = false }, string.Empty, hints, []);
                return true;

            case Profile profile:
                result = new(MakeLocal(profile), string.Empty, hints, []);
                return true;

            default:
                throw new NotSupportedException($"Configuration objects of the type {type} cannot be imported yet.");
        }
    }

    /// <summary>
    /// Checks what an imported configuration object refers to in the settings: the providers,
    /// profiles, data sources, and tools it preselects or permits.
    /// </summary>
    /// <remarks>
    /// The selected tools are normalized first, as every selection of tools is. A reference which
    /// cannot be found stays in the object, as it would in a configuration plugin: the snippet may
    /// stem from a computer which has more, and the user may add what is missing later.
    /// </remarks>
    /// <param name="result">What TryImport read from the snippet.</param>
    /// <param name="settingsManager">The settings in which the references must exist.</param>
    /// <param name="toolRegistry">The tools which are available.</param>
    /// <returns>The result with the tools normalized and a hint for each kind of missing reference.</returns>
    public static async Task<ConfigurationSnippetImportResult> ResolveReferencesAsync(ConfigurationSnippetImportResult result, SettingsManager settingsManager, ToolRegistry toolRegistry)
    {
        var hints = new List<string>(result.Hints);
        switch (result.ConfigurationObject)
        {
            case ChatTemplate template:
                var templateToolIds = template.ToolIds is null ? null : toolRegistry.NormalizeSelection(template.ToolIds);
                var missingDataSourceIds = (template.DataSourceOptions?.PreselectedDataSourceIds ?? [])
                    .Where(id => settingsManager.ConfigurationData.DataSources.All(dataSource => dataSource.Id != id))
                    .ToList();

                if (missingDataSourceIds.Count > 0)
                    hints.Add(string.Format(TB("The chat template preselects data sources which do not exist on this computer: {0}."), string.Join(", ", missingDataSourceIds)));

                var missingChatToolIds = await FindMissingToolIdsAsync(templateToolIds ?? [], toolRegistry, Components.CHAT);
                if (missingChatToolIds.Count > 0)
                    hints.Add(string.Format(TB("The chat template preselects tools which are not available on this computer: {0}."), string.Join(", ", missingChatToolIds)));

                if (template.ProviderId is not null)
                {
                    var templateProvider = settingsManager.GetProviderById(template.ProviderId);
                    if (templateProvider == Settings.Provider.NONE)
                        hints.Add(string.Format(TB("The chat template preselects a provider which does not exist on this computer: {0}. Chats started with it use your usual provider instead."), template.ProviderId));
                    else if (settingsManager.GetChatTemplateProvider(template.ProviderId) == Settings.Provider.NONE)
                        hints.Add(string.Format(TB("The chat template preselects the provider '{0}', which does not meet the confidence level required for chats. Chats started with it use your usual provider instead."), templateProvider.InstanceName));
                }

                if (template.ProfileId is not null && !template.AllowProfileUsage)
                    hints.Add(TB("The chat template preselects a profile but does not allow profiles. Chats started with it ignore that profile."));
                else if (template.ProfileId is not null && !template.StartsWithoutProfile() && settingsManager.GetProfileById(template.ProfileId) == Profile.NO_PROFILE)
                    hints.Add(string.Format(TB("The chat template preselects a profile which does not exist on this computer: {0}. Chats started with it use your usual profile instead."), template.ProfileId));

                return result with { ConfigurationObject = template with { ToolIds = templateToolIds }, Hints = hints };

            case DataDocumentAnalysisPolicy policy:
                var allowedToolIds = toolRegistry.NormalizeSelection(policy.AllowedToolIds);
                if (!string.IsNullOrWhiteSpace(policy.PreselectedProvider) && settingsManager.GetProviderById(policy.PreselectedProvider) == Settings.Provider.NONE)
                    hints.Add(string.Format(TB("The policy preselects a provider which does not exist on this computer: {0}."), policy.PreselectedProvider));

                if (!string.IsNullOrWhiteSpace(policy.PreselectedProfile) && policy.PreselectedProfile != Profile.NO_PROFILE.Id && settingsManager.ConfigurationData.Profiles.All(profile => profile.Id != policy.PreselectedProfile))
                    hints.Add(string.Format(TB("The policy preselects a profile which does not exist on this computer: {0}."), policy.PreselectedProfile));

                var missingPolicyToolIds = await FindMissingToolIdsAsync(allowedToolIds, toolRegistry, Components.DOCUMENT_ANALYSIS_ASSISTANT);
                if (missingPolicyToolIds.Count > 0)
                    hints.Add(string.Format(TB("The policy permits tools which are not available on this computer: {0}."), string.Join(", ", missingPolicyToolIds)));

                return result with { ConfigurationObject = policy with { AllowedToolIds = allowedToolIds }, Hints = hints };

            default:
                return result;
        }
    }

    private static async Task<List<string>> FindMissingToolIdsAsync(IReadOnlyCollection<string> toolIds, ToolRegistry toolRegistry, Components component)
    {
        if (toolIds.Count == 0)
            return [];

        // A tool the user switched off exists here all the same, and switching it on again makes the import use it:
        var availableToolIds = (await toolRegistry.GetCatalogAsync(component)).Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        return toolIds.Where(id => !availableToolIds.Contains(id) && !toolRegistry.IsSwitchedOffByUser(id)).Order(StringComparer.Ordinal).ToList();
    }

    private static T MakeLocal<T>(T configObject) where T : ConfigurationBaseObject => (T)((ConfigurationBaseObject)configObject with
    {
        Id = Guid.NewGuid().ToString(),
        Num = 0,
        IsEnterpriseConfiguration = false,
        EnterpriseConfigurationPluginId = Guid.Empty,
    });

    private static DataSourceERI_V1 MakeLocal(DataSourceERI_V1 dataSource) => dataSource with
    {
        Id = Guid.NewGuid().ToString(),
        Num = 0,
        IsEnterpriseConfiguration = false,
        EnterpriseConfigurationPluginId = Guid.Empty,

        // Only an organization manages usernames and passwords for its users, so the user manages them here:
        UsernamePasswordMode = DataSourceERIUsernamePasswordMode.USER_MANAGED,
        Username = dataSource.Username == DataSourceERI_V1.SHARED_USERNAME_PLACEHOLDER ? string.Empty : dataSource.Username,
    };

    private static List<string> TakeAttachmentPaths(LuaTable table)
    {
        var paths = new List<string>();
        if (table.TryGetValue(FILE_ATTACHMENTS_FIELD, out var attachmentsValue) && attachmentsValue.TryRead<LuaTable>(out var attachments))
        {
            for (var index = 1; index <= attachments.ArrayLength; index++)
            {
                if (attachments[index].TryRead<string>(out var path) && !string.IsNullOrWhiteSpace(path) && !paths.Contains(path))
                    paths.Add(path);
            }
        }

        table[FILE_ATTACHMENTS_FIELD] = LuaValue.Nil;
        return paths;
    }

    /// <remarks>
    /// An export names the tokenizer relative to the data directory, where the copy of the provider
    /// lies on the computer it came from. Imported there, the path leads back to that copy; anywhere
    /// else, the copy is missing.
    /// </remarks>
    private static string KeepLocalTokenizer(string tokenizerPath, List<string> hints)
    {
        if (string.IsNullOrWhiteSpace(tokenizerPath))
            return tokenizerPath;

        var localPath = ExportedTokenizerPath.ToLocal(tokenizerPath, SettingsManager.DataDirectory);
        if (IsLocalFile(localPath))
            return localPath;

        hints.Add(string.Format(TB("The tokenizer '{0}' is not on this computer, so the provider is imported without it."), tokenizerPath));
        return string.Empty;
    }

    /// <summary>
    /// Whether a path names a file on this computer. A path on a network share never does: looking
    /// it up would already connect to the share.
    /// </summary>
    private static bool IsLocalFile(string path) => Path.IsPathFullyQualified(path) && !path.StartsWith(@"\\", StringComparison.Ordinal) && !path.StartsWith("//", StringComparison.Ordinal) && File.Exists(path);

    /// <remarks>
    /// Unlike the secret of a data source, an API key is no part of what makes a provider valid, so
    /// a key which cannot be read leaves only a hint: the user enters a key of their own instead.
    /// </remarks>
    private static string ReadAPIKey(LuaTable table, List<string> hints)
    {
        var encryptedApiKey = ReadString(table, API_KEY_FIELD);
        if (string.IsNullOrWhiteSpace(encryptedApiKey))
            return string.Empty;

        if (!EnterpriseEncryption.IsEncrypted(encryptedApiKey))
        {
            hints.Add(TB("The pasted configuration contains an API key which is not encrypted, so it was left out. Please enter your API key before you save the provider."));
            return string.Empty;
        }

        if (TryReadSecret(table, API_KEY_FIELD, out var apiKey))
            return apiKey;

        hints.Add(TB("The API key in the pasted configuration cannot be decrypted on this computer. Please enter your API key before you save the provider."));
        return string.Empty;
    }

    private static bool TryReadSecret(LuaTable table, string fieldName, out string secret)
    {
        secret = string.Empty;
        var encryptedSecret = ReadString(table, fieldName);
        if (!EnterpriseEncryption.IsEncrypted(encryptedSecret))
            return false;

        var encryption = PluginFactory.EnterpriseEncryption;
        if (encryption?.IsAvailable != true || !encryption.TryDecrypt(encryptedSecret, out var decryptedSecret))
            return false;

        secret = decryptedSecret;
        return true;
    }

    private static string ReadString(LuaTable table, string fieldName) => !string.IsNullOrWhiteSpace(fieldName) && table.TryGetValue(fieldName, out var value) && value.TryRead<string>(out var text) ? text : string.Empty;

    private static string DescribeInvalidSnippet(PluginConfigurationObjectType type, LuaTable table)
    {
        //
        // A data source which signs in with a secret is only valid where its secret can be
        // decrypted. That is no fault of the snippet, so the user learns the actual reason:
        //
        if (type is PluginConfigurationObjectType.DATA_SOURCE && Enum.TryParse<AuthMethod>(ReadString(table, "AuthMethod"), true, out var authMethod))
        {
            var secretFieldName = DataSourceERI_V1.GetSecretFieldName(authMethod);
            if (EnterpriseEncryption.IsEncrypted(ReadString(table, secretFieldName)) && !TryReadSecret(table, secretFieldName, out _))
                return TB("The token or password of the data source cannot be decrypted on this computer. Such data sources can only be imported where the encryption secret of your organization is configured.");
        }

        return string.Format(TB("The pasted configuration is not a valid configuration of the type '{0}'."), type.GetName());
    }
}