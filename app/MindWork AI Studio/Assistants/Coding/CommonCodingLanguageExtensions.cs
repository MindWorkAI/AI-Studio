namespace AIStudio.Assistants.Coding;

public static class CommonCodingLanguageExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(CommonCodingLanguageExtensions).Namespace, nameof(CommonCodingLanguageExtensions));
    
    public static string Name(this CommonCodingLanguages language) => language switch
    { 
        CommonCodingLanguages.NONE => TB("None"),
        
        CommonCodingLanguages.BASH => "Bash",
        CommonCodingLanguages.BLAZOR => ".NET Blazor",
        CommonCodingLanguages.C => "C",
        CommonCodingLanguages.CPP => "C++",
        CommonCodingLanguages.CSHARP => "C#",
        CommonCodingLanguages.CSS => "CSS",
        CommonCodingLanguages.FORTRAN => "Fortran",
        CommonCodingLanguages.GDSCRIPT => "GDScript",
        CommonCodingLanguages.GO => "Go",
        CommonCodingLanguages.HTML => "HTML",
        CommonCodingLanguages.JAVA => "Java",
        CommonCodingLanguages.JAVASCRIPT => "JavaScript",
        CommonCodingLanguages.JSON => "JSON",
        CommonCodingLanguages.JULIA => "Julia",
        CommonCodingLanguages.KOTLIN => "Kotlin",
        CommonCodingLanguages.LUA => "Lua",
        CommonCodingLanguages.MARKDOWN => "Markdown",
        CommonCodingLanguages.MATHEMATICA => "Mathematica",
        CommonCodingLanguages.MATLAB => "MATLAB",
        CommonCodingLanguages.PHP => "PHP",
        CommonCodingLanguages.POWERSHELL => "PowerShell",
        CommonCodingLanguages.PROLOG => "Prolog",
        CommonCodingLanguages.PYTHON => "Python",
        CommonCodingLanguages.R => "R",
        CommonCodingLanguages.RUBY => "Ruby",
        CommonCodingLanguages.RUST => "Rust",
        CommonCodingLanguages.SQL => "SQL",
        CommonCodingLanguages.SWIFT => "Swift",
        CommonCodingLanguages.TYPESCRIPT => "TypeScript",
        CommonCodingLanguages.XML => "XML",
        
        CommonCodingLanguages.OTHER => TB("Other"),
        _ => TB("Unknown")
    };
}