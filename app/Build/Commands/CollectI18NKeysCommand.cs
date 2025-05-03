using System.Text.RegularExpressions;

using SharedTools;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Build.Commands;

public sealed partial class CollectI18NKeysCommand
{
    private const string START_TAG1 = """
                                      T("
                                      """;
        
    private const string START_TAG2 = """
                                      TB("
                                      """;
        
    private const string START_TAG3 = """
                                      T(@"
                                      """;
        
    private const string END_TAG = """
                           ")
                           """;
        
    private static readonly (string Tag, int Length)[] START_TAGS =
    [
        (START_TAG1, START_TAG1.Length),
        (START_TAG2, START_TAG2.Length),
        (START_TAG3, START_TAG3.Length)
    ];
    
    [Command("collect-i18n", Description = "Collect I18N keys")]
    public async Task CollectI18NKeys()
    {
        if(!Environment.IsWorkingDirectoryValid())
            return;

        Console.WriteLine("=========================");
        Console.Write("- Collecting I18N keys ...");
        
        var cwd = Environment.GetAIStudioDirectory();
        var binPath = Path.Join(cwd, "bin");
        var objPath = Path.Join(cwd, "obj");
        var wwwrootPath = Path.Join(cwd, "wwwroot");
        var allFiles = Directory.EnumerateFiles(cwd, "*", SearchOption.AllDirectories);
        var counter = 0;
        
        var allI18NContent = new Dictionary<string, string>();
        foreach (var filePath in allFiles)
        {
            counter++;
            if(filePath.StartsWith(binPath, StringComparison.OrdinalIgnoreCase))
                continue;
            
            if(filePath.StartsWith(objPath, StringComparison.OrdinalIgnoreCase))
                continue;
            
            if(filePath.StartsWith(wwwrootPath, StringComparison.OrdinalIgnoreCase))
                continue;
            
            var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            var matches = this.FindAllTextTags(content);
            if (matches.Count == 0)
                continue;
            
            var ns = this.DetermineNamespace(filePath);
            var fileInfo = new FileInfo(filePath);
            var name = fileInfo.Name.Replace(fileInfo.Extension, string.Empty).Replace(".razor", string.Empty);
            var langNamespace = $"{ns}.{name}".ToUpperInvariant();
            foreach (var match in matches)
            {
                // The key in the format A.B.C.D.T{hash}:
                var key = $"UI_TEXT_CONTENT.{langNamespace}.T{match.ToFNV32()}";
                allI18NContent.TryAdd(key, match);
            }
        }
        
        Console.WriteLine($" {counter:###,###} files processed, {allI18NContent.Count:###,###} keys found.");
        
        Console.Write("- Creating Lua code ...");
        var luaCode = this.ExportToLuaAssignments(allI18NContent);
        
        // Build the path, where we want to store the Lua code:
        var luaPath = Path.Join(cwd, "Assistants", "I18N", "allTexts.lua");
        
        // Store the Lua code:
        await File.WriteAllTextAsync(luaPath, luaCode, Encoding.UTF8);
        
        Console.WriteLine(" done.");
    }

    private string ExportToLuaAssignments(Dictionary<string, string> keyValuePairs)
    {
        var sb = new StringBuilder();

        // Add the mandatory plugin metadata:
        sb.AppendLine(
            """
            -- The ID for this plugin:
            ID = "77c2688a-a68f-45cc-820e-fa8f3038a146"
            
            -- The icon for the plugin:
            ICON_SVG = ""
            
            -- The name of the plugin:
            NAME = "Collected I18N keys"
            
            -- The description of the plugin:
            DESCRIPTION = "This plugin is not meant to be used directly. Its a collection of all I18N keys found in the project."
            
            -- The version of the plugin:
            VERSION = "1.0.0"
            
            -- The type of the plugin:
            TYPE = "LANGUAGE"
            
            -- The authors of the plugin:
            AUTHORS = {"MindWork AI Community"}
            
            -- The support contact for the plugin:
            SUPPORT_CONTACT = "MindWork AI Community"
            
            -- The source URL for the plugin:
            SOURCE_URL = "https://github.com/MindWorkAI/AI-Studio"
            
            -- The categories for the plugin:
            CATEGORIES = { "CORE" }
            
            -- The target groups for the plugin:
            TARGET_GROUPS = { "EVERYONE" }
            
            -- The flag for whether the plugin is maintained:
            IS_MAINTAINED = true
            
            -- When the plugin is deprecated, this message will be shown to users:
            DEPRECATION_MESSAGE = ""
            
            -- The IETF BCP 47 tag for the language. It's the ISO 639 language
            -- code followed by the ISO 3166-1 country code:
            IETF_TAG = "en-US"
            
            -- The language name in the user's language:
            LANG_NAME = "English (United States)"
            
            """
        );
        
        // Add the UI_TEXT_CONTENT table:
        LuaTable.Create(ref sb, "UI_TEXT_CONTENT", keyValuePairs);
        return sb.ToString();
    }

