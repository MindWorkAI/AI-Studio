namespace AIStudio.Assistants.ERI;

public static class ProgrammingLanguagesExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(ProgrammingLanguagesExtensions).Namespace, nameof(ProgrammingLanguagesExtensions));
    
    public static string Name(this ProgrammingLanguages language) => language switch
    { 
        ProgrammingLanguages.NONE => TB("No programming language selected"),
        
        ProgrammingLanguages.C => "C",
        ProgrammingLanguages.CPP => "C++",
        ProgrammingLanguages.CSHARP => "C#",
        ProgrammingLanguages.GO => "Go",
        ProgrammingLanguages.JAVA => "Java",
        ProgrammingLanguages.JAVASCRIPT => "JavaScript",
        ProgrammingLanguages.JULIA => "Julia",
        ProgrammingLanguages.MATLAB => "MATLAB",
        ProgrammingLanguages.PHP => "PHP",
        ProgrammingLanguages.PYTHON => "Python",
        ProgrammingLanguages.RUST => "Rust",
        
        ProgrammingLanguages.OTHER => TB("Other"),
        _ => TB("Unknown")
    };
    
    public static string ToPrompt(this ProgrammingLanguages language) => language switch
    { 
        ProgrammingLanguages.NONE => "No programming language selected",
        
        ProgrammingLanguages.C => "C",
        ProgrammingLanguages.CPP => "C++",
        ProgrammingLanguages.CSHARP => "C#",
        ProgrammingLanguages.GO => "Go",
        ProgrammingLanguages.JAVA => "Java",
        ProgrammingLanguages.JAVASCRIPT => "JavaScript",
        ProgrammingLanguages.JULIA => "Julia",
        ProgrammingLanguages.MATLAB => "MATLAB",
        ProgrammingLanguages.PHP => "PHP",
        ProgrammingLanguages.PYTHON => "Python",
        ProgrammingLanguages.RUST => "Rust",
        
        ProgrammingLanguages.OTHER => "Other",
        _ => "Unknown"
    };
}