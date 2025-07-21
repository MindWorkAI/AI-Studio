using Lua;

namespace AIStudio.Tools.PluginSystem.Assistants;

public sealed class PluginAssistants : PluginBase
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(PluginAssistants).Namespace, nameof(PluginAssistants));
    private static readonly ILogger<PluginAssistants> LOGGER = Program.LOGGER_FACTORY.CreateLogger<PluginAssistants>();

    public string AssistantTitle { get; set;} = string.Empty;
    private string AssistantDescription {get; set;} = string.Empty;

    public PluginAssistants(bool isInternal, LuaState state, PluginType type) : base(isInternal, state, type)
    {
        
    }
    
    
}