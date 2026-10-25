namespace AIStudio.Tools.PowerPoint;

internal static partial class LayoutPurposeExtensions
{
    private static LayoutPurposeInfo? DescribeFraming(LayoutPurpose purpose) => purpose switch
    {
        LayoutPurpose.TITLE => Framing(TB("Layout for the title slide"), TB("Which slide layout should the title slide use?"), "the title slide, which is the first slide"),
        LayoutPurpose.TABLE_OF_CONTENTS => Framing(TB("Layout for the table of contents"), TB("Which slide layout should the table of contents use, which lists the topics of the presentation?"), "the table of contents, which lists the topics or chapters of the presentation without times"),
        LayoutPurpose.AGENDA => Framing(TB("Layout for the agenda"), TB("Which slide layout should an agenda with times and rooms use, e.g., of a workshop?"), "an agenda with times, and with rooms where the content names them, e.g., of a workshop; only where the content gives such a schedule, never with times of your own"),
        LayoutPurpose.SPEAKER => Framing(TB("Layout for introducing one speaker"), TB("Which slide layout should the introduction of a single speaker use?"), "a slide which introduces a single speaker"),
        LayoutPurpose.SPEAKERS => Framing(TB("Layout for introducing several speakers"), TB("Which slide layout should the introduction of several speakers use?"), "a slide which introduces several speakers together"),
        LayoutPurpose.CHAPTER => Framing(TB("Layout for chapters"), TB("Which slide layout should open each chapter?"), "each slide which starts a chapter; it opens the chapter, and the content of the chapter follows on the next slides"),
        LayoutPurpose.CHAPTER_SUMMARY => Framing(TB("Layout for interim summaries"), TB("Which slide layout should an interim summary at the end of a chapter use?"), "an interim summary at the end of a chapter"),
        LayoutPurpose.Q_AND_A => Framing(TB("Layout for “Any questions?”"), TB("Which slide layout should a slide use which invites the questions of the audience?"), "a slide which invites the questions of the audience"),
        LayoutPurpose.ACKNOWLEDGEMENTS => Framing(TB("Layout for acknowledgements"), TB("Which slide layout should the acknowledgements use?"), "acknowledgements, which thank the people or organizations who contributed"),
        LayoutPurpose.REFERENCES => Framing(TB("Layout for sources and literature"), TB("Which slide layout should the sources and the literature use?"), "the sources and the literature"),
        LayoutPurpose.CLOSING => Framing(TB("Layout for the closing slide"), TB("Which slide layout should the closing slide use, e.g., with thanks and contact details?"), "the closing slide, e.g., with thanks and contact details"),
        LayoutPurpose.APPENDIX => Framing(TB("Layout for the appendix"), TB("Which slide layout should open an appendix with backup slides?"), "the slide which opens an appendix with backup slides after the closing slide"),
        LayoutPurpose.IMPRINT => Framing(TB("Layout for the imprint"), TB("Which slide layout should the imprint use, e.g., with authors, organization, and date?"), "the imprint, e.g., with the authors, the organization, and the date"),

        _ => null,
    };

    private static LayoutPurposeInfo Framing(string label, string question, string promptDescription) => new(LayoutPurposeArea.FRAMING, label, question, promptDescription);
}