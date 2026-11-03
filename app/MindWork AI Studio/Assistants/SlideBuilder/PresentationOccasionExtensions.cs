namespace AIStudio.Assistants.SlideBuilder;

public static class PresentationOccasionExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(PresentationOccasionExtensions).Namespace, nameof(PresentationOccasionExtensions));

    public static string Name(this PresentationOccasion occasion) => occasion switch
    {
        PresentationOccasion.UNSPECIFIED => TB("Unspecified occasion"),
        PresentationOccasion.TALK => TB("Talk"),
        PresentationOccasion.KEYNOTE => TB("Keynote"),
        PresentationOccasion.CONFERENCE_TALK => TB("Conference talk"),
        PresentationOccasion.LECTURE => TB("Lecture"),
        PresentationOccasion.TRAINING => TB("Training"),
        PresentationOccasion.WORKSHOP => TB("Workshop of one day"),
        PresentationOccasion.MULTI_DAY_WORKSHOP => TB("Workshop of several days"),
        PresentationOccasion.THESIS_DEFENSE => TB("Defense of a thesis or dissertation"),
        PresentationOccasion.RESEARCH_RESULTS => TB("Presentation of research results"),
        PresentationOccasion.PROJECT_REPORT => TB("Project or status report"),
        PresentationOccasion.MANAGEMENT_REPORT => TB("Report to management"),
        PresentationOccasion.PITCH => TB("Pitch"),
        PresentationOccasion.PRODUCT_PRESENTATION => TB("Product presentation"),
        PresentationOccasion.KICK_OFF => TB("Kick-off"),
        PresentationOccasion.TEAM_MEETING => TB("Team meeting"),
        PresentationOccasion.OTHER => TB("Other occasion"),

        _ => TB("Unspecified occasion"),
    };

    /// <summary>
    /// Tells the model what the occasion means for the structure of the presentation.
    /// </summary>
    /// <param name="occasion">The occasion.</param>
    /// <param name="customOccasion">The occasion the user described, for OTHER.</param>
    public static string Prompt(this PresentationOccasion occasion, string customOccasion) => occasion switch
    {
        PresentationOccasion.TALK => "The presentation accompanies a talk. Structure it for a speaker who talks the audience through it.",
        PresentationOccasion.KEYNOTE => "The presentation accompanies a keynote. Favor few words per slide, strong key messages, and a story which carries the audience.",
        PresentationOccasion.CONFERENCE_TALK => "The presentation accompanies a talk at a conference. Keep to the essentials of a short time slot, and end with the key messages and room for questions.",
        PresentationOccasion.LECTURE => "The presentation accompanies a lecture. Teach step by step, with examples, and where it suits, end chapters with an interim summary or with room for questions.",
        PresentationOccasion.TRAINING => "The presentation accompanies a training. Teach practical skills step by step, with examples and exercises where the content offers them.",
        PresentationOccasion.WORKSHOP => "The presentation accompanies a workshop of one day. Plan for interaction, e.g., exercises, group work, or discussions, and for the breaks the schedule of the content has.",
        PresentationOccasion.MULTI_DAY_WORKSHOP => "The presentation accompanies a workshop of several days. Plan for interaction, mark where a day ends and the next one starts, and look back at the start of each day, following the schedule of the content.",
        PresentationOccasion.THESIS_DEFENSE => "The presentation defends a thesis or a dissertation before a committee. Present the research questions, the method, the results, and the contribution precisely, and anticipate critical questions.",
        PresentationOccasion.RESEARCH_RESULTS => "The presentation presents research results. Present the questions, the method, the results, and their meaning precisely and comprehensibly.",
        PresentationOccasion.PROJECT_REPORT => "The presentation reports on a project. Present the status, the progress, the problems, and the next steps clearly.",
        PresentationOccasion.MANAGEMENT_REPORT => "The presentation reports to management and may prepare a decision. Lead with the key messages, keep details short, and state clearly what is to be decided.",
        PresentationOccasion.PITCH => "The presentation pitches an idea, a product, or an offer. Lead with the benefit for the audience, and end with a clear call to action.",
        PresentationOccasion.PRODUCT_PRESENTATION => "The presentation presents a product. Show what it does and what it brings the audience, with examples.",
        PresentationOccasion.KICK_OFF => "The presentation opens a project at its kick-off. Present the goals, the team, the plan, and how the work is organized.",
        PresentationOccasion.TEAM_MEETING => "The presentation serves a team meeting. Keep it short and practical, with clear points and next steps.",
        PresentationOccasion.OTHER => $"The presentation is for this occasion: {customOccasion}. Structure it accordingly.",

        _ => "No particular occasion is given. Structure the presentation by its content.",
    };
}