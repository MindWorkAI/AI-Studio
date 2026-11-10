using AIStudio.Assistants;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings.DataModel;

public static class AssistantStartModeExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(AssistantStartModeExtensions).Namespace, nameof(AssistantStartModeExtensions));

    /// <param name="startMode">How the assistant starts.</param>
    extension(AssistantStartMode startMode)
    {
        /// <summary>
        /// Gets the name of the start mode, as the settings show it.
        /// </summary>
        public string Name => startMode switch
        {
            AssistantStartMode.ASK_EVERY_TIME => TB("Ask every time"),
            AssistantStartMode.DOCUMENT => TB("Always start with a document"),
            AssistantStartMode.TEXT => TB("Always start with a text"),

            _ => TB("Ask every time"),
        };

        /// <summary>
        /// Gets what the assistant works on when it starts; undecided while it still has to ask.
        /// </summary>
        public AssistantContentMode InitialContentMode => startMode switch
        {
            AssistantStartMode.DOCUMENT => AssistantContentMode.DOCUMENT,
            AssistantStartMode.TEXT => AssistantContentMode.TEXT,

            _ => AssistantContentMode.UNDECIDED,
        };
    }
}