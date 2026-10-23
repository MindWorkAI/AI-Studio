namespace AIStudio.Tools.PowerPoint;

internal static partial class LayoutPurposeExtensions
{
    private static LayoutPurposeInfo? DescribeBusiness(LayoutPurpose purpose) => purpose switch
    {
        LayoutPurpose.VISION => Business(TB("Layout for the vision"), TB("Which slide layout should a vision use?"), "a vision, i.e., the picture of where to go"),
        LayoutPurpose.GOAL => Business(TB("Layout for one goal"), TB("Which slide layout should a single goal use, shown prominently?"), "a single goal, shown prominently"),
        LayoutPurpose.GOALS => Business(TB("Layout for several goals"), TB("Which slide layout should several goals use, shown together?"), "several goals, shown together"),
        LayoutPurpose.NARRATIVE => Business(TB("Layout for the narrative"), TB("Which slide layout should a narrative use, which frames, e.g., a project?"), "a narrative which frames, e.g., a project"),
        LayoutPurpose.STORY => Business(TB("Layout for stories"), TB("Which slide layout should a story use, e.g., in the storytelling of a keynote?"), "a story, e.g., in the storytelling of a keynote"),
        LayoutPurpose.PROBLEM => Business(TB("Layout for one problem"), TB("Which slide layout should a single problem use, shown prominently?"), "a single problem, shown prominently"),
        LayoutPurpose.PROBLEMS => Business(TB("Layout for several problems"), TB("Which slide layout should several problems use, shown together?"), "several problems, shown together"),
        LayoutPurpose.OPPORTUNITY => Business(TB("Layout for one opportunity"), TB("Which slide layout should a single opportunity use, shown prominently?"), "a single opportunity, shown prominently"),
        LayoutPurpose.OPPORTUNITIES => Business(TB("Layout for several opportunities"), TB("Which slide layout should several opportunities use, shown together?"), "several opportunities, shown together"),
        LayoutPurpose.SWOT => Business(TB("Layout for SWOT analyses"), TB("Which slide layout should a SWOT analysis use?"), "a SWOT analysis of strengths, weaknesses, opportunities, and threats"),
        LayoutPurpose.USE_CASE => Business(TB("Layout for one use case"), TB("Which slide layout should a single use case use, shown prominently?"), "a single use case, shown prominently"),
        LayoutPurpose.USE_CASES => Business(TB("Layout for several use cases"), TB("Which slide layout should several use cases use, shown together?"), "several use cases, shown together"),
        LayoutPurpose.USER_STORY => Business(TB("Layout for one user story"), TB("Which slide layout should a single user story use, shown prominently?"), "a single user story, shown prominently"),
        LayoutPurpose.USER_STORIES => Business(TB("Layout for several user stories"), TB("Which slide layout should several user stories use, shown together?"), "several user stories, shown together"),
        LayoutPurpose.ACCEPTANCE_CRITERIA => Business(TB("Layout for acceptance criteria"), TB("Which slide layout should acceptance criteria use?"), "acceptance criteria"),
        LayoutPurpose.KPI => Business(TB("Layout for one KPI"), TB("Which slide layout should a single key figure use, shown prominently?"), "a single key figure (KPI), shown prominently"),
        LayoutPurpose.KPIS => Business(TB("Layout for an overview of KPIs"), TB("Which slide layout should an overview of several key figures use?"), "an overview of several key figures (KPIs)"),
        LayoutPurpose.FORECAST => Business(TB("Layout for forecasts"), TB("Which slide layout should a forecast use?"), "a forecast"),
        LayoutPurpose.SCENARIO => Business(TB("Layout for one scenario"), TB("Which slide layout should a single scenario use, shown prominently?"), "a single scenario, shown prominently"),
        LayoutPurpose.SCENARIOS => Business(TB("Layout for several scenarios"), TB("Which slide layout should several scenarios use, shown together?"), "several scenarios, shown together"),
        LayoutPurpose.RISK => Business(TB("Layout for one risk"), TB("Which slide layout should a single risk use, shown prominently?"), "a single risk, shown prominently"),
        LayoutPurpose.RISKS => Business(TB("Layout for several risks"), TB("Which slide layout should several risks use, shown together?"), "several risks, shown together"),
        LayoutPurpose.DEPENDENCY => Business(TB("Layout for one dependency"), TB("Which slide layout should a single dependency and its countermeasures use?"), "a single dependency and its countermeasures, shown prominently"),
        LayoutPurpose.DEPENDENCIES => Business(TB("Layout for several dependencies"), TB("Which slide layout should several dependencies and their countermeasures use?"), "several dependencies and their countermeasures, shown together"),
        LayoutPurpose.RESOURCES => Business(TB("Layout for resources"), TB("Which slide layout should resources use, e.g., costs, people, or machines?"), "resources, e.g., costs, people, or machines"),
        LayoutPurpose.TEAM => Business(TB("Layout for the team"), TB("Which slide layout should a slide use which presents a team?"), "a slide which presents a team"),
        LayoutPurpose.PARTNERS => Business(TB("Layout for partners"), TB("Which slide layout should a slide use which presents partners, e.g., with their logos?"), "a slide which presents partners, e.g., with their logos"),
        LayoutPurpose.ROADMAP => Business(TB("Layout for roadmaps"), TB("Which slide layout should a roadmap use?"), "a roadmap"),
        LayoutPurpose.MILESTONE => Business(TB("Layout for one milestone"), TB("Which slide layout should a single milestone use, e.g., with a report?"), "a single milestone, e.g., with a report or a description"),
        LayoutPurpose.MILESTONES => Business(TB("Layout for an overview of milestones"), TB("Which slide layout should an overview of several milestones use?"), "an overview of several milestones"),
        LayoutPurpose.WORK_PACKAGE => Business(TB("Layout for one work package"), TB("Which slide layout should a single work package use, e.g., in a project report?"), "a single work package, e.g., in a project report"),
        LayoutPurpose.WORK_PACKAGES => Business(TB("Layout for an overview of work packages"), TB("Which slide layout should an overview of several work packages use?"), "an overview of several work packages"),
        LayoutPurpose.PROJECT_STATUS => Business(TB("Layout for the project status"), TB("Which slide layout should the status of a project use, e.g., as a traffic light?"), "the status of a project, e.g., as a traffic light"),
        LayoutPurpose.LESSONS_LEARNED => Business(TB("Layout for lessons learned"), TB("Which slide layout should lessons learned use?"), "lessons learned"),
        LayoutPurpose.REASON => Business(TB("Layout for one reason"), TB("Which slide layout should a single reason use, shown prominently?"), "a single reason or justification, shown prominently"),
        LayoutPurpose.REASONS => Business(TB("Layout for several reasons"), TB("Which slide layout should several reasons use, shown together?"), "several reasons or justifications, shown together"),
        LayoutPurpose.RECOMMENDATION => Business(TB("Layout for one recommendation"), TB("Which slide layout should a single recommendation use, shown prominently?"), "a single recommendation, shown prominently"),
        LayoutPurpose.RECOMMENDATIONS => Business(TB("Layout for several recommendations"), TB("Which slide layout should several recommendations use, shown together?"), "several recommendations, shown together"),
        LayoutPurpose.DECISION_REQUEST => Business(TB("Layout for decision requests"), TB("Which slide layout should a decision use which the audience is asked to make?"), "a decision which the audience is asked to make"),
        LayoutPurpose.NEXT_STEPS => Business(TB("Layout for next steps"), TB("Which slide layout should the next steps use?"), "the next steps"),
        LayoutPurpose.CALL_TO_ACTION => Business(TB("Layout for calls to action"), TB("Which slide layout should a call to action use?"), "a call to action"),

        _ => null,
    };

    private static LayoutPurposeInfo Business(string label, string question, string promptDescription) => new(LayoutPurposeArea.BUSINESS, label, question, promptDescription);
}