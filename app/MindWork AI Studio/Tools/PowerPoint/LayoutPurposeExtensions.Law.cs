namespace AIStudio.Tools.PowerPoint;

internal static partial class LayoutPurposeExtensions
{
    private static LayoutPurposeInfo? DescribeLaw(LayoutPurpose purpose) => purpose switch
    {
        LayoutPurpose.FACTS_OF_THE_CASE => Law(TB("Layout for the facts of the case"), TB("Which slide layout should the facts of a legal case use?"), "the facts of a legal case"),
        LayoutPurpose.LEGAL_QUESTION => Law(TB("Layout for one legal question"), TB("Which slide layout should a single legal question use, shown prominently?"), "a single legal question, shown prominently"),
        LayoutPurpose.LEGAL_QUESTIONS => Law(TB("Layout for several legal questions"), TB("Which slide layout should several legal questions use, shown together?"), "several legal questions, shown together"),
        LayoutPurpose.LEGAL_PROVISION => Law(TB("Layout for one legal provision"), TB("Which slide layout should a single legal provision use, e.g., a quoted section of a law?"), "a single legal provision, e.g., a quoted section of a law, shown prominently"),
        LayoutPurpose.LEGAL_PROVISIONS => Law(TB("Layout for several legal provisions"), TB("Which slide layout should several legal provisions use, shown together?"), "several legal provisions, shown together"),
        LayoutPurpose.EXAMINATION_SCHEME => Law(TB("Layout for examination schemes"), TB("Which slide layout should a scheme use by which a legal question is examined step by step?"), "a scheme by which a legal question is examined step by step"),
        LayoutPurpose.DISPUTE_OF_OPINIONS => Law(TB("Layout for disputes of opinions"), TB("Which slide layout should a dispute of legal opinions use, e.g., the prevailing and a minority opinion?"), "a dispute of legal opinions, e.g., the prevailing and a minority opinion"),
        LayoutPurpose.POSITIONS_OF_THE_PARTIES => Law(TB("Layout for the positions of the parties"), TB("Which slide layout should the positions of the parties use, e.g., of plaintiff and defendant?"), "the positions of the parties, e.g., of plaintiff and defendant"),
        LayoutPurpose.PIECE_OF_EVIDENCE => Law(TB("Layout for one piece of evidence"), TB("Which slide layout should a single piece of evidence use, shown prominently?"), "a single piece of evidence in a legal case, shown prominently"),
        LayoutPurpose.PIECES_OF_EVIDENCE => Law(TB("Layout for several pieces of evidence"), TB("Which slide layout should several pieces of evidence use, shown together?"), "several pieces of evidence in a legal case, shown together"),
        LayoutPurpose.RULING => Law(TB("Layout for one ruling"), TB("Which slide layout should a single court ruling use, e.g., with court, date, case number, and headnote?"), "a single court ruling, shown prominently, e.g., with court, date, case number, and headnote"),
        LayoutPurpose.RULINGS => Law(TB("Layout for several rulings"), TB("Which slide layout should several court rulings use, shown together?"), "several court rulings, shown together"),
        LayoutPurpose.LEGAL_COMMENTARY => Law(TB("Layout for one commentary"), TB("Which slide layout should a single passage of a legal commentary use, e.g., of an annotated code, shown prominently?"), "a single passage of a legal commentary, e.g., of an annotated code or statute, or of other legal literature, shown prominently with its citation"),
        LayoutPurpose.LEGAL_COMMENTARIES => Law(TB("Layout for several commentaries"), TB("Which slide layout should several passages of legal commentaries or other legal literature use, shown together?"), "several passages of legal commentaries, e.g., of annotated codes or statutes, or of other legal literature, shown together with their citations"),
        LayoutPurpose.CONTRACT_CLAUSE => Law(TB("Layout for one contract clause"), TB("Which slide layout should a single contract clause use, shown prominently?"), "a single contract clause, shown prominently"),
        LayoutPurpose.CONTRACT_CLAUSES => Law(TB("Layout for several contract clauses"), TB("Which slide layout should several contract clauses use, shown together?"), "several contract clauses, shown together"),
        LayoutPurpose.LEGAL_CONSEQUENCES => Law(TB("Layout for legal consequences"), TB("Which slide layout should legal consequences use?"), "the legal consequences"),
        LayoutPurpose.DEADLINES => Law(TB("Layout for deadlines"), TB("Which slide layout should deadlines use, e.g., for an appeal?"), "deadlines, e.g., for an appeal"),

        _ => null,
    };

    private static LayoutPurposeInfo Law(string label, string question, string promptDescription) => new(LayoutPurposeArea.LAW, label, question, promptDescription);
}