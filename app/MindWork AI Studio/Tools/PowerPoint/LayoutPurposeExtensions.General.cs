namespace AIStudio.Tools.PowerPoint;

internal static partial class LayoutPurposeExtensions
{
    private static LayoutPurposeInfo? DescribeGeneral(LayoutPurpose purpose) => purpose switch
    {
        LayoutPurpose.CONTENT => General(TB("Default layout for content"), TB("Which slide layout should ordinary slides with content use by default?"), "ordinary slides with content; use another layout only where a slide needs other slots, e.g., for a picture or a table, or where another choice here applies"),
        LayoutPurpose.LITTLE_CONTENT => General(TB("Layout for little content"), TB("Which slide layout should slides with little content use, e.g., a short statement?"), "slides with little content, e.g., a short statement, where no other choice here applies"),
        LayoutPurpose.MUCH_CONTENT => General(TB("Layout for much content"), TB("Which slide layout should slides with much content use, e.g., many points or a longer explanation?"), "slides with much content, e.g., many points or a longer explanation"),
        LayoutPurpose.KEY_MESSAGE => General(TB("Layout for a key message"), TB("Which slide layout should a key message use, which states the one thing to remember?"), "a key message, which states the one thing the audience should remember"),
        LayoutPurpose.EXECUTIVE_SUMMARY => General(TB("Layout for the executive summary"), TB("Which slide layout should an executive summary use, which sums up the presentation for decision makers?"), "an executive summary, which sums up the whole presentation for decision makers, usually at its start"),
        LayoutPurpose.CONTEXT => General(TB("Layout for the context"), TB("Which slide layout should the context or the starting point use?"), "the context or the starting point of the topic"),
        LayoutPurpose.RETROSPECTIVE => General(TB("Layout for a look back"), TB("Which slide layout should a look back on where we come from use?"), "a look back on what happened so far, i.e., where we come from"),
        LayoutPurpose.PICTURE => General(TB("Layout for one picture"), TB("Which slide layout should a single picture use, shown prominently, e.g., a screenshot, a photo, or a sketch?"), "a single attached picture, shown prominently, e.g., a screenshot, a photo, or a sketch"),
        LayoutPurpose.PICTURES => General(TB("Layout for several pictures"), TB("Which slide layout should several pictures use, shown together?"), "several attached pictures, shown together"),
        LayoutPurpose.TABLE => General(TB("Layout for tables"), TB("Which slide layout should a slide with a table use?"), "a slide with a table"),
        LayoutPurpose.COMPARISON => General(TB("Layout for comparisons"), TB("Which slide layout should comparisons use, e.g., before and after, or two options?"), "comparisons, e.g., before and after, or two options side by side"),
        LayoutPurpose.PROS_AND_CONS => General(TB("Layout for pros and cons"), TB("Which slide layout should pros and cons use?"), "the pros and cons of one thing"),
        LayoutPurpose.TIMELINE => General(TB("Layout for timelines"), TB("Which slide layout should timelines use, e.g., the course of a project?"), "timelines, e.g., the course of a project"),
        LayoutPurpose.PROCESS => General(TB("Layout for processes"), TB("Which slide layout should processes use, e.g., the steps of a workflow?"), "processes, e.g., the steps of a workflow"),
        LayoutPurpose.QUOTE => General(TB("Layout for one quote"), TB("Which slide layout should a single quote use, shown prominently?"), "a single quote, shown prominently"),
        LayoutPurpose.QUOTES => General(TB("Layout for several quotes"), TB("Which slide layout should several quotes use, shown together?"), "several quotes, shown together"),
        LayoutPurpose.CODE => General(TB("Layout for source code"), TB("Which slide layout should source code use?"), "source code"),
        LayoutPurpose.CHECKLIST => General(TB("Layout for checklists"), TB("Which slide layout should a checklist use?"), "a checklist"),
        LayoutPurpose.AUDIENCE_QUESTION => General(TB("Layout for questions to the audience"), TB("Which slide layout should questions to the audience during the presentation use?"), "questions to the audience during the presentation"),
        LayoutPurpose.FEEDBACK => General(TB("Layout for feedback"), TB("Which slide layout should feedback use, e.g., of customers or users?"), "feedback, e.g., of customers or users"),
        LayoutPurpose.DISCLAIMER => General(TB("Layout for disclaimers"), TB("Which slide layout should conflicts of interest and disclaimers use?"), "conflicts of interest and disclaimers"),
        LayoutPurpose.SUMMARY => General(TB("Layout for the summary"), TB("Which slide layout should a summary or a conclusion use?"), "a summary or a conclusion"),
        LayoutPurpose.OUTLOOK => General(TB("Layout for the outlook"), TB("Which slide layout should an outlook on what comes next use?"), "an outlook on what comes next"),

        _ => null,
    };

    private static LayoutPurposeInfo General(string label, string question, string promptDescription) => new(LayoutPurposeArea.GENERAL, label, question, promptDescription);
}