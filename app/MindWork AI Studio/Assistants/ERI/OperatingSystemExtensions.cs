namespace AIStudio.Assistants.ERI;

public static class OperatingSystemExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(OperatingSystemExtensions).Namespace, nameof(OperatingSystemExtensions));
    
    extension(OperatingSystem os)
    {
        public string Name => os switch
        {
            OperatingSystem.NONE => TB("No operating system specified"),
        
            OperatingSystem.WINDOWS => TB("Windows"),
            OperatingSystem.LINUX => TB("Linux"),
        
            _ => TB("Unknown operating system")
        };

        public string Prompt => os switch
        {
            OperatingSystem.NONE => "No operating system specified",
        
            OperatingSystem.WINDOWS => "Windows",
            OperatingSystem.LINUX => "Linux",
        
            _ => "Unknown operating system"
        };
    }
}