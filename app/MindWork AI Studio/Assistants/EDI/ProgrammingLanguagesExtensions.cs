namespace AIStudio.Assistants.EDI;

public static class ProgrammingLanguagesExtensions
{
    public static string Name(this ProgrammingLanguages language) => language switch
    { 
        ProgrammingLanguages.NONE => "None",
        
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