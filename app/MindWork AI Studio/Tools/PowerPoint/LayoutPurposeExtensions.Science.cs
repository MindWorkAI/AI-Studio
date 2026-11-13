namespace AIStudio.Tools.PowerPoint;

internal static partial class LayoutPurposeExtensions
{
    private static LayoutPurposeInfo? DescribeScience(LayoutPurpose purpose) => purpose switch
    {
        LayoutPurpose.STATE_OF_RESEARCH => Science(TB("Layout for the state of research"), TB("Which slide layout should the state of research use?"), "the state of research"),
        LayoutPurpose.RESEARCH_GAP => Science(TB("Layout for the research gap"), TB("Which slide layout should the research gap use?"), "the research gap, i.e., what the state of research leaves open"),
        LayoutPurpose.RESEARCH_QUESTION => Science(TB("Layout for one research question"), TB("Which slide layout should a single research question use, shown prominently?"), "a single research question, shown prominently"),
        LayoutPurpose.RESEARCH_QUESTIONS => Science(TB("Layout for several research questions"), TB("Which slide layout should several research questions use, shown together?"), "several research questions, shown together"),
        LayoutPurpose.DEFINITION => Science(TB("Layout for one definition"), TB("Which slide layout should the definition of a single term use?"), "the definition of a single term, shown prominently"),
        LayoutPurpose.DEFINITIONS => Science(TB("Layout for several definitions"), TB("Which slide layout should the definitions of several terms use?"), "the definitions of several terms, shown together"),
        LayoutPurpose.THEOREM => Science(TB("Layout for theorems"), TB("Which slide layout should a theorem use, e.g., in mathematics?"), "a theorem, e.g., in mathematics, with its premises and its claim"),
        LayoutPurpose.PROOF => Science(TB("Layout for proofs"), TB("Which slide layout should the proof of a theorem use?"), "the proof of a theorem, step by step"),
        LayoutPurpose.ASSUMPTION => Science(TB("Layout for one assumption"), TB("Which slide layout should a single assumption use, shown prominently?"), "a single assumption, shown prominently"),
        LayoutPurpose.ASSUMPTIONS => Science(TB("Layout for several assumptions"), TB("Which slide layout should several assumptions use, shown together?"), "several assumptions, shown together"),
        LayoutPurpose.DELIMITATION => Science(TB("Layout for one delimitation"), TB("Which slide layout should a single delimitation use, i.e., what the work leaves out?"), "a single delimitation, i.e., what the work deliberately leaves out, shown prominently"),
        LayoutPurpose.DELIMITATIONS => Science(TB("Layout for several delimitations"), TB("Which slide layout should several delimitations use, shown together?"), "several delimitations, i.e., what the work deliberately leaves out, shown together"),
        LayoutPurpose.THESIS => Science(TB("Layout for one thesis"), TB("Which slide layout should a single thesis or central claim use, shown prominently?"), "a single thesis or central claim, which the work argues for, shown prominently"),
        LayoutPurpose.THESES => Science(TB("Layout for several theses"), TB("Which slide layout should several theses or central claims use, shown together?"), "several theses or central claims, which the work argues for, shown together"),
        LayoutPurpose.HYPOTHESIS => Science(TB("Layout for one hypothesis"), TB("Which slide layout should a single hypothesis use, shown prominently?"), "a single hypothesis, which the work tests, shown prominently"),
        LayoutPurpose.HYPOTHESES => Science(TB("Layout for several hypotheses"), TB("Which slide layout should several hypotheses use, shown together?"), "several hypotheses, which the work tests, shown together"),
        LayoutPurpose.ARGUMENT_CHAIN => Science(TB("Layout for chains of arguments"), TB("Which slide layout should a chain of arguments use?"), "a chain of arguments"),
        LayoutPurpose.COUNTERARGUMENT => Science(TB("Layout for one counterargument"), TB("Which slide layout should a single counterargument use, shown prominently?"), "a single counterargument, shown prominently"),
        LayoutPurpose.COUNTERARGUMENTS => Science(TB("Layout for several counterarguments"), TB("Which slide layout should several counterarguments use, shown together?"), "several counterarguments, shown together"),
        LayoutPurpose.METHODOLOGY => Science(TB("Layout for the methodology"), TB("Which slide layout should the methodology use?"), "the methodology"),
        LayoutPurpose.STUDY_DESIGN => Science(TB("Layout for the study design"), TB("Which slide layout should the design of a study use?"), "the design of a study"),
        LayoutPurpose.EXPERIMENTAL_SETUP => Science(TB("Layout for the experimental setup"), TB("Which slide layout should the experimental setup use?"), "the experimental setup"),
        LayoutPurpose.DATA => Science(TB("Layout for data"), TB("Which slide layout should a description of the data use, e.g., of a dataset?"), "a description of the data, e.g., of a dataset"),
        LayoutPurpose.RESULT => Science(TB("Layout for one result"), TB("Which slide layout should a single result use, shown prominently?"), "a single result, shown prominently"),
        LayoutPurpose.RESULTS => Science(TB("Layout for several results"), TB("Which slide layout should several results use, shown together?"), "several results, shown together"),
        LayoutPurpose.INTERPRETATION => Science(TB("Layout for interpretations"), TB("Which slide layout should an interpretation of the results use?"), "an interpretation of the results"),
        LayoutPurpose.LIMITATIONS => Science(TB("Layout for limitations"), TB("Which slide layout should the limitations of the work use?"), "the limitations of the work"),
        LayoutPurpose.CONTRIBUTION => Science(TB("Layout for the contribution"), TB("Which slide layout should the contribution of the work use, i.e., what is new?"), "the contribution of the work, i.e., what is new about it"),
        LayoutPurpose.FUNDING => Science(TB("Layout for funding notes"), TB("Which slide layout should a note on the funding of the work use?"), "a note on who funded the work"),

        _ => null,
    };

    private static LayoutPurposeInfo Science(string label, string question, string promptDescription) => new(LayoutPurposeArea.SCIENCE, label, question, promptDescription);
}