    private List<string> FindAllTextTags(ReadOnlySpan<char> fileContent)
    {
        (int Index, int Len) FindNextStart(ReadOnlySpan<char> content)
        {
            var bestIndex = -1;
            var bestLength = 0;

            foreach (var (tag, length) in START_TAGS)
            {
                var index = content.IndexOf(tag);
                if (index != -1 && (bestIndex == -1 || index < bestIndex))
                {
                    bestIndex = index;
                    bestLength = length;
                }
            }

            return (bestIndex, bestLength);
        }
        
        var matches = new List<string>();
        var startIdx = FindNextStart(fileContent);
        var content = fileContent;
        while (startIdx.Index > -1)
        {
            //
            // In some cases, after the initial " there follow more " characters.
            // We need to skip them:
            //
            content = content[(startIdx.Index + startIdx.Len)..];
            while(content[0] == '"')
                content = content[1..];
            
            var endIdx = content.IndexOf(END_TAG);
            if (endIdx == -1)
                break;
            
            var match = content[..endIdx];
            while (match[^1] == '"')
                match = match[..^1];
            
            matches.Add(match.ToString());
            startIdx = FindNextStart(content);
        }
        
        return matches;
    }
    
    private string? DetermineNamespace(string filePath)
    {
        // Is it a C# file? Then we can read the namespace from it:
        if (filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            return this.ReadNamespaceFromCSharp(filePath);

        // Is it a Razor file? Then, it depends:
        if (filePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
        {
            // Check if the file contains a namespace declaration:
            var blazorNamespace = this.ReadNamespaceFromRazor(filePath);
            if (blazorNamespace != null)
                return blazorNamespace;
            
            // Alright, no namespace declaration. Let's check the corresponding C# file:
            var csFilePath = $"{filePath}.cs";
            if (File.Exists(csFilePath))
            {
                var csNamespace = this.ReadNamespaceFromCSharp(csFilePath);
                if (csNamespace != null)
                    return csNamespace;
                
                Console.WriteLine($"- Error: Neither the blazor file '{filePath}' nor the corresponding C# file '{csFilePath}' contain a namespace declaration.");
                return null;
            }

            Console.WriteLine($"- Error: The blazor file '{filePath}' does not contain a namespace declaration and the corresponding C# file '{csFilePath}' does not exist.");
            return null;
        }
        
        // Not a C# or Razor file. We can't determine the namespace:
        Console.WriteLine($"- Error: The file '{filePath}' is neither a C# nor a Razor file. We can't determine the namespace.");
        return null;
    }
    
    private string? ReadNamespaceFromCSharp(string filePath)
    {
        var content = File.ReadAllText(filePath, Encoding.UTF8);
        var matches = CSharpNamespaceRegex().Matches(content);
        
        if (matches.Count == 0)
            return null;

        if (matches.Count > 1)
        {
            Console.WriteLine($"The file '{filePath}' contains multiple namespaces. This scenario is not supported.");
            return null;
        }
        
        var match = matches[0];
        return match.Groups[1].Value;
    }
    
    private string? ReadNamespaceFromRazor(string filePath)
    {
        var content = File.ReadAllText(filePath, Encoding.UTF8);
        var matches = BlazorNamespaceRegex().Matches(content);
        
        if (matches.Count == 0)
            return null;

        if (matches.Count > 1)
        {
            Console.WriteLine($"The file '{filePath}' contains multiple namespaces. This scenario is not supported.");
            return null;
        }
        
        var match = matches[0];
        return match.Groups[1].Value;
    }

    [GeneratedRegex("""@namespace\s+([a-zA-Z0-9_.]+)""")]
    private static partial Regex BlazorNamespaceRegex();
    
    [GeneratedRegex("""namespace\s+([a-zA-Z0-9_.]+)""")]
    private static partial Regex CSharpNamespaceRegex();
}