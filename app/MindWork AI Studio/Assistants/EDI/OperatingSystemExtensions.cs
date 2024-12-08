namespace AIStudio.Assistants.EDI;

public static class OperatingSystemExtensions
{
    public static string Name(this OperatingSystem os) => os switch
    {
        OperatingSystem.NONE => "No operating system specified",
        
        OperatingSystem.WINDOWS => "Windows",
        OperatingSystem.LINUX => "Linux",
        
        _ => "Unknown operating system"
    };
}