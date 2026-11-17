using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Tells a chat of the BeyondFacts Lab what it can do on the web with the current setup, and why it cannot do more.
/// </summary>
/// <remarks>
/// The chats learn this with every request, so that they never claim a research which the setup does not allow, nor
/// deny one which it does. The texts are German, like the prompts, and depend on nothing but what keeps each tool from
/// being offered, so that the same setup always gives the same prompt.
/// </remarks>
public static class BflToolStatus
{
    /// <summary>
    /// Tells the model that the lab never researches an event which is not public, and where the person says so.
    /// </summary>
    private const string NOT_PUBLIC = "Ist die Veranstaltung nicht öffentlich, recherchiert das Lab nie im Web, weil auch ihre Angaben nicht öffentlich sind; das gibt die Person in ihren Angaben zu den Rahmendaten an.";

    /// <summary>
    /// Describes what the chat of a step can do on the web.
    /// </summary>
    /// <param name="availability">What keeps the tools of the lab from being offered with the provider of the preparation.</param>
    /// <returns>The text for the section "Werkzeuge" of the prompt of a step.</returns>
    public static string ForStep(BflResearchAvailability availability)
    {
        var readWebPage = availability.ReadWebPage is ToolOfferBlockReason.NONE
            ? "Du kannst Webseiten lesen, deren Adressen in diesem Gespräch oder in diesen Anweisungen stehen, etwa in den Nachrichten der Person, in ihren Angaben, in ihrem Material oder in der Recherche. Adressen, die du selbst bildest oder errätst, kann das Lab nicht lesen. Lies eine Seite nur, wenn sie dem Schritt hilft, und nenne sie mit ihrer Adresse, wenn du dich auf sie stützt."
            : $"Webseiten lesen kannst du in diesem Gespräch nicht. {ReasonFor(availability.ReadWebPage)}";

        return $"""
                {readWebPage}

                Im Web suchen kannst du in diesem Gespräch nie. Es kennt die ganze Vorbereitung, auch Unveröffentlichtes. Deshalb sucht nur die Recherche des Labs im Web: eigene Recherche-Agenten, die ausschließlich die öffentlichen Angaben zur Veranstaltung erhalten ({PublicFields()}). Alle anderen Angaben, das Material, die Arbeitsstände und die Gespräche erreichen sie nie. Die Person startet die Recherche im Schritt „Strategie“. {NOT_PUBLIC} {ResearchStatus(availability)}

                Erfinde keine aktuellen Positionen, Aussagen oder Zitate von Personen und keine fehlenden Angaben zur Veranstaltung. Was weder die Angaben, das Material, die Recherche noch eine gelesene Seite belegt, halte als offenen Punkt fest, den die Person prüfen kann.
                """;
    }

    /// <summary>
    /// Describes what the lab can do on the web, for the conversation about whether the lab fits a task.
    /// </summary>
    /// <param name="availability">What keeps the tools of the lab from being offered with the provider of the conversation.</param>
    /// <returns>The text for the section about research and web pages of the prompt of the onboarding.</returns>
    public static string ForOnboarding(BflResearchAvailability availability)
    {
        var research = availability.CanResearch
            ? $"Mit der aktuellen Einrichtung und dem hier gewählten Anbieter kann das Lab im Web recherchieren. Im Schritt „Strategie“ recherchieren eigene Recherche-Agenten die Veranstaltung, ihre Teilnehmenden sowie die öffentliche Debatte und das Framing zum Thema. Sie erhalten ausschließlich die öffentlichen Angaben zur Veranstaltung ({PublicFields()}). Alle anderen Angaben, das Material, die Arbeitsstände und die Gespräche bleiben in der Vorbereitung und erreichen keine Suche. {NOT_PUBLIC}"
            : $"Mit der aktuellen Einrichtung und dem hier gewählten Anbieter kann das Lab nicht im Web recherchieren. {ReasonFor(availability.WebSearch)} Gibt es die Recherche, erhält sie ausschließlich die öffentlichen Angaben zur Veranstaltung ({PublicFields()}); alle anderen Angaben bleiben in der Vorbereitung.";

        var readWebPage = availability.ReadWebPage is ToolOfferBlockReason.NONE
            ? "Die Gespräche der Schritte können außerdem Webseiten lesen, deren Adressen die Person angibt, etwa den Link zur Veranstaltung. Selbst im Web suchen sie nie, weil sie die ganze Vorbereitung kennen."
            : $"Webseiten lesen können die Gespräche der Schritte mit der aktuellen Einrichtung nicht. {ReasonFor(availability.ReadWebPage)}";

        return $"""
                {research}

                {readWebPage}
                """;
    }

    /// <summary>
    /// Tells the model why a tool cannot be offered, in German.
    /// </summary>
    /// <param name="reason">What keeps the tool from being offered.</param>
    /// <returns>The reason, as one sentence.</returns>
    public static string ReasonFor(ToolOfferBlockReason reason) => reason switch
    {
        ToolOfferBlockReason.NONE => "Nichts steht im Weg.",
        ToolOfferBlockReason.TOOLS_SWITCHED_OFF => "Die Organisation der Person hat Werkzeuge in AI Studio abgeschaltet.",
        ToolOfferBlockReason.MODEL_CANNOT_USE_TOOLS => "Das gewählte Modell kann keine Werkzeuge nutzen; mit einem anderen Anbieter kann es gehen.",
        ToolOfferBlockReason.NOT_AVAILABLE_HERE => "Das Werkzeug steht im Lab nicht zur Verfügung.",
        ToolOfferBlockReason.TOOL_SWITCHED_OFF => "Die Organisation der Person hat das Werkzeug abgeschaltet.",
        ToolOfferBlockReason.TOOL_SWITCHED_OFF_BY_USER => "Die Person hat das Werkzeug in den Einstellungen der Werkzeuge abgeschaltet und kann es dort wieder einschalten.",
        ToolOfferBlockReason.NOT_CONFIGURED => "Das Werkzeug ist noch nicht eingerichtet; die Person kann es in den Einstellungen der Werkzeuge einrichten.",
        ToolOfferBlockReason.PROVIDER_CONFIDENCE_TOO_LOW => "Dem gewählten Anbieter wird für dieses Werkzeug nicht genug vertraut.",
        ToolOfferBlockReason.OUTBOUND_DATA_RESTRICTED => "Das Werkzeug darf von hier aus keine Daten senden.",

        // A reason this version does not know:
        _ => "Das Werkzeug lässt sich hier nicht nutzen.",
    };

    /// <summary>
    /// Tells the chat of a step whether the research is available.
    /// </summary>
    /// <param name="availability">What keeps the tools of the lab from being offered.</param>
    private static string ResearchStatus(BflResearchAvailability availability) => availability.CanResearch
        ? "Mit der aktuellen Einrichtung ist die Recherche verfügbar."
        : $"Mit der aktuellen Einrichtung ist die Recherche nicht verfügbar. {ReasonFor(availability.WebSearch)}";

    /// <summary>
    /// Names the fields which the research receives, from the catalog, so that the text never promises less than it does.
    /// </summary>
    private static string PublicFields() => string.Join(", ", BflFieldCatalog.FIELDS.Where(field => field.IsSharedWithResearch).Select(field => $"„{field.Label}“"));
}