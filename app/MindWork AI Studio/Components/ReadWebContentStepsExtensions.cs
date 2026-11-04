using AIStudio.Tools.PluginSystem;

namespace AIStudio.Components;

public static class ReadWebContentStepsExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ReadWebContentStepsExtensions).Namespace, nameof(ReadWebContentStepsExtensions));
 
    /// <param name="step">The ReadWebContentSteps enum value.</param>
    extension(ReadWebContentSteps step)
    {
        /// <summary>
        /// Gets the text representation of a given ReadWebContentSteps enum value.
        /// </summary>
        /// <value>The text representation of the process step.</value>
        public string Text => step switch
        {
            ReadWebContentSteps.START => TB("Start"),
            ReadWebContentSteps.LOADING => TB("Loading"),
            ReadWebContentSteps.PARSING => TB("Parsing"),
            ReadWebContentSteps.CLEANING => TB("Cleaning"),
            ReadWebContentSteps.DONE => TB("Done"),
        
            _ => TB("n/a")
        };
    }
}