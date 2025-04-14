using System.Text.RegularExpressions;

using SharedTools;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Build.Commands;

public sealed partial class CollectI18NKeysCommand
{
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
        var sb = new StringBuilder();
        
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
            var name = fileInfo.Name.Replace(fileInfo.Extension, string.Empty);
            var langNamespace = $"{ns}::{name}".ToUpperInvariant().Replace(".", "::");
            foreach (var match in matches)
            {
                var key = $"root::{langNamespace}::T{match.ToFNV32()}";
                
            }
        }
        
        Console.WriteLine($" {counter:###,###} files processed.");
        Console.WriteLine();
    }
    
    private List<string> FindAllTextTags(ReadOnlySpan<char> fileContent)
    {
        const string START_TAG = """
                                 T("
                                 """;
        
        const string END_TAG = """
                               ")
                               """;

        var matches = new List<string>();
        var startIdx = fileContent.IndexOf(START_TAG);
        var content = fileContent;
        while (startIdx > -1)
        {
            content = content[(startIdx + START_TAG.Length)..];
            var endIdx = content.IndexOf(END_TAG);
            if (endIdx == -1)
                break;
            
            var match = content[..endIdx];
            matches.Add(match.ToString());
            
            startIdx = content.IndexOf(START_TAG);
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