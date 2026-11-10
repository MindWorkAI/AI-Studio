namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Every section of the work states of the BeyondFacts Lab, step by step.
/// </summary>
/// <remarks>
/// The work states store their texts under the keys of the sections, so these keys never change. The sections follow
/// what the prompt of each step asks the conversation to clarify. The titles come from BflWorkStateSectionTexts.
/// </remarks>
public static class BflWorkStateSections
{
    /// <summary>
    /// The key of the section which every work state ends with: what is still open.
    /// </summary>
    public const string OPEN_POINTS = "OPEN_POINTS";

    /// <summary>
    /// Every section, step by step, in the order a work state shows them.
    /// </summary>
    public static readonly IReadOnlyList<BflWorkStateSection> SECTIONS =
    [
        new("FORMAT", BflStep.FRAMEWORK, "Format und Setting: online, vor Ort oder hybrid, Ablauf, Veranstalter und Moderation."),
        new("AUDIENCE", BflStep.FRAMEWORK, "Publikum: an wen sich die Veranstaltung richtet; bei Medienformaten die Zuschauenden oder Zuhörenden."),
        new("SPEAKING_TIME", BflStep.FRAMEWORK, "Dauer der Veranstaltung, Dauer des Beitrags und reale Redezeit der Person."),
        new("PARTICIPANTS", BflStep.FRAMEWORK, "Weitere Teilnehmende, soweit die Person sie angegeben hat oder ihr Material sie nennt."),
        new("CONFLICTS", BflStep.FRAMEWORK, "Mögliche Konfliktstruktur: Evidenz-, Ziel-, Wert- oder Interessenkonflikte, als Hypothese."),
        new(OPEN_POINTS, BflStep.FRAMEWORK, "Offene Punkte, die die Person noch klären oder recherchieren möchte."),

        new("ESTABLISHED", BflStep.KNOWLEDGE, "Gesicherte Aussagen der Person und worauf sie beruhen."),
        new("UNCERTAINTIES", BflStep.KNOWLEDGE, "Unsicherheiten und Grenzen des Wissens."),
        new("WEAK_POINTS", BflStep.KNOWLEDGE, "Schwachstellen und mögliche Kritikpunkte ihrer Arbeit."),
        new("CONTROVERSIES", BflStep.KNOWLEDGE, "Kontroversen im Fach."),
        new("EXPERTISE_LIMITS", BflStep.KNOWLEDGE, "Wo die Expertise der Person endet."),
        new("KEY_MESSAGES", BflStep.KNOWLEDGE, "Ein bis zwei mögliche Kernbotschaften."),
        new(OPEN_POINTS, BflStep.KNOWLEDGE, "Offene Punkte und Wissenslücken."),

        new("GOAL", BflStep.GOALS_AND_ROLE, "Was die Person mit ihrem Beitrag erreichen möchte."),
        new("ROLE", BflStep.GOALS_AND_ROLE, "In welcher Rolle sie spricht, etwa nach Pielke, mit welcher Verantwortung und welchen Interessen."),
        new("REASONS", BflStep.GOALS_AND_ROLE, "Eine kurze Begründung für Ziel und Rolle."),
        new("CONSEQUENCES", BflStep.GOALS_AND_ROLE, "Folgen von Ziel und Rolle für ihren Auftritt."),
        new(OPEN_POINTS, BflStep.GOALS_AND_ROLE, "Offene Punkte."),

        new("VALUES", BflStep.VALUES_AND_PERSPECTIVES, "Für den Anlass wichtige Werte und ihre Bedeutung."),
        new("VALUE_CONFLICTS", BflStep.VALUES_AND_PERSPECTIVES, "Erkannte Wertkonflikte und die Abwägungen der Person."),
        new("PERSONAL_INVOLVEMENT", BflStep.VALUES_AND_PERSPECTIVES, "Erkenntnisse zur persönlichen Betroffenheit, nur soweit die Person sie ausdrücklich übernehmen möchte; sonst leer.") { IsPersonalDisclosure = true },
        new("PERSPECTIVES", BflStep.VALUES_AND_PERSPECTIVES, "Berücksichtigte Perspektiven, jeweils mit dem Hinweis, ob sie auf Aussagen Betroffener oder auf Vermutungen beruhen."),
        new("CONSEQUENCES", BflStep.VALUES_AND_PERSPECTIVES, "Folgen für die Position oder die Kommunikation der Person."),
        new(OPEN_POINTS, BflStep.VALUES_AND_PERSPECTIVES, "Offene Fragen und Annahmen, die die Person überprüfen möchte."),

        new("KEY_MESSAGES", BflStep.STRATEGY, "Kernbotschaften."),
        new("STRUCTURE", BflStep.STRATEGY, "Einstieg und Struktur des Beitrags."),
        new("LANGUAGE", BflStep.STRATEGY, "Sprache: Begriffe, Bilder und Beispiele für das Publikum."),
        new("UNCERTAINTY", BflStep.STRATEGY, "Wie die Person Evidenz, Wertabwägungen und Unsicherheit erkennbar macht."),
        new("FRAMING", BflStep.STRATEGY, "Framing des Themas und des eigenen Beitrags."),
        new("PARTICIPANTS", BflStep.STRATEGY, "Teilnehmende und Gegenüber: wer außer der Person spricht oder moderiert, je mit Rolle und den öffentlichen Positionen zum Thema, soweit die Recherche, die Angaben oder das Material sie belegen, mit Quelle. Nur, was die Person im Gespräch bestätigt hat; sonst leer."),
        new("COUNTERARGUMENTS", BflStep.STRATEGY, "Gegenargumente und wie die Person ihnen begegnet."),
        new("WORRIES", BflStep.STRATEGY, "Umgang mit den kommunikativen Sorgen der Person."),
        new(OPEN_POINTS, BflStep.STRATEGY, "Offene Punkte, etwa für die Recherche."),

        new("STRENGTHS", BflStep.TEST_SITUATION, "Was der Person schon gut gelingt."),
        new("PRACTICE", BflStep.TEST_SITUATION, "Wo die Person weiter üben sollte."),
        new("HARD_QUESTIONS", BflStep.TEST_SITUATION, "Die schwierigsten Fragen mit Ansätzen für Antworten."),
        new("POSITION", BflStep.TEST_SITUATION, "Ob sich die Position der Person, ihre Begründung oder nur ihre Darstellung verändert hat."),
        new(OPEN_POINTS, BflStep.TEST_SITUATION, "Offene Punkte."),

        new("CHANGED", BflStep.RESULTS, "Was die Person gegenüber ihrer Ausgangsposition verändert hat, und weshalb."),
        new("KEPT", BflStep.RESULTS, "Was sie beibehält, und weshalb."),
        new("LEFT_OPEN", BflStep.RESULTS, "Was sie offenlässt, und weshalb."),
        new("NEXT_STEPS", BflStep.RESULTS, "Nächste Schritte bis zur Veranstaltung."),
    ];

    /// <summary>
    /// Gets the sections of the work state of a step, in the order a work state shows them.
    /// </summary>
    /// <param name="step">The step.</param>
    public static IReadOnlyList<BflWorkStateSection> ForStep(BflStep step) => [.. SECTIONS.Where(section => section.Step == step)];
}