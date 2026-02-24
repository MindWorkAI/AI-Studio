using AIStudio.Components;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;
using System.Collections.Generic;
using System.Linq;

namespace AIStudio.Pages;

public partial class Assistants : MSGComponentBase
{
    private IReadOnlyCollection<PluginAssistants> AssistantPlugins => PluginFactory.RunningPlugins.OfType<PluginAssistants>().ToList();
}
