using System.Globalization;

using AIStudio.Components;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Tells how to work with the BeyondFacts Lab: what it is for and what not, how long a preparation takes, how to
/// proceed, what the research on the web receives, and how to recognize good results.
/// </summary>
/// <remarks>
/// The texts follow the short guide for users by Lara Wüster. They describe the lab as this version has it, so a
/// change of the lab may need a change here as well, and in the prompt of the onboarding.
/// </remarks>
public partial class BflIntroduction : MSGComponentBase
{
    /// <summary>
    /// Puts the icon of a list item at the first line of its text, like MudTextList does.
    /// </summary>
    private const string LIST_ITEM_STYLE = "display: flex; align-items: flex-start;";

    private static CultureInfo Culture => Tools.PluginSystem.I18N.I.Culture;
}