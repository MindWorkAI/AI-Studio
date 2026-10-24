namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// A kind of slide for which the user may choose the layout of the template.
/// </summary>
/// <remarks>
/// A choice says which layout a kind of slide uses, not whether the presentation has such a slide
/// at all. The members are grouped by their LayoutPurposeArea, in the order of the areas, and within
/// an area they roughly follow the course of a presentation; the forms list them in this order. A
/// kind which comes as a single item or as several gets a member for each, because a template
/// usually shows one item prominently and several as an overview. What the user and the model read
/// about each member is in LayoutPurposeExtensions, one file per area.<br/><br/>
/// Public, because the settings store the choices by these members. They are stored by name, so
/// members may be added anywhere, but a renamed member loses the choices made for it.
/// </remarks>
public enum LayoutPurpose
{
    //
    // Structure and framing:
    //
    TITLE,
    TABLE_OF_CONTENTS,
    AGENDA,
    SPEAKER,
    SPEAKERS,
    CHAPTER,
    CHAPTER_SUMMARY,
    Q_AND_A,
    ACKNOWLEDGEMENTS,
    REFERENCES,
    CLOSING,
    APPENDIX,
    IMPRINT,

    //
    // General content:
    //
    CONTENT,
    LITTLE_CONTENT,
    MUCH_CONTENT,
    KEY_MESSAGE,
    EXECUTIVE_SUMMARY,
    CONTEXT,
    RETROSPECTIVE,
    PICTURE,
    PICTURES,
    TABLE,
    COMPARISON,
    PROS_AND_CONS,
    TIMELINE,
    PROCESS,
    QUOTE,
    QUOTES,
    CODE,
    FORMULA,
    FORMULAS,
    CHECKLIST,
    NOTE,
    NOTES,
    AUDIENCE_QUESTION,
    FEEDBACK,
    DISCLAIMER,
    SUMMARY,
    OUTLOOK,

    //
    // Teaching and workshops:
    //
    INTRODUCTION_ROUND,
    EXPECTATIONS,
    GROUND_RULES,
    ORGANIZATIONAL_NOTES,
    LEARNING_OBJECTIVE,
    LEARNING_OBJECTIVES,
    EXAMPLE,
    EXAMPLES,
    STEP_BY_STEP,
    EXERCISE,
    EXERCISE_SOLUTION,
    GROUP_WORK,
    POLL,
    DISCUSSION_PROMPT,
    REFLECTION,
    BREAK,
    DAY_CHANGE,

    //
    // Business and projects:
    //
    VISION,
    GOAL,
    GOALS,
    NARRATIVE,
    STORY,
    PROBLEM,
    PROBLEMS,
    OPPORTUNITY,
    OPPORTUNITIES,
    SWOT,
    USE_CASE,
    USE_CASES,
    USER_STORY,
    USER_STORIES,
    ACCEPTANCE_CRITERIA,
    KPI,
    KPIS,
    FORECAST,
    SCENARIO,
    SCENARIOS,
    RISK,
    RISKS,
    DEPENDENCY,
    DEPENDENCIES,
    RESOURCES,
    TEAM,
    PARTNERS,
    ROADMAP,
    MILESTONE,
    MILESTONES,
    WORK_PACKAGE,
    WORK_PACKAGES,
    PROJECT_STATUS,
    LESSONS_LEARNED,
    REASON,
    REASONS,
    RECOMMENDATION,
    RECOMMENDATIONS,
    DECISION_REQUEST,
    NEXT_STEPS,
    ACTION_ITEMS,
    CALL_TO_ACTION,

    //
    // Science and research:
    //
    STATE_OF_RESEARCH,
    RESEARCH_GAP,
    RESEARCH_QUESTION,
    RESEARCH_QUESTIONS,
    DEFINITION,
    DEFINITIONS,
    THEOREM,
    PROOF,
    ASSUMPTION,
    ASSUMPTIONS,
    DELIMITATION,
    DELIMITATIONS,
    THESIS,
    THESES,
    HYPOTHESIS,
    HYPOTHESES,
    ARGUMENT_CHAIN,
    COUNTERARGUMENT,
    COUNTERARGUMENTS,
    METHODOLOGY,
    STUDY_DESIGN,
    EXPERIMENTAL_SETUP,
    DATA,
    RESULT,
    RESULTS,
    INTERPRETATION,
    LIMITATIONS,
    CONTRIBUTION,
    FUNDING,

    //
    // Medicine:
    //
    PATIENT_CASE,
    MEDICAL_HISTORY,
    CLINICAL_FINDINGS,
    IMAGING,
    LAB_VALUES,
    DIAGNOSIS,
    DIFFERENTIAL_DIAGNOSES,
    THERAPY,
    MEDICATION,
    COURSE_OF_DISEASE,
    PROGNOSIS,
    ADVERSE_EVENTS,
    GUIDELINE_RECOMMENDATION,
    STUDY_POPULATION,
    ENDPOINTS,
    ETHICS_APPROVAL,

    //
    // Law:
    //
    FACTS_OF_THE_CASE,
    LEGAL_QUESTION,
    LEGAL_QUESTIONS,
    LEGAL_PROVISION,
    LEGAL_PROVISIONS,
    EXAMINATION_SCHEME,
    DISPUTE_OF_OPINIONS,
    POSITIONS_OF_THE_PARTIES,
    PIECE_OF_EVIDENCE,
    PIECES_OF_EVIDENCE,
    RULING,
    RULINGS,
    CONTRACT_CLAUSE,
    CONTRACT_CLAUSES,
    LEGAL_CONSEQUENCES,
    DEADLINES,
}