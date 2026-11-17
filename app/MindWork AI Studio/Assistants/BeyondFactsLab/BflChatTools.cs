using System.Collections.Frozen;

using AIStudio.Chat;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The tools of the chats of the BeyondFacts Lab, and where the chats may send data.
/// </summary>
/// <remarks>
/// The chat of a step knows the whole preparation, which may be unpublished. It therefore never searches the web: a
/// query could carry anything of the preparation to a search engine. It reads only web pages whose addresses were given
/// to it, in a message of the person, in their details or material, or in the research, see
/// ChatThread.IsWebAddressGivenToTheModel. The restriction of the chat enforces that for Read Web Page, whatever its
/// free address choice; the lab searches the web in agents of its own, see BflResearchRunner. The onboarding knows no
/// preparation and needs no tool.
/// </remarks>
public static class BflChatTools
{
    /// <summary>
    /// Where the chat of a step may send data, from its start.
    /// </summary>
    public static readonly OutboundDataRequirement STEP_REQUIREMENT = new(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, OutboundDataOrigin.BEYOND_FACTS_LAB, string.Empty);

    /// <summary>
    /// The tools of the chat of a step.
    /// </summary>
    private static readonly FrozenSet<string> STEP_TOOL_IDS = FrozenSet.Create(StringComparer.Ordinal, ToolSelectionRules.READ_WEB_PAGE_TOOL_ID);

    /// <summary>
    /// Gets the tools a chat of the lab offers the model.
    /// </summary>
    /// <param name="location">Where the chat belongs.</param>
    /// <returns>The IDs of the tools; the request still leaves out every one which may not run.</returns>
    public static IReadOnlySet<string> For(BflThreadLocation location) => location is BflStepThreadLocation ? STEP_TOOL_IDS : FrozenSet<string>.Empty;

    /// <summary>
    /// Restricts where a chat of the lab may send data, as its place demands. The chat stores the restriction, and it
    /// never gets looser.
    /// </summary>
    /// <param name="thread">The chat.</param>
    /// <param name="location">Where the chat belongs.</param>
    public static void Restrict(ChatThread thread, BflThreadLocation location)
    {
        if (location is BflStepThreadLocation)
            thread.RequireOutboundDataRestriction(STEP_REQUIREMENT);
    }
}