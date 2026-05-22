using System.Text;

using AIStudio.Chat;
using AIStudio.Tools.PluginSystem;

using SharedTools;

using LuaTable = Lua.LuaTable;

namespace AIStudio.Settings;

public record ChatTemplate(
    uint Num,
    string Id,
    string Name,
    string SystemPrompt,
    string PredefinedUserPrompt,
    List<ContentBlock> ExampleConversation,
    List<FileAttachment> FileAttachments,
    bool AllowProfileUsage,
    bool IsEnterpriseConfiguration = false,
    Guid EnterpriseConfigurationPluginId = default) : ConfigurationBaseObject
{
    private const string ATTACHMENTS_DIRECTORY = "attachments";

    public ChatTemplate() : this(0, Guid.Empty.ToString(), string.Empty, string.Empty, string.Empty, [], [], false)
    {
    }
    
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ChatTemplate).Namespace, nameof(ChatTemplate));
    
    private static readonly ILogger<ChatTemplate> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ChatTemplate>();
    
    public static readonly ChatTemplate NO_CHAT_TEMPLATE = new()
    {
        Name = TB("Use no chat template"), // Cannot be localized due to being a static readonly field
        SystemPrompt = string.Empty,
        PredefinedUserPrompt = string.Empty,
        Id = Guid.Empty.ToString(),
        Num = uint.MaxValue,
        ExampleConversation = [],
        FileAttachments = [],
        AllowProfileUsage = true,
        EnterpriseConfigurationPluginId = Guid.Empty,
        IsEnterpriseConfiguration = false,
    };
    
    #region Overrides of ValueType

    /// <summary>
    /// Returns a string that represents the profile in a human-readable format.
    /// </summary>
    /// <returns>A string that represents the profile in a human-readable format.</returns>
    public override string ToString() => this.GetSafeName();

    #endregion

    /// <summary>
    /// Gets the name of this chat template. If it is the NO_CHAT_TEMPLATE, it returns a localized string.
    /// </summary>
    /// <remarks>
    /// Why not using the Name property directly? Because the Name property of NO_CHAT_TEMPLATE cannot be
    /// localized because it is a static readonly field. So we need this method to return a localized
    /// string instead.
    /// </remarks>
    /// <returns>The name of this chat template.</returns>
    public string GetSafeName()
    {
        if(this == NO_CHAT_TEMPLATE)
            return TB("Use no chat template");
        
        return this.Name;
    }
    
    public string ToSystemPrompt()
    {
        if(this.Num == uint.MaxValue)
            return string.Empty;
        
        return this.SystemPrompt;
    }

    public static bool TryParseChatTemplateTable(int idx, LuaTable table, Guid configPluginId, string pluginPath, out ConfigurationBaseObject template)
    {
        template = NO_CHAT_TEMPLATE;
        if (!table.TryGetValue("Id", out var idValue) || !idValue.TryRead<string>(out var idText) || !Guid.TryParse(idText, out var id))
        {
            LOGGER.LogWarning($"The configured chat template {idx} does not contain a valid ID. The ID must be a valid GUID.");
            return false;
        }
        
        if (!table.TryGetValue("Name", out var nameValue) || !nameValue.TryRead<string>(out var name))
        {
            LOGGER.LogWarning($"The configured chat template {idx} does not contain a valid name.");
            return false;
        }
        
        if (!table.TryGetValue("SystemPrompt", out var sysPromptValue) || !sysPromptValue.TryRead<string>(out var systemPrompt))
        {
            LOGGER.LogWarning($"The configured chat template {idx} does not contain a valid system prompt.");
            return false;
        }
        
        var predefinedUserPrompt = string.Empty;
        if (table.TryGetValue("PredefinedUserPrompt", out var preUserValue) && preUserValue.TryRead<string>(out var preUser))
            predefinedUserPrompt = preUser;
        
        var allowProfileUsage = false;
        if (table.TryGetValue("AllowProfileUsage", out var allowProfileValue) && allowProfileValue.TryRead<bool>(out var allow))
            allowProfileUsage = allow;

        var fileAttachments = ParseFileAttachments(idx, table, pluginPath);

        template = new ChatTemplate
        {
            Num = 0, // will be set later by the PluginConfigurationObject
            Id = id.ToString(),
            Name = name,
            SystemPrompt = systemPrompt,
            PredefinedUserPrompt = predefinedUserPrompt,
            ExampleConversation = ParseExampleConversation(idx, table),
            FileAttachments = fileAttachments,
            AllowProfileUsage = allowProfileUsage,
            IsEnterpriseConfiguration = true,
            EnterpriseConfigurationPluginId = configPluginId,
        };
        
        return true;
    }
    
    private static List<ContentBlock> ParseExampleConversation(int idx, LuaTable table)
    {
        var exampleConversation = new List<ContentBlock>();
        if (!table.TryGetValue("ExampleConversation", out var exConvValue) || !exConvValue.TryRead<LuaTable>(out var exConvTable))
            return exampleConversation;
        
        var numBlocks = exConvTable.ArrayLength;
        for (var j = 1; j <= numBlocks; j++)
        {
            var blockValue = exConvTable[j];
            if (!blockValue.TryRead<LuaTable>(out var blockTable))
            {
                LOGGER.LogWarning($"The ExampleConversation entry {j} in chat template {idx} is not a valid table.");
                continue;
            }
            
            if (!blockTable.TryGetValue("Role", out var roleValue) || !roleValue.TryRead<string>(out var roleText) || !Enum.TryParse<ChatRole>(roleText, true, out var parsedRole))
            {
                LOGGER.LogWarning($"The ExampleConversation entry {j} in chat template {idx} does not contain a valid role.");
                continue;
            }

            if (!blockTable.TryGetValue("Content", out var contentValue) || !contentValue.TryRead<string>(out var content))
            {
                LOGGER.LogWarning($"The ExampleConversation entry {j} in chat template {idx} does not contain a valid content message.");
                continue;
            }
                
            if (string.IsNullOrWhiteSpace(content))
            {
                LOGGER.LogWarning($"The ExampleConversation entry {j} in chat template {idx} contains an empty content message.");
                continue;
            }
                
            exampleConversation.Add(new ContentBlock
            {
                Time = DateTimeOffset.UtcNow,
                Role = parsedRole,
                Content = new ContentText { Text = content },
                ContentType = ContentType.TEXT,
                HideFromUser = true,
            });
        }

        return exampleConversation;
    }

    private static List<FileAttachment> ParseFileAttachments(int idx, LuaTable table, string pluginPath)
    {
        var fileAttachments = new List<FileAttachment>();
        if (!table.TryGetValue("FileAttachments", out var fileAttValue) || !fileAttValue.TryRead<LuaTable>(out var fileAttTable))
            return fileAttachments;

        var numAttachments = fileAttTable.ArrayLength;
        for (var attachmentNum = 1; attachmentNum <= numAttachments; attachmentNum++)
        {
            var attachmentValue = fileAttTable[attachmentNum];
            if (!attachmentValue.TryRead<string>(out var filePath))
            {
                LOGGER.LogWarning("The FileAttachments entry {AttachmentNum} in chat template {IdxChatTemplate} is not a valid string.", attachmentNum, idx);
                continue;
            }

            if (TryResolveFileAttachmentPath(idx, attachmentNum, filePath, pluginPath, out var resolvedFilePath))
                fileAttachments.Add(FileAttachment.FromPath(resolvedFilePath));
        }

        return fileAttachments;
    }

    private static bool TryResolveFileAttachmentPath(int idx, int attachmentNum, string filePath, string pluginPath, out string resolvedFilePath)
    {
        resolvedFilePath = filePath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            LOGGER.LogWarning("The FileAttachments entry {AttachmentNum} in chat template {IdxChatTemplate} is empty.", attachmentNum, idx);
            return false;
        }

        if (Path.IsPathFullyQualified(filePath))
            return true;

        if (string.IsNullOrWhiteSpace(pluginPath))
        {
            LOGGER.LogWarning("The relative FileAttachments entry {AttachmentNum} in chat template {IdxChatTemplate} cannot be resolved because the plugin path is unknown.", attachmentNum, idx);
            return false;
        }

        var pluginRoot = Path.GetFullPath(pluginPath);
        var relativePath = filePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        var combinedPath = Path.GetFullPath(Path.Combine(pluginRoot, relativePath));
        var pluginRootWithSeparator = pluginRoot.EndsWith(Path.DirectorySeparatorChar)
            ? pluginRoot
            : pluginRoot + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!combinedPath.StartsWith(pluginRootWithSeparator, comparison))
        {
            LOGGER.LogWarning("The relative FileAttachments entry {AttachmentNum} in chat template {IdxChatTemplate} points outside of the plugin folder and will be ignored.", attachmentNum, idx);
            return false;
        }

        resolvedFilePath = combinedPath;
        return true;
    }

    public bool TryExportAsConfigurationSection(out string luaCode, out string issue) => this.TryExportAsConfigurationSection(null, Guid.NewGuid().ToString(), out luaCode, out issue);
    
    private bool TryExportAsConfigurationSection(IReadOnlyList<string>? fileAttachmentPaths, string exportId, out string luaCode, out string issue)
    {
        luaCode = string.Empty;
        issue = string.Empty;
        if (!this.TryBuildExampleConversationLua(out var exampleConversationLua, out issue))
            return false;

        return this.TryExportAsConfigurationSection(fileAttachmentPaths, exportId, exampleConversationLua, out luaCode, out issue);
    }

    private bool TryExportAsConfigurationSection(IReadOnlyList<string>? fileAttachmentPaths, string exportId, string exampleConversationLua, out string luaCode, out string issue)
    {
        issue = string.Empty;
        var fileAttachmentsLua = this.BuildFileAttachmentsLua(fileAttachmentPaths);
        luaCode = $$"""
                    CONFIG["CHAT_TEMPLATES"][#CONFIG["CHAT_TEMPLATES"]+1] = {
                        ["Id"] = "{{LuaTools.EscapeLuaString(exportId)}}",
                        ["Name"] = {{LuaTools.ToLuaStringLiteral(this.Name)}},
                        ["SystemPrompt"] = {{LuaTools.ToLuaStringLiteral(this.SystemPrompt)}},
                        ["PredefinedUserPrompt"] = {{LuaTools.ToLuaStringLiteral(this.PredefinedUserPrompt)}},
                        ["AllowProfileUsage"] = {{this.AllowProfileUsage.ToString().ToLowerInvariant()}},
                        ["FileAttachments"] = {{fileAttachmentsLua}},
                        ["ExampleConversation"] = {{exampleConversationLua}},
                    }
                    """;
        return true;
    }

    public bool TryExportAsConfigurationSectionWithPackagedAttachments(string pluginDirectory, out string luaCode, out string issue)
    {
        luaCode = string.Empty;
        issue = string.Empty;
        var exportId = Guid.NewGuid().ToString();

        if (!this.TryBuildExampleConversationLua(out var exampleConversationLua, out issue))
            return false;

        if (this.FileAttachments.Count == 0)
            return this.TryExportAsConfigurationSection(null, exportId, exampleConversationLua, out luaCode, out issue);

        if (string.IsNullOrWhiteSpace(pluginDirectory) || !File.Exists(Path.Combine(pluginDirectory, "plugin.lua")))
        {
            issue = TB("Please select a valid configuration plugin folder. The folder must contain a plugin.lua file.");
            return false;
        }

        var sourcePaths = new List<string>();
        foreach (var attachment in this.FileAttachments)
        {
            if (string.IsNullOrWhiteSpace(attachment.FilePath) || !File.Exists(attachment.FilePath))
            {
                issue = string.Format(TB("Cannot package the attachment '{0}' because the file does not exist."), attachment.FileName);
                return false;
            }

            sourcePaths.Add(attachment.FilePath);
        }

        var targetDirectory = Path.Combine(pluginDirectory, ATTACHMENTS_DIRECTORY, exportId);
        var relativeAttachmentPaths = new List<string>();
        var usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            Directory.CreateDirectory(targetDirectory);
            foreach (var sourcePath in sourcePaths)
            {
                var targetFileName = CreateUniqueAttachmentFileName(sourcePath, usedFileNames);
                var targetPath = Path.Combine(targetDirectory, targetFileName);
                File.Copy(sourcePath, targetPath, overwrite: false);
                relativeAttachmentPaths.Add($"{ATTACHMENTS_DIRECTORY}/{exportId}/{targetFileName}");
            }
        }
        catch (Exception e)
        {
            try
            {
                if (Directory.Exists(targetDirectory))
                    Directory.Delete(targetDirectory, true);
            }
            catch
            {
                // Keep the original packaging error as the user-facing issue.
            }

            issue = string.Format(TB("Cannot package the chat template attachments. The issue was: {0}"), e.Message);
            return false;
        }

        return this.TryExportAsConfigurationSection(relativeAttachmentPaths, exportId, exampleConversationLua, out luaCode, out issue);
    }

    private bool TryBuildExampleConversationLua(out string luaTable, out string issue)
    {
        luaTable = "{}";
        issue = string.Empty;
        if (this.ExampleConversation.Count == 0)
            return true;

        var builder = new StringBuilder();
        builder.AppendLine("{");
        for (var i = 0; i < this.ExampleConversation.Count; i++)
        {
            var block = this.ExampleConversation[i];
            if (block.Role is not ChatRole.USER and not ChatRole.AI)
            {
                issue = string.Format(TB("Cannot export this chat template because example message {0} uses a role that is not supported by configuration plugins."), i + 1);
                return false;
            }

            if (block.Content is not ContentText textContent)
            {
                issue = string.Format(TB("Cannot export this chat template because example message {0} is not a text message."), i + 1);
                return false;
            }

            if (string.IsNullOrWhiteSpace(textContent.Text))
            {
                issue = string.Format(TB("Cannot export this chat template because example message {0} is empty."), i + 1);
                return false;
            }

            builder.AppendLine("        {");
            builder.AppendLine($"            [\"Role\"] = \"{block.Role}\",");
            builder.AppendLine($"            [\"Content\"] = {LuaTools.ToLuaStringLiteral(textContent.Text)},");
            builder.AppendLine("        },");
        }

        builder.Append("    }");
        luaTable = builder.ToString();
        return true;
    }

    private string BuildFileAttachmentsLua(IReadOnlyList<string>? fileAttachmentPaths)
    {
        var paths = fileAttachmentPaths ?? this.FileAttachments.Select(attachment => attachment.FilePath).ToList();
        if (paths.Count == 0)
            return "{}";

        var builder = new StringBuilder();
        builder.AppendLine("{");
        foreach (var path in paths)
            builder.AppendLine($"        \"{LuaTools.EscapeLuaString(path)}\",");

        builder.Append("    }");
        return builder.ToString();
    }

    private static string CreateUniqueAttachmentFileName(string sourcePath, HashSet<string> usedFileNames)
    {
        var fileName = SanitizeFileName(Path.GetFileName(sourcePath));
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "attachment";

        var extension = Path.GetExtension(fileName);
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var candidate = fileName;
        var counter = 2;
        while (!usedFileNames.Add(candidate))
            candidate = $"{nameWithoutExtension}-{counter++}{extension}";

        return candidate;
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(invalidChar, '_');

        return fileName;
    }
}