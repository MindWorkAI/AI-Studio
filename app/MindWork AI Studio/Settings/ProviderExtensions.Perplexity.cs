using AIStudio.Provider;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
    private static List<Capability> GetModelCapabilitiesPerplexity(Model model)
    {
        var modelName = NormalizeModelId(model.Id).AsSpan();
        
        //
        // No Sonar model writes images. What looked like it does is the option to have the
        // answer come with images: those are pictures the search found on the pages it read,
        // handed back as links, not something the model drew.
        //
        if(modelName.IndexOf("reasoning") is not -1 ||
           modelName.IndexOf("deep-research") is not -1)
            return
            [
                Capability.TEXT_INPUT,
                Capability.MULTIPLE_IMAGE_INPUT,

                Capability.TEXT_OUTPUT,

                Capability.ALWAYS_REASONING,
                Capability.WEB_SEARCH,
                Capability.CHAT_COMPLETION_API,
            ];

        return
        [
            Capability.TEXT_INPUT,
            Capability.MULTIPLE_IMAGE_INPUT,

            Capability.TEXT_OUTPUT,

            Capability.WEB_SEARCH,
            Capability.CHAT_COMPLETION_API,
        ];
    }
}