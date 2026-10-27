using System.Collections.Frozen;

using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools;

/// <summary>
/// The languages AI Studio recognizes behind the opening fence of a code block, so that the block
/// can be saved as a file of its own.
/// </summary>
/// <remarks>
/// Models do not agree on how they spell a language: Python comes as python, py, or Python3. The
/// catalog therefore knows the usual aliases, the way GitHub Linguist and highlight.js spell them,
/// in any case. The formats AI Studio writes as formats of their own, such as html, latex, markdown,
/// or csv, are missing on purpose: FileExportFormatExtensions.TryFromCodeFenceLanguage reads them
/// first, and a file in one of them is treated apart from code, it takes the sources of the answer
/// along, for example. Each language has exactly one extension; whoever needs another one renames
/// the file.
/// </remarks>
public static class SourceCodeLanguages
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(SourceCodeLanguages).Namespace, nameof(SourceCodeLanguages));

    /// <summary>
    /// What a model writes behind the fence when it means plain text.
    /// </summary>
    private static readonly FrozenSet<string> PLAIN_TEXT_ALIASES = new[] { "text", "txt", "plaintext", "plain", "none", "nohighlight" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, SourceCodeLanguage> LANGUAGES_BY_ALIAS = BuildCatalog(
    [
        (new("ABAP", ".abap"), ["abap"]),
        (new("Ada", ".adb"), ["ada"]),
        (new("Apache", ".conf"), ["apache", "apacheconf"]),
        (new("Arduino", ".ino"), ["arduino", "ino"]),
        (new("AsciiDoc", ".adoc"), ["asciidoc", "adoc"]),
        (new("Assembly", ".asm"), ["asm", "assembly", "nasm", "x86asm"]),
        (new("AWK", ".awk"), ["awk"]),
        (new("Batch", ".bat", NeedsWindowsLineEndings: true), ["bat", "batch", "batchfile", "cmd", "dos"]),
        (new("BibTeX", ".bib"), ["bibtex", "biblatex", "bib"]),
        (new("Bicep", ".bicep"), ["bicep"]),
        (new("C", ".c"), ["c"]),
        (new("C#", ".cs"), ["csharp", "cs", "c#"]),
        (new("C++", ".cpp"), ["cpp", "c++", "cc", "cxx"]),
        (new("Clojure", ".clj"), ["clojure", "clj"]),
        (new("CMake", string.Empty, "CMakeLists.txt"), ["cmake"]),
        (new("COBOL", ".cbl"), ["cobol"]),
        (new("Containerfile", string.Empty, "Containerfile"), ["containerfile"]),
        (new("CSS", ".css"), ["css"]),
        (new("CUDA", ".cu"), ["cuda", "cu"]),
        (new("Cypher", ".cypher"), ["cypher"]),
        (new("D", ".d"), ["d"]),
        (new("Dart", ".dart"), ["dart"]),
        (new("Diff", ".diff"), ["diff", "udiff"]),
        (new("Dockerfile", string.Empty, "Dockerfile"), ["dockerfile", "docker"]),
        (new("Dotenv", ".env"), ["dotenv", "env"]),
        (new("Elixir", ".ex"), ["elixir", "ex"]),
        (new("Elm", ".elm"), ["elm"]),
        (new("Emacs Lisp", ".el"), ["elisp", "emacs-lisp"]),
        (new("Erlang", ".erl"), ["erlang", "erl"]),
        (new("F#", ".fs"), ["fsharp", "fs", "f#"]),
        (new("Fish", ".fish"), ["fish"]),
        (new("Fortran", ".f90"), ["fortran", "f90", "f95", "f03", "f08"]),
        (new("GDScript", ".gd"), ["gdscript", "gd"]),
        (new("Gherkin", ".feature"), ["gherkin", "cucumber", "feature"]),
        (new("GLSL", ".glsl"), ["glsl"]),
        (new("Gnuplot", ".gp"), ["gnuplot"]),
        (new("Go", ".go"), ["go", "golang"]),
        (new("GraphQL", ".graphql"), ["graphql", "gql"]),
        (new("Graphviz", ".dot"), ["graphviz", "dot", "gv"]),
        (new("Groovy", ".groovy"), ["groovy", "gradle"]),
        (new("Haskell", ".hs"), ["haskell", "hs"]),
        (new("HCL", ".hcl"), ["hcl"]),
        (new("HLSL", ".hlsl"), ["hlsl"]),
        (new("HTTP", ".http"), ["http"]),
        (new("INI", ".ini"), ["ini", "dosini"]),
        (new("Java", ".java"), ["java"]),
        (new("Java Properties", ".properties"), ["properties", "java-properties"]),
        (new("JavaScript", ".js"), ["javascript", "js", "node", "nodejs"]),
        (new("Jenkinsfile", string.Empty, "Jenkinsfile"), ["jenkinsfile", "jenkins"]),
        (new("Jinja", ".j2"), ["jinja", "jinja2", "j2"]),
        (new("JSON", ".json"), ["json"]),
        (new("JSON5", ".json5"), ["json5"]),
        (new("JSONC", ".jsonc"), ["jsonc"]),
        (new("JSX", ".jsx"), ["jsx"]),
        (new("Julia", ".jl"), ["julia", "jl"]),
        (new("Kotlin", ".kt"), ["kotlin", "kt"]),
        (new("Less", ".less"), ["less"]),
        (new("Lisp", ".lisp"), ["lisp", "commonlisp", "common-lisp"]),
        (new("Log", ".log"), ["log"]),
        (new("Lua", ".lua"), ["lua"]),
        (new("Makefile", string.Empty, "Makefile"), ["makefile", "make"]),
        (new("Mathematica", ".wl"), ["mathematica", "wolfram", "wl"]),
        (new("MATLAB", ".m"), ["matlab"]),
        (new("Mermaid", ".mmd"), ["mermaid", "mmd"]),
        (new("Modelica", ".mo"), ["modelica"]),
        (new("Mojo", ".mojo"), ["mojo"]),
        (new("Nginx", ".conf"), ["nginx", "nginxconf"]),
        (new("Nim", ".nim"), ["nim"]),
        (new("Nix", ".nix"), ["nix"]),
        (new("Objective-C", ".m"), ["objectivec", "objective-c", "objc", "obj-c"]),
        (new("OCaml", ".ml"), ["ocaml", "ml"]),
        (new("Octave", ".m"), ["octave"]),
        (new("Pascal", ".pas"), ["pascal", "delphi", "objectpascal", "pas"]),
        (new("Patch", ".patch"), ["patch"]),
        (new("Perl", ".pl"), ["perl", "pl"]),
        (new("PHP", ".php"), ["php"]),
        (new("PlantUML", ".puml"), ["plantuml", "puml"]),
        (new("PowerShell", ".ps1", NeedsByteOrderMark: true), ["powershell", "ps1", "pwsh", "posh"]),
        (new("Prisma", ".prisma"), ["prisma"]),
        (new("Prolog", ".pl"), ["prolog"]),
        (new("Protocol Buffers", ".proto"), ["protobuf", "proto", "proto3"]),
        (new("Python", ".py"), ["python", "py", "python3", "py3"]),
        (new("R", ".R"), ["r"]),
        (new("Racket", ".rkt"), ["racket", "rkt"]),
        (new("Razor", ".razor"), ["razor"]),
        (new("reStructuredText", ".rst"), ["restructuredtext", "rst"]),
        (new("Ruby", ".rb"), ["ruby", "rb"]),
        (new("Rust", ".rs"), ["rust", "rs"]),
        (new("SAS", ".sas"), ["sas"]),
        (new("Sass", ".sass"), ["sass"]),
        (new("Scala", ".scala"), ["scala"]),
        (new("Scheme", ".scm"), ["scheme", "scm"]),
        (new("SCSS", ".scss"), ["scss"]),
        (new("sed", ".sed"), ["sed"]),
        (new("Shell", ".sh"), ["bash", "sh", "shell", "ksh"]),
        (new("Solidity", ".sol"), ["solidity", "sol"]),
        (new("SPARQL", ".rq"), ["sparql", "rq"]),
        (new("SQL", ".sql"), ["sql", "mysql", "mssql", "postgresql", "postgres", "pgsql", "plpgsql", "plsql", "sqlite", "tsql"]),
        (new("Stata", ".do"), ["stata"]),
        (new("Svelte", ".svelte"), ["svelte"]),
        (new("SVG", ".svg"), ["svg"]),
        (new("Swift", ".swift"), ["swift"]),
        (new("SystemVerilog", ".sv"), ["systemverilog", "sv"]),
        (new("Tcl", ".tcl"), ["tcl"]),
        (new("Terraform", ".tf"), ["terraform", "tf"]),
        (new("TOML", ".toml"), ["toml"]),
        (new("TSX", ".tsx"), ["tsx"]),
        (new("Turtle", ".ttl"), ["turtle", "ttl"]),
        (new("TypeScript", ".ts"), ["typescript", "ts"]),
        (new("Typst", ".typ"), ["typst", "typ"]),
        (new("VB.NET", ".vb"), ["vbnet", "vb.net", "vb", "visualbasic"]),
        (new("VBScript", ".vbs"), ["vbscript", "vbs"]),
        (new("Verilog", ".v"), ["verilog"]),
        (new("VHDL", ".vhd"), ["vhdl", "vhd"]),
        (new("Vim script", ".vim"), ["vim", "vimscript", "viml"]),
        (new("Vue", ".vue"), ["vue"]),
        (new("WebAssembly", ".wat"), ["wat", "wast", "wasm"]),
        (new("XAML", ".xaml"), ["xaml"]),
        (new("XML", ".xml"), ["xml"]),
        (new("XSLT", ".xslt"), ["xslt", "xsl"]),
        (new("YAML", ".yaml"), ["yaml", "yml"]),
        (new("Zig", ".zig"), ["zig"]),
        (new("Zsh", ".zsh"), ["zsh"]),
    ]);

    /// <summary>
    /// Every alias the catalog knows, including the ones for plain text.
    /// </summary>
    internal static IEnumerable<string> Aliases => [..PLAIN_TEXT_ALIASES, ..LANGUAGES_BY_ALIAS.Keys];

    /// <summary>
    /// Every language the catalog knows.
    /// </summary>
    internal static IEnumerable<SourceCodeLanguage> Languages => LANGUAGES_BY_ALIAS.Values.Distinct();

    /// <summary>
    /// Reads which language a model means when it names one behind the opening fence of a code block.
    /// </summary>
    /// <remarks>
    /// Every code block becomes a file. A language we do not know keeps its name and becomes a text
    /// file, so the user still sees which block it is; a block without any language is plain text.
    /// </remarks>
    /// <param name="language">The language behind the opening fence, as Markdig reads it into
    /// FencedCodeBlock.Info.</param>
    /// <returns>The language, never NONE.</returns>
    public static SourceCodeLanguage FromCodeFenceLanguage(string? language)
    {
        var name = language?.Trim() ?? string.Empty;
        if (name.Length is 0 || PLAIN_TEXT_ALIASES.Contains(name))
            return new(TB("Text"), ".txt");

        return LANGUAGES_BY_ALIAS.TryGetValue(name, out var knownLanguage) ? knownLanguage : new(name, ".txt");
    }

    private static FrozenDictionary<string, SourceCodeLanguage> BuildCatalog(IEnumerable<(SourceCodeLanguage Language, string[] Aliases)> languages)
    {
        //
        // Add throws for an alias named twice. A mistake in the list above therefore fails every test
        // which touches the catalog, instead of quietly hiding one of the two languages:
        //
        var catalog = new Dictionary<string, SourceCodeLanguage>(StringComparer.OrdinalIgnoreCase);
        foreach (var (language, aliases) in languages)
            foreach (var alias in aliases)
                catalog.Add(alias, language);

        return catalog.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}