namespace AIStudio.Tools.PowerPoint;

internal static partial class LayoutPurposeExtensions
{
    private static LayoutPurposeInfo? DescribeTeaching(LayoutPurpose purpose) => purpose switch
    {
        LayoutPurpose.INTRODUCTION_ROUND => Teaching(TB("Layout for introduction rounds"), TB("Which slide layout should a round use in which the participants introduce themselves?"), "a round in which the participants introduce themselves"),
        LayoutPurpose.EXPECTATIONS => Teaching(TB("Layout for expectations"), TB("Which slide layout should collecting the expectations of the participants use?"), "collecting the expectations of the participants"),
        LayoutPurpose.GROUND_RULES => Teaching(TB("Layout for ground rules"), TB("Which slide layout should the ground rules for working together use?"), "the ground rules for working together"),
        LayoutPurpose.ORGANIZATIONAL_NOTES => Teaching(TB("Layout for organizational notes"), TB("Which slide layout should organizational notes use, e.g., on rooms or Wi-Fi?"), "organizational notes, e.g., on rooms or Wi-Fi"),
        LayoutPurpose.LEARNING_OBJECTIVE => Teaching(TB("Layout for one learning objective"), TB("Which slide layout should a single learning objective use, shown prominently?"), "a single learning objective, shown prominently"),
        LayoutPurpose.LEARNING_OBJECTIVES => Teaching(TB("Layout for several learning objectives"), TB("Which slide layout should several learning objectives use, shown together?"), "several learning objectives, shown together"),
        LayoutPurpose.EXAMPLE => Teaching(TB("Layout for one example"), TB("Which slide layout should a single example or case study use?"), "a single example or case study"),
        LayoutPurpose.EXAMPLES => Teaching(TB("Layout for several examples"), TB("Which slide layout should several examples use, shown together?"), "several examples, shown together"),
        LayoutPurpose.STEP_BY_STEP => Teaching(TB("Layout for step-by-step explanations"), TB("Which slide layout should a step-by-step explanation use?"), "a step-by-step explanation"),
        LayoutPurpose.EXERCISE => Teaching(TB("Layout for exercises"), TB("Which slide layout should an exercise use, with its task, time frame, and expected result?"), "an exercise with its task, its time frame, and the expected result"),
        LayoutPurpose.EXERCISE_SOLUTION => Teaching(TB("Layout for solutions of exercises"), TB("Which slide layout should the solution of an exercise use?"), "the solution of an exercise"),
        LayoutPurpose.GROUP_WORK => Teaching(TB("Layout for group work"), TB("Which slide layout should group work use, e.g., the groups and their tasks?"), "group work, e.g., the groups and their tasks"),
        LayoutPurpose.POLL => Teaching(TB("Layout for polls"), TB("Which slide layout should a poll, a vote, or a knowledge check use?"), "a poll, a vote, or a knowledge check"),
        LayoutPurpose.DISCUSSION_PROMPT => Teaching(TB("Layout for discussion prompts"), TB("Which slide layout should a prompt for a discussion use?"), "a prompt which starts a discussion"),
        LayoutPurpose.REFLECTION => Teaching(TB("Layout for reflection"), TB("Which slide layout should reflecting on and recording the results use?"), "reflecting on and recording the results"),
        LayoutPurpose.BREAK => Teaching(TB("Layout for breaks"), TB("Which slide layout should a break use?"), "a break, only where the schedule of the content has one"),
        LayoutPurpose.DAY_CHANGE => Teaching(TB("Layout for the change of days"), TB("Which slide layout should the end of a day or the start of the next use in a workshop of several days?"), "the end of one day or the start of the next in a workshop of several days"),

        _ => null,
    };

    private static LayoutPurposeInfo Teaching(string label, string question, string promptDescription) => new(LayoutPurposeArea.TEACHING, label, question, promptDescription);
}