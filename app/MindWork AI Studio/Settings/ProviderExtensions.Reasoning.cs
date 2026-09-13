using AIStudio.Models;
using AIStudio.Provider;
using AIStudio.Provider.Reasoning;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
    /// <summary>
    /// Get the effective reasoning indicator state for the configured provider instance.
    /// </summary>
    /// <remarks>
    /// Two answers meet here, and they answer different questions. What a model is able to do comes
    /// from the rules; what this person asked for comes from the parameters they wrote into their
    /// own provider. A model which thinks unless told otherwise stops showing the indicator when a
    /// parameter turns it off, and a model which can be asked to think shows it only once one does.
    /// </remarks>
    /// <param name="provider">The configured provider.</param>
    /// <returns>The effective reasoning indicator state.</returns>
    public static ReasoningIndicatorState GetReasoningIndicatorState(this Provider provider)
    {
        var reasoning = provider.GetModelProfile().Reasoning;
        if (reasoning is ReasoningSupport.ALWAYS)
            return ReasoningIndicatorState.ALWAYS_ON;

        var configured = ReasoningDispatcher.WhatTheParametersSay(provider.UsedLLMProvider, provider.Host, provider.AdditionalJsonApiParameters);
        if (reasoning is ReasoningSupport.ON_BY_DEFAULT)
        {
            return configured switch
            {
                ReasoningConfigurationState.EXPLICITLY_DISABLED => ReasoningIndicatorState.NONE,
                ReasoningConfigurationState.EXPLICITLY_ENABLED => ReasoningIndicatorState.CONFIGURED,

                _ => ReasoningIndicatorState.DEFAULT_ON,
            };
        }

        if (reasoning is ReasoningSupport.OPTIONAL && configured is ReasoningConfigurationState.EXPLICITLY_ENABLED)
            return ReasoningIndicatorState.CONFIGURED;

        return ReasoningIndicatorState.NONE;
    }
}