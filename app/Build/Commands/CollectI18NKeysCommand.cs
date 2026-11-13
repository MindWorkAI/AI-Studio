using System.Text.RegularExpressions;

using SharedTools;

namespace Build.Commands;

public sealed partial class CollectI18NKeysCommand
{
    /// <summary>
    /// Defines the command "collect-i18n".
    /// </summary>
    public static Command CreateCommand()
    {
        var command = new Command("collect-i18n", "Collect I18N keys");
        command.SetAction((_, _) => new CollectI18NKeysCommand().CollectI18NKeys());
        return command;
    }

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
        
        var warnings = new List<string>();
        var allI18NContent = new Dictionary<string, string>();
        foreach (var filePath in allFiles)
        {
            counter++;
            if(!this.IsSupportedSourceFile(filePath))
                continue;

            if(filePath.StartsWith(binPath, StringComparison.OrdinalIgnoreCase))
                continue;
            
            if(filePath.StartsWith(objPath, StringComparison.OrdinalIgnoreCase))
                continue;
            
            if(filePath.StartsWith(wwwrootPath, StringComparison.OrdinalIgnoreCase))
                continue;
            
            var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            var matches = I18NSourceScanner.FindTexts(content, filePath, warnings);
            if (matches.Count == 0)
                continue;
            
            var ns = this.DetermineNamespace(filePath);
            if(ns is null)
                throw new InvalidOperationException($"Could not determine the namespace for I18N source file '{filePath}'.");

            var fileInfo = new FileInfo(filePath);
            
            var name = this.DetermineTypeName(filePath)
                       ?? fileInfo.Name.Replace(fileInfo.Extension, string.Empty).Replace(".razor", string.Empty);
            
            var langNamespace = $"{ns}.{name}".ToUpperInvariant();
            foreach (var match in matches)
            {
                // The key in the format A.B.C.D.T{hash}, with the hash the app computes at runtime:
                var key = $"UI_TEXT_CONTENT.{langNamespace}.T{I18NKeyHash.Of(match.Text)}";
                var text = I18NKeyHash.NormalizeLineEndings(match.Text);
                allI18NContent.TryAdd(key, text);

                // A text of TA() addresses the reader, so it gets a second key for the informal address:
                if (match.HasAddressVariants)
                    allI18NContent.TryAdd(I18NAddressVariant.InformalKeyOf(key), text);
            }
        }
        
        Console.WriteLine($" {counter:###,###} files processed, {allI18NContent.Count:###,###} keys found.");
        foreach (var warning in warnings)
            Console.WriteLine(warning);

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

    private bool IsSupportedSourceFile(string filePath) =>
        filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
        filePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase);
    
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

    private string? DetermineTypeName(string filePath)
    {
        if (!filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            return null;

        return this.ReadPartialTypeNameFromCSharp(filePath);
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

    private string? ReadPartialTypeNameFromCSharp(string filePath)
    {
        var content = File.ReadAllText(filePath, Encoding.UTF8);
        var matches = CSharpPartialTypeRegex().Matches(content);

        if (matches.Count == 0)
            return null;

        if (matches.Count > 1)
        {
            Console.WriteLine($"The file '{filePath}' contains multiple partial type declarations. This scenario is not supported.");
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

    [GeneratedRegex("""(?m)^\s*@namespace\s+([a-zA-Z0-9_.]+)""")]
    private static partial Regex BlazorNamespaceRegex();
    
    [GeneratedRegex("""(?m)^\s*namespace\s+([a-zA-Z0-9_.]+)\s*[;{]""")]
    private static partial Regex CSharpNamespaceRegex();

    [GeneratedRegex("""\bpartial\s+(?:class|struct|interface|record(?:\s+(?:class|struct))?)\s+([A-Za-z_][A-Za-z0-9_]*)""")]
    private static partial Regex CSharpPartialTypeRegex();
}
