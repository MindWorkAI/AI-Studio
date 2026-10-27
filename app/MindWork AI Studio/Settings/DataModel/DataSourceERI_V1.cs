// ReSharper disable InconsistentNaming

using AIStudio.Assistants.ERI;
using AIStudio.Chat;
using AIStudio.Tools.ERIClient;
using AIStudio.Tools.ERIClient.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.RAG;
using AIStudio.Tools.Services;
using AIStudio.Tools.Validation;

using SharedTools;

using ChatThread = AIStudio.Chat.ChatThread;
using ContentType = AIStudio.Tools.ERIClient.DataModel.ContentType;
using LuaTable = Lua.LuaTable;

namespace AIStudio.Settings.DataModel;

/// <summary>
/// An external data source, accessed via an ERI server, cf. https://github.com/MindWorkAI/ERI.
/// </summary>
public readonly record struct DataSourceERI_V1 : IERIDataSource
{
    private static readonly ILogger<DataSourceERI_V1> LOGGER = Program.LOGGER_FACTORY.CreateLogger<DataSourceERI_V1>();

    /// <summary>
    /// What an export writes as the shared username when the data source has none to export.
    /// </summary>
    public const string SHARED_USERNAME_PLACEHOLDER = "<shared username>";

    public DataSourceERI_V1()
    {
    }
    
    /// <inheritdoc />
    public uint Num { get; init; }

    /// <inheritdoc />
    public string Id { get; init; } = Guid.Empty.ToString();
    
    /// <inheritdoc />
    public string Name { get; init; } = string.Empty;
    
    /// <inheritdoc />
    public DataSourceType Type { get; init; } = DataSourceType.NONE;
    
    /// <inheritdoc />
    public string Hostname { get; init; } = string.Empty;
    
    /// <inheritdoc />
    public int Port { get; init; }

    /// <inheritdoc />
    public AuthMethod AuthMethod { get; init; } = AuthMethod.NONE;

    /// <inheritdoc />
    public string Username { get; init; } = string.Empty;

    /// <inheritdoc />
    public DataSourceERIUsernamePasswordMode UsernamePasswordMode { get; init; } = DataSourceERIUsernamePasswordMode.USER_MANAGED;

    /// <inheritdoc />
    public DataSourceSecurity SecurityPolicy { get; init; } = DataSourceSecurity.NOT_SPECIFIED;

    /// <inheritdoc />
    public OutboundDataRestriction OutboundDataRestriction { get; init; } = DataSourceOutboundDataExtensions.DEFAULT_OUTBOUND_DATA_RESTRICTION;

    /// <inheritdoc />
    public bool IsEnterpriseConfiguration { get; init; }

    /// <inheritdoc />
    public Guid EnterpriseConfigurationPluginId { get; init; } = Guid.Empty;
    
    /// <inheritdoc />
    public ERIVersion Version { get; init; } = ERIVersion.V1;
    
    /// <inheritdoc />
    public string SelectedRetrievalId { get; init; } = string.Empty;

    /// <inheritdoc />
    public ushort MaxMatches { get; init; } = 10;
    
    /// <inheritdoc />
    public async Task<IReadOnlyList<IRetrievalContext>> RetrieveDataAsync(IContent lastUserPrompt, ChatThread thread, CancellationToken token = default)
    {
        var latestUserPrompt = lastUserPrompt switch
        {
            ContentText text => text.Text,
            ContentImage image => await image.TryAsBase64(token) is (success: true, { } base64Image)
                ? base64Image
                : string.Empty,
            _ => string.Empty
        };

        return await this.RetrieveDataAsync(latestUserPrompt, lastUserPrompt.ToERIContentType, thread, this.MaxMatches, token) ?? [];
    }

    /// <inheritdoc />
    /// <remarks>
    /// An ERI data source has a single page. ERI v1 knows no offset, so a further page would have
    /// to ask the server for all pages before it as well. A server may refuse more matches than its
    /// retrieval process allows, which often is the very number configured here: it would refuse
    /// every further page, and the model would hear that the data source could not be searched
    /// rather than that there is nothing more.
    /// </remarks>
    public int GetLastPage() => 1;

    /// <inheritdoc />
    public async Task<RetrievalPage> RetrieveDataAsync(string query, int page, ChatThread thread, CancellationToken token = default)
    {
        var lastPage = this.GetLastPage();
        if (page < 1 || page > lastPage)
            throw new ArgumentOutOfRangeException(nameof(page), page, $"An ERI data source has pages from 1 to {lastPage}.");

        if (this.MaxMatches == 0)
            return RetrievalPage.EMPTY;

        //
        // ERI v1 knows no query apart from the latest user prompt, so the query takes its place; the
        // thread still tells the server what the conversation is about. Nor does it know an offset,
        // so the data source has a single page, asking for exactly what the classic RAG process asks
        // for, see GetLastPage:
        //
        var contexts = await this.RetrieveDataAsync(query, ContentType.TEXT, thread, this.MaxMatches, token);
        if (contexts is null)
            return RetrievalPage.EMPTY with { Gaps = [RetrievalGap.NOT_SEARCHED] };

        return new RetrievalPage(contexts.Take(this.MaxMatches).ToList(), false);
    }

    /// <returns>What the ERI server found, or null when it could not be searched.</returns>
    private async Task<IReadOnlyList<IRetrievalContext>?> RetrieveDataAsync(string latestUserPrompt, ContentType latestUserPromptType, ChatThread thread, int maxMatches, CancellationToken token)
    {
        // Important: Do not dispose the RustService here, as it is a singleton.
        var rustService = Program.SERVICE_PROVIDER.GetRequiredService<RustService>();
        var logger = Program.SERVICE_PROVIDER.GetRequiredService<ILogger<DataSourceERI_V1>>();
        
        using var eriClient = ERIClientFactory.Get(this.Version, this)!;
        var authResponse = await eriClient.AuthenticateAsync(rustService, cancellationToken: token);
        if (authResponse.Successful)
        {
            var retrievalRequest = new RetrievalRequest
            {
                LatestUserPromptType = latestUserPromptType,
                LatestUserPrompt = latestUserPrompt,
                
                Thread = await thread.ToERIChatThread(token),
                MaxMatches = maxMatches,
                RetrievalProcessId = this.SelectedRetrievalId,
                Parameters = null, // The ERI server selects useful default parameters
            };
            
            var retrievalResponse = await eriClient.ExecuteRetrievalAsync(retrievalRequest, token);
            if(retrievalResponse is { Successful: true, Data: not null })
            {
                //
                // Next, we have to transform the ERI context back to our generic retrieval context.
                // The server writes the JSON, and a field it leaves out arrives as null, whatever the
                // contract says. Everything after this point counts on texts and lists:
                //
                var genericRetrievalContexts = new List<IRetrievalContext>(retrievalResponse.Data.Count);
                foreach (var eriContext in retrievalResponse.Data)
                {
                    switch (eriContext.Type)
                    {
                        case ContentType.TEXT:
                            genericRetrievalContexts.Add(new RetrievalTextContext
                            {
                                Path = eriContext.Path ?? string.Empty,
                                Type = eriContext.ToRetrievalContentType(),
                                Links = eriContext.Links ?? [],
                                Category = eriContext.Type.ToRetrievalContentCategory(),
                                SourceCategory = eriContext.Category ?? string.Empty,
                                MatchedText = eriContext.MatchedContent ?? string.Empty,
                                DataSourceName = eriContext.Name ?? string.Empty,
                                SurroundingContent = eriContext.SurroundingContent ?? [],
                            });
                            break;

                        case ContentType.IMAGE:
                            genericRetrievalContexts.Add(new RetrievalImageContext
                            {
                                Path = eriContext.Path ?? string.Empty,
                                Type = eriContext.ToRetrievalContentType(),
                                Links = eriContext.Links ?? [],
                                Source = eriContext.MatchedContent ?? string.Empty,
                                Category = eriContext.Type.ToRetrievalContentCategory(),
                                SourceCategory = eriContext.Category ?? string.Empty,
                                SourceType = ContentImageSource.BASE64,
                                DataSourceName = eriContext.Name ?? string.Empty,
                            });
                            break;
                        
                        default:
                            logger.LogWarning($"The ERI context type '{eriContext.Type}' is not supported yet.");
                            break;
                    }
                }

                return genericRetrievalContexts;
            }

            //
            // The ERI client answers a cancellation like any other failure, since other callers
            // pass their own timeout and expect an answer. Whoever cancelled the search, though,
            // waits for it to stop; reporting the data source as not searched would carry on:
            //
            token.ThrowIfCancellationRequested();
            logger.LogWarning($"Was not able to retrieve data from the ERI data source '{this.Name}'. Message: {retrievalResponse.Message}");
            return null;
        }

        token.ThrowIfCancellationRequested();
        logger.LogWarning($"Was not able to authenticate with the ERI data source '{this.Name}'. Message: {authResponse.Message}");
        return null;
    }

    public static bool TryParseConfiguration(int idx, LuaTable table, Guid configPluginId, bool dryRun, out DataSourceERI_V1 dataSource)
    {
        dataSource = default;
        if (!table.TryGetValue("Id", out var idValue) || !idValue.TryRead<string>(out var idText) || !Guid.TryParse(idText, out var id))
        {
            LOGGER.LogWarning($"The configured data source {idx} does not contain a valid ID. The ID must be a valid GUID. (Plugin ID: {configPluginId})");
            return false;
        }

        if (!table.TryGetValue("Name", out var nameValue) || !nameValue.TryRead<string>(out var name) || !DataSourceValidation.IsNameValid(name))
        {
            LOGGER.LogWarning($"The configured data source {idx} does not contain a valid name of at most {DataSourceValidation.MAX_NAME_LENGTH} characters without control characters. (Plugin ID: {configPluginId})");
            return false;
        }

        if (!table.TryGetValue("Type", out var typeValue) || !typeValue.TryRead<string>(out var typeText) || !Enum.TryParse<DataSourceType>(typeText, true, out var type) || type is not DataSourceType.ERI_V1)
        {
            LOGGER.LogWarning($"The configured data source {idx} does not contain a supported data source type. Only ERI_V1 is supported. (Plugin ID: {configPluginId})");
            return false;
        }

        if (!table.TryGetValue("Hostname", out var hostnameValue) || !hostnameValue.TryRead<string>(out var hostname) || string.IsNullOrWhiteSpace(hostname))
        {
            LOGGER.LogWarning($"The configured data source {idx} does not contain a valid hostname. (Plugin ID: {configPluginId})");
            return false;
        }

        if (!table.TryGetValue("Port", out var portValue) || !portValue.TryRead<int>(out var port) || port is < 1 or > 65535)
        {
            LOGGER.LogWarning($"The configured data source {idx} does not contain a valid port. (Plugin ID: {configPluginId})");
            return false;
        }

        if (!table.TryGetValue("AuthMethod", out var authMethodValue) || !authMethodValue.TryRead<string>(out var authMethodText) || !Enum.TryParse<AuthMethod>(authMethodText, true, out var authMethod))
        {
            LOGGER.LogWarning($"The configured data source {idx} does not contain a valid auth method. (Plugin ID: {configPluginId})");
            return false;
        }

        if (!table.TryGetValue("SecurityPolicy", out var securityPolicyValue) || !securityPolicyValue.TryRead<string>(out var securityPolicyText) || !Enum.TryParse<DataSourceSecurity>(securityPolicyText, true, out var securityPolicy))
        {
            LOGGER.LogWarning($"The configured data source {idx} does not contain a valid security policy. (Plugin ID: {configPluginId})");
            return false;
        }

        if (securityPolicy is DataSourceSecurity.NOT_SPECIFIED)
        {
            LOGGER.LogWarning($"The configured data source {idx} must specify a security policy. (Plugin ID: {configPluginId})");
            return false;
        }

        //
        // Optional, so that configurations written before the setting existed stay valid; their data
        // sources get the level of a new one. A value this version cannot read counts as the
        // strictest level, as in the settings file. Only a name counts, never a number, see EnumNames.
        // A data source for self-hosted providers only is not clamped here: the level applies when
        // its passages reach a chat, see DataSourceOutboundDataExtensions.GetEffectiveOutboundDataRestriction.
        //
        var outboundDataRestriction = DataSourceOutboundDataExtensions.DEFAULT_OUTBOUND_DATA_RESTRICTION;
        if (table.TryGetValue("OutboundDataRestriction", out var outboundDataRestrictionValue) && !(outboundDataRestrictionValue.TryRead<string>(out var outboundDataRestrictionText) && EnumNames.TryParse(outboundDataRestrictionText, out outboundDataRestriction)))
        {
            LOGGER.LogWarning($"The configured data source {idx} does not contain a valid outbound data restriction, so the strictest one applies. (Plugin ID: {configPluginId})");
            outboundDataRestriction = OutboundDataRestriction.ONLY_CONFIGURED_SERVICES;
        }

        if (!table.TryGetValue("SelectedRetrievalId", out var selectedRetrievalIdValue) || !selectedRetrievalIdValue.TryRead<string>(out var selectedRetrievalId) || string.IsNullOrWhiteSpace(selectedRetrievalId))
        {
            LOGGER.LogWarning($"The configured data source {idx} must specify a selected retrieval ID. (Plugin ID: {configPluginId})");
            return false;
        }

        if (!table.TryGetValue("MaxMatches", out var maxMatchesValue) || !maxMatchesValue.TryRead<int>(out var maxMatches) || maxMatches is < 1 or > ushort.MaxValue)
        {
            LOGGER.LogWarning($"The configured data source {idx} does not contain a valid maximum number of matches. (Plugin ID: {configPluginId})");
            return false;
        }

        var username = string.Empty;
        var usernamePasswordMode = DataSourceERIUsernamePasswordMode.USER_MANAGED;
        if (table.TryGetValue("UsernamePasswordMode", out var usernamePasswordModeValue) && usernamePasswordModeValue.TryRead<string>(out var usernamePasswordModeText))
        {
            if (!Enum.TryParse(usernamePasswordModeText, true, out usernamePasswordMode))
            {
                LOGGER.LogWarning($"The configured data source {idx} does not contain a valid username/password mode. (Plugin ID: {configPluginId})");
                return false;
            }

            if (usernamePasswordMode is DataSourceERIUsernamePasswordMode.USER_MANAGED)
            {
                LOGGER.LogWarning($"The configured data source {idx} uses the user-managed username/password mode. This mode is not allowed in configuration plugins. (Plugin ID: {configPluginId})");
                return false;
            }
        }

        if (authMethod is AuthMethod.USERNAME_PASSWORD)
        {
            if (!table.TryGetValue("UsernamePasswordMode", out _) || usernamePasswordMode is DataSourceERIUsernamePasswordMode.USER_MANAGED)
            {
                LOGGER.LogWarning($"The configured data source {idx} must specify an organization-managed username/password mode. (Plugin ID: {configPluginId})");
                return false;
            }

            if (usernamePasswordMode is DataSourceERIUsernamePasswordMode.SHARED_USERNAME_AND_PASSWORD &&
                (!table.TryGetValue("Username", out var usernameValue) || !usernameValue.TryRead<string>(out username) || string.IsNullOrWhiteSpace(username)))
            {
                LOGGER.LogWarning($"The configured data source {idx} must specify a username. (Plugin ID: {configPluginId})");
                return false;
            }
        }

        dataSource = new DataSourceERI_V1
        {
            Num = 0,
            Id = id.ToString(),
            Name = name,
            Type = DataSourceType.ERI_V1,
            Hostname = CleanHostname(hostname),
            Port = port,
            AuthMethod = authMethod,
            Username = username,
            UsernamePasswordMode = usernamePasswordMode,
            SecurityPolicy = securityPolicy,
            OutboundDataRestriction = outboundDataRestriction,
            Version = ERIVersion.V1,
            SelectedRetrievalId = selectedRetrievalId,
            MaxMatches = (ushort)maxMatches,
            IsEnterpriseConfiguration = true,
            EnterpriseConfigurationPluginId = configPluginId,
        };

        return TryQueueEnterpriseSecret(idx, table, configPluginId, dataSource, dryRun);
    }

    /// <summary>
    /// Exports the ERI v1 data source configuration as a Lua configuration section.
    /// </summary>
    /// <param name="encryptedSecret">Optional encrypted token or password to include in the export.</param>
    /// <param name="usernamePasswordMode">The organization-managed username/password mode to export.</param>
    /// <returns>A Lua configuration section string.</returns>
    public string ExportAsConfigurationSection(string? encryptedSecret = null, DataSourceERIUsernamePasswordMode usernamePasswordMode = DataSourceERIUsernamePasswordMode.USER_MANAGED)
    {
        var secretLine = string.Empty;
        var usernamePasswordModeLine = string.Empty;
        var usernameLine = string.Empty;

        switch (this.AuthMethod)
        {
            case AuthMethod.TOKEN:
                secretLine = CreateSecretLine(GetSecretFieldName(AuthMethod.TOKEN), encryptedSecret);
                break;

            case AuthMethod.USERNAME_PASSWORD:
                if (usernamePasswordMode is DataSourceERIUsernamePasswordMode.USER_MANAGED)
                    usernamePasswordMode = DataSourceERIUsernamePasswordMode.OS_USERNAME_SHARED_PASSWORD;

                usernamePasswordModeLine = $"""
                                           ["UsernamePasswordMode"] = "{usernamePasswordMode}",
                                           """;

                if (usernamePasswordMode is DataSourceERIUsernamePasswordMode.SHARED_USERNAME_AND_PASSWORD)
                {
                    var username = string.IsNullOrWhiteSpace(this.Username) ? SHARED_USERNAME_PLACEHOLDER : this.Username;
                    usernameLine = $"""
                                   ["Username"] = "{LuaTools.EscapeLuaString(username)}",
                                   """;
                }

                secretLine = CreateSecretLine(GetSecretFieldName(AuthMethod.USERNAME_PASSWORD), encryptedSecret);
                break;
        }

        return $$"""
                CONFIG["DATA_SOURCES"][#CONFIG["DATA_SOURCES"]+1] = {
                    ["Id"] = "{{Guid.NewGuid().ToString()}}",
                    ["Name"] = "{{LuaTools.EscapeLuaString(this.Name)}}",
                    ["Type"] = "ERI_V1",
                    ["Hostname"] = "{{LuaTools.EscapeLuaString(this.Hostname)}}",
                    ["Port"] = {{this.Port}},
                    ["AuthMethod"] = "{{this.AuthMethod}}",
                    {{usernamePasswordModeLine}}
                    {{usernameLine}}
                    {{secretLine}}
                    ["SecurityPolicy"] = "{{this.SecurityPolicy}}",
                    ["OutboundDataRestriction"] = "{{this.OutboundDataRestriction}}",
                    ["SelectedRetrievalId"] = "{{LuaTools.EscapeLuaString(this.SelectedRetrievalId)}}",
                    ["MaxMatches"] = {{this.MaxMatches}},
                }
                """;
    }

    /// <summary>
    /// The field of a configuration which holds the encrypted secret of an auth method.
    /// </summary>
    /// <param name="authMethod">How the data source signs in.</param>
    /// <returns>The name of the field, or an empty string for an auth method without a secret.</returns>
    public static string GetSecretFieldName(AuthMethod authMethod) => authMethod switch
    {
        AuthMethod.TOKEN => "Token",
        AuthMethod.USERNAME_PASSWORD => "Password",
        _ => string.Empty,
    };

    private static bool TryQueueEnterpriseSecret(int idx, LuaTable table, Guid configPluginId, DataSourceERI_V1 dataSource, bool dryRun)
    {
        var secretFieldName = GetSecretFieldName(dataSource.AuthMethod);
        if (string.IsNullOrWhiteSpace(secretFieldName))
            return true;

        if (!table.TryGetValue(secretFieldName, out var secretValue) || !secretValue.TryRead<string>(out var encryptedSecret) || string.IsNullOrWhiteSpace(encryptedSecret))
        {
            LOGGER.LogWarning($"The configured data source {idx} does not contain a valid encrypted {secretFieldName}. (Plugin ID: {configPluginId})");
            return false;
        }

        if (!EnterpriseEncryption.IsEncrypted(encryptedSecret))
        {
            LOGGER.LogWarning($"The configured data source {idx} contains a plaintext {secretFieldName}. Only encrypted secrets (starting with 'ENC:') are supported. (Plugin ID: {configPluginId})");
            return false;
        }

        var encryption = PluginFactory.EnterpriseEncryption;
        if (encryption?.IsAvailable != true)
        {
            LOGGER.LogWarning($"The configured data source {idx} contains an encrypted {secretFieldName}, but no encryption secret is configured. (Plugin ID: {configPluginId})");
            return false;
        }

        if (!encryption.TryDecrypt(encryptedSecret, out var decryptedSecret))
        {
            LOGGER.LogWarning($"Failed to decrypt the {secretFieldName} for data source {idx}. The encryption secret may be incorrect. (Plugin ID: {configPluginId})");
            return false;
        }

        //
        // A dry run only checks the configuration, so it queues nothing: the start which follows it
        // would otherwise store the secret once more for every dry run before it:
        //
        if (dryRun)
            return true;

        PendingEnterpriseSecrets.Add(new(
            $"{ISecretId.ENTERPRISE_KEY_PREFIX}::{dataSource.Id}",
            dataSource.Name,
            decryptedSecret,
            SecretStoreType.DATA_SOURCE));
        LOGGER.LogDebug($"Successfully decrypted the {secretFieldName} for data source {idx}. It will be stored in the OS keyring. (Plugin ID: {configPluginId})");
        return true;
    }

    private static string CreateSecretLine(string fieldName, string? encryptedSecret)
    {
        if (string.IsNullOrWhiteSpace(encryptedSecret))
            return string.Empty;

        return $"""
               ["{fieldName}"] = "{LuaTools.EscapeLuaString(encryptedSecret)}",
               """;
    }

    private static string CleanHostname(string hostname)
    {
        var cleanedHostname = hostname.Trim();
        return cleanedHostname.EndsWith('/') ? cleanedHostname[..^1] : cleanedHostname;
    }
}