// ReSharper disable InconsistentNaming

using AIStudio.Assistants.ERI;
using AIStudio.Chat;
using AIStudio.Tools.ERIClient;
using AIStudio.Tools.ERIClient.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.RAG;
using AIStudio.Tools.Services;

using Lua;

using ChatThread = AIStudio.Chat.ChatThread;
using ContentType = AIStudio.Tools.ERIClient.DataModel.ContentType;

namespace AIStudio.Settings.DataModel;

/// <summary>
/// An external data source, accessed via an ERI server, cf. https://github.com/MindWorkAI/ERI.
/// </summary>
public readonly record struct DataSourceERI_V1 : IERIDataSource
{
    private static readonly ILogger<DataSourceERI_V1> LOGGER = Program.LOGGER_FACTORY.CreateLogger<DataSourceERI_V1>();

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
        // Important: Do not dispose the RustService here, as it is a singleton.
        var rustService = Program.SERVICE_PROVIDER.GetRequiredService<RustService>();
        var logger = Program.SERVICE_PROVIDER.GetRequiredService<ILogger<DataSourceERI_V1>>();
        
        using var eriClient = ERIClientFactory.Get(this.Version, this)!;
        var authResponse = await eriClient.AuthenticateAsync(rustService, cancellationToken: token);
        if (authResponse.Successful)
        {
            var retrievalRequest = new RetrievalRequest
            {
                LatestUserPromptType = lastUserPrompt.ToERIContentType,
                LatestUserPrompt = lastUserPrompt switch
                {
                    ContentText text => text.Text,
                    ContentImage image => await image.TryAsBase64(token) is (success: true, { } base64Image)
                        ? base64Image 
                        : string.Empty,
                    _ => string.Empty
                },
                
                Thread = await thread.ToERIChatThread(token),
                MaxMatches = this.MaxMatches,
                RetrievalProcessId = this.SelectedRetrievalId,
                Parameters = null, // The ERI server selects useful default parameters
            };
            
            var retrievalResponse = await eriClient.ExecuteRetrievalAsync(retrievalRequest, token);
            if(retrievalResponse is { Successful: true, Data: not null })
            {
                //
                // Next, we have to transform the ERI context back to our generic retrieval context:
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
                                Links = eriContext.Links,
                                Category = eriContext.Type.ToRetrievalContentCategory(),
                                MatchedText = eriContext.MatchedContent,
                                DataSourceName = eriContext.Name,
                                SurroundingContent = eriContext.SurroundingContent,
                            });
                            break;
                        
                        case ContentType.IMAGE:
                            genericRetrievalContexts.Add(new RetrievalImageContext
                            {
                                Path = eriContext.Path ?? string.Empty,
                                Type = eriContext.ToRetrievalContentType(),
                                Links = eriContext.Links,
                                Source = eriContext.MatchedContent,
                                Category = eriContext.Type.ToRetrievalContentCategory(),
                                SourceType = ContentImageSource.BASE64,
                                DataSourceName = eriContext.Name,
                            });
                            break;
                        
                        default:
                            logger.LogWarning($"The ERI context type '{eriContext.Type}' is not supported yet.");
                            break;
                    }
                }

                return genericRetrievalContexts;
            }

            logger.LogWarning($"Was not able to retrieve data from the ERI data source '{this.Name}'. Message: {retrievalResponse.Message}");
            return [];
        }

        logger.LogWarning($"Was not able to authenticate with the ERI data source '{this.Name}'. Message: {authResponse.Message}");
        return [];
    }

    public static bool TryParseConfiguration(int idx, LuaTable table, Guid configPluginId, out DataSourceERI_V1 dataSource)
    {
        dataSource = default;
        if (!table.TryGetValue("Id", out var idValue) || !idValue.TryRead<string>(out var idText) || !Guid.TryParse(idText, out var id))
        {
            LOGGER.LogWarning($"The configured data source {idx} does not contain a valid ID. The ID must be a valid GUID. (Plugin ID: {configPluginId})");
            return false;
        }

        if (!table.TryGetValue("Name", out var nameValue) || !nameValue.TryRead<string>(out var name) || string.IsNullOrWhiteSpace(name))
        {
            LOGGER.LogWarning($"The configured data source {idx} does not contain a valid name. (Plugin ID: {configPluginId})");
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
            Version = ERIVersion.V1,
            SelectedRetrievalId = selectedRetrievalId,
            MaxMatches = (ushort)maxMatches,
            IsEnterpriseConfiguration = true,
            EnterpriseConfigurationPluginId = configPluginId,
        };

        return TryQueueEnterpriseSecret(idx, table, configPluginId, dataSource);
    }

    /// <summary>
    /// Exports the ERI v1 data source configuration as a Lua configuration section.
    /// </summary>
    /// <param name="encryptedSecret">Optional encrypted token or password to include in the export.</param>
    /// <param name="usernamePasswordMode">The organization-managed username/password mode to export.</param>
    /// <param name="useSecretPlaceholder">Whether to include a placeholder for the encrypted secret.</param>
    /// <returns>A Lua configuration section string.</returns>
    public string ExportAsConfigurationSection(string? encryptedSecret = null, DataSourceERIUsernamePasswordMode usernamePasswordMode = DataSourceERIUsernamePasswordMode.USER_MANAGED, bool useSecretPlaceholder = false)
    {
        var secretLine = string.Empty;
        var usernamePasswordModeLine = string.Empty;
        var usernameLine = string.Empty;

        switch (this.AuthMethod)
        {
            case AuthMethod.TOKEN:
                secretLine = CreateSecretLine("Token", encryptedSecret, "ENC:v1:<base64-encoded encrypted token>", useSecretPlaceholder);
                break;

            case AuthMethod.USERNAME_PASSWORD:
                if (usernamePasswordMode is DataSourceERIUsernamePasswordMode.USER_MANAGED)
                    usernamePasswordMode = DataSourceERIUsernamePasswordMode.OS_USERNAME_SHARED_PASSWORD;

                usernamePasswordModeLine = $"""
                                           ["UsernamePasswordMode"] = "{usernamePasswordMode}",
                                           """;

                if (usernamePasswordMode is DataSourceERIUsernamePasswordMode.SHARED_USERNAME_AND_PASSWORD)
                {
                    var username = string.IsNullOrWhiteSpace(this.Username) ? "<shared username>" : this.Username;
                    usernameLine = $"""
                                   ["Username"] = "{LuaTools.EscapeLuaString(username)}",
                                   """;
                }

                secretLine = CreateSecretLine("Password", encryptedSecret, "ENC:v1:<base64-encoded encrypted password>", useSecretPlaceholder);
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
                    ["SelectedRetrievalId"] = "{{LuaTools.EscapeLuaString(this.SelectedRetrievalId)}}",
                    ["MaxMatches"] = {{this.MaxMatches}},
                }
                """;
    }

    private static bool TryQueueEnterpriseSecret(int idx, LuaTable table, Guid configPluginId, DataSourceERI_V1 dataSource)
    {
        var secretFieldName = dataSource.AuthMethod switch
        {
            AuthMethod.TOKEN => "Token",
            AuthMethod.USERNAME_PASSWORD => "Password",
            _ => string.Empty,
        };

        if (string.IsNullOrWhiteSpace(secretFieldName))
            return true;

        if (!table.TryGetValue(secretFieldName, out var secretValue) || !secretValue.TryRead<string>(out var encryptedSecret) || string.IsNullOrWhiteSpace(encryptedSecret))
        {
            LOGGER.LogWarning($"The configured data source {idx} does not contain a valid encrypted {secretFieldName}. (Plugin ID: {configPluginId})");
            return false;
        }

        if (!EnterpriseEncryption.IsEncrypted(encryptedSecret))
        {
            LOGGER.LogWarning($"The configured data source {idx} contains a plaintext {secretFieldName}. Only encrypted secrets (starting with 'ENC:v1:') are supported. (Plugin ID: {configPluginId})");
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

        PendingEnterpriseSecrets.Add(new(
            $"{ISecretId.ENTERPRISE_KEY_PREFIX}::{dataSource.Id}",
            dataSource.Name,
            decryptedSecret,
            SecretStoreType.DATA_SOURCE));
        LOGGER.LogDebug($"Successfully decrypted the {secretFieldName} for data source {idx}. It will be stored in the OS keyring. (Plugin ID: {configPluginId})");
        return true;
    }

    private static string CreateSecretLine(string fieldName, string? encryptedSecret, string placeholder, bool useSecretPlaceholder)
    {
        var secret = !string.IsNullOrWhiteSpace(encryptedSecret)
            ? encryptedSecret
            : useSecretPlaceholder
                ? placeholder
                : string.Empty;

        if (string.IsNullOrWhiteSpace(secret))
            return string.Empty;

        return $"""
               ["{fieldName}"] = "{LuaTools.EscapeLuaString(secret)}",
               """;
    }

    private static string CleanHostname(string hostname)
    {
        var cleanedHostname = hostname.Trim();
        return cleanedHostname.EndsWith('/') ? cleanedHostname[..^1] : cleanedHostname;
    }
}