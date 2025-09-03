using AIStudio.Tools.PluginSystem;

namespace AIStudio.Components;

public static class ReadWebContentStepsExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ReadWebContentStepsExtensions).Namespace, nameof(ReadWebContentStepsExtensions));
 
    /// <summary>
    /// Gets the text representation of a given ReadWebContentSteps enum value.
    /// </summary>
    /// <param name="step">The ReadWebContentSteps enum value.</param>
    /// <returns>>The text representation of the process step.</returns>
    public static string GetText(this ReadWebContentSteps step) => step switch
    {
        ReadWebContentSteps.START => TB("Start"),
        ReadWebContentSteps.LOADING => TB("Loading"),
        ReadWebContentSteps.PARSING => TB("Parsing"),
        ReadWebContentSteps.CLEANING => TB("Cleaning"),
        ReadWebContentSteps.DONE => TB("Done"),
        
        _ => TB("n/a")
    };
}