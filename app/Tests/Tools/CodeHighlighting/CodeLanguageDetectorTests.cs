using AIStudio.Tools;
using AIStudio.Tools.CodeHighlighting;

namespace AIStudio.Tests.Tools.CodeHighlighting;

/// <summary>
/// Checks which language AI Studio recognizes in a code block which names none.
/// </summary>
[TestFixture]
public sealed class CodeLanguageDetectorTests
{
    /// <summary>
    /// The languages MudBlazor.Markdown highlights itself, as a model writes them behind the fence.
    /// </summary>
    private static readonly string[] LIBRARY_LANGUAGES = ["csharp", "go", "kotlin", "rust", "json", "xml", "html", "yaml"];

    [TestCase("import os\n\ndef greet(name):\n    if name is None:\n        return \"nobody\"\n    print(f\"Hello {name}\")", "python")]
    [TestCase("#!/bin/bash\nfor file in *.txt; do\n  echo \"$file\"\ndone", "bash")]
    [TestCase("npm install express", "bash", Description = "A single command is enough when only a shell runs it.")]
    [TestCase("Get-ChildItem -Path . | ForEach-Object {\n    Write-Host $_.Name\n}", "powershell")]
    [TestCase("local function greet(name)\n  if name ~= nil then\n    print(\"Hello \" .. name)\n  end\nend", "lua")]
    [TestCase("const express = require('express');\nconst app = express();\napp.get('/', (req, res) => res.send('Hi'));", "javascript")]
    [TestCase("interface User {\n  name: string;\n  age: number;\n}\nconst user: User = { name: 'Ada', age: 36 };", "typescript")]
    [TestCase("#include <stdio.h>\n\nint main(void) {\n    printf(\"Hello\\n\");\n    return 0;\n}", "c")]
    [TestCase("#include <iostream>\n\nint main() {\n    std::cout << \"Hello\" << std::endl;\n}", "cpp")]
    [TestCase("using System;\n\npublic class Greeter\n{\n    public string Name { get; set; }\n    public void Greet() => Console.WriteLine($\"Hello {Name}\");\n}", "csharp")]
    [TestCase("import java.util.List;\n\npublic class Main {\n    public static void main(String[] args) {\n        System.out.println(\"Hello\");\n    }\n}", "java")]
    [TestCase("data class User(val name: String)\n\nfun main() {\n    val user = User(\"Ada\")\n    println(user.name)\n}", "kotlin")]
    [TestCase("package main\n\nimport \"fmt\"\n\nfunc main() {\n    msg := \"Hello\"\n    fmt.Println(msg)\n}", "go")]
    [TestCase("use std::collections::HashMap;\n\nfn main() {\n    let mut map = HashMap::new();\n    println!(\"{:?}\", map);\n}", "rust")]
    [TestCase("SELECT name, COUNT(*) AS total\nFROM orders\nWHERE created_at > '2026-01-01'\nGROUP BY name\nORDER BY total DESC;", "sql")]
    [TestCase("select * from users;", "sql", Description = "SQL ignores the case of its keywords.")]
    [TestCase(".card {\n  color: #333;\n  margin: 0 auto;\n  padding: 1rem;\n}", "css")]
    [TestCase("services:\n  web:\n    image: nginx\n    ports:\n      - \"80:80\"", "yaml")]
    [TestCase("{\"name\": \"Ada\", \"age\": 36}", "json")]
    [TestCase("<div class=\"card\">\n  <p>Hello</p>\n</div>", "html")]
    [TestCase("<?xml version=\"1.0\"?>\n<root><item/></root>", "xml")]
    public void TheCodeNamesItsLanguage(string code, string expected)
    {
        Assert.That(CodeLanguageDetector.Detect(code), Is.EqualTo(expected));
    }

    [TestCase("Just some words in a block.", TestName = "Prose")]
    [TestCase("hello", TestName = "A single word")]
    [TestCase("x = 1", TestName = "A line many languages write alike")]
    [TestCase("let x = 5", TestName = "A line with a single weak hint")]
    [TestCase("{\"name\": \"Ada\",", TestName = "JSON a model has not finished yet")]
    [TestCase("", TestName = "Nothing")]
    [TestCase("   \n  ", TestName = "Only space")]
    public void UnclearCodeStaysText(string code)
    {
        Assert.That(CodeLanguageDetector.Detect(code), Is.Null, "A file saved under a wrong extension is worse than a text file.");
    }

    [TestCase("text")]
    [TestCase("python")]
    [TestCase("foo")]
    public void TheLanguageBehindTheFenceWins(string fenceLanguage)
    {
        Assert.That(CodeLanguageDetector.FenceLanguageOf(fenceLanguage, "using System;\nConsole.WriteLine(\"Hi\");"), Is.EqualTo(fenceLanguage));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void WithoutALanguageTheCodeDecides(string? fenceLanguage)
    {
        Assert.That(CodeLanguageDetector.FenceLanguageOf(fenceLanguage, "using System;\nConsole.WriteLine(\"Hi\");"), Is.EqualTo("csharp"));
    }

    /// <summary>
    /// A recognized language has to be one the export saves under its extension, and one which gets
    /// colors. Otherwise, the guess would help neither.
    /// </summary>
    [Test]
    public void EveryLanguageIsSavedAndHighlighted([ValueSource(nameof(FenceLanguages))] string fenceLanguage)
    {
        using (Assert.EnterMultipleScope())
        {
            var isFormat = FileExportFormatExtensions.TryFromCodeFenceLanguage(fenceLanguage, out _);
            Assert.That(isFormat || SourceCodeLanguages.Aliases.Contains(fenceLanguage, StringComparer.OrdinalIgnoreCase), Is.True, "The export would save it as a text file.");
            Assert.That(CodeHighlighters.TryGetRules(fenceLanguage, out _) || LIBRARY_LANGUAGES.Contains(fenceLanguage), Is.True, "Nobody highlights it.");
        }
    }

    private static IEnumerable<string> FenceLanguages() => CodeLanguageDetector.FenceLanguages;
}