using System.Collections.Frozen;

namespace AIStudio.Tools.DocumentSegments.Latex;

/// <summary>
/// The commands and environments of LaTeX and its common packages, as far as the segments need to know them.
/// </summary>
/// <remarks>
/// Commands with letters which stand for words, e.g., \ss or \glqq, are known by LatexWords instead. Whatever is
/// missing here stays as it is: an unknown command becomes a tag of its own with its arguments, an unknown
/// environment holds paragraphs.
/// </remarks>
internal static class LatexCommands
{
    private static readonly FrozenDictionary<string, LatexCommand> COMMANDS = CreateCommands();

    private static readonly FrozenDictionary<string, LatexEnvironmentRole> ENVIRONMENTS = CreateEnvironments();

    /// <summary>
    /// Parts of the names of environments which hold code or drawings, e.g., of lstlisting, pycode, or tikzcd.
    /// </summary>
    private static readonly string[] VERBATIM_NAME_PARTS = ["verbatim", "listing", "code", "minted", "tikz"];

    /// <summary>
    /// Finds what the reader knows about a command.
    /// </summary>
    /// <param name="name">The name of the command without its backslash and star, e.g., "section" or "\" for \\.</param>
    public static LatexCommand Of(string name) => COMMANDS.GetValueOrDefault(name, LatexCommand.UNKNOWN);

    /// <summary>
    /// Finds what an environment means for the segments.
    /// </summary>
    /// <param name="name">The name of the environment, e.g., "itemize" or "align*".</param>
    public static LatexEnvironmentRole EnvironmentOf(string name)
    {
        var baseName = name.TrimEnd('*');
        if (ENVIRONMENTS.TryGetValue(baseName, out var role))
            return role;

        return VERBATIM_NAME_PARTS.Any(part => baseName.Contains(part, StringComparison.OrdinalIgnoreCase))
            ? LatexEnvironmentRole.VERBATIM
            : LatexEnvironmentRole.CONTAINER;
    }

    private static FrozenDictionary<string, LatexCommand> CreateCommands()
    {
        var commands = new Dictionary<string, LatexCommand>(StringComparer.Ordinal);
        void Add(LatexCommand command, params string[] names)
        {
            foreach (var name in names)
                commands[name] = command;
        }

        //
        // Words in another look, with a link, or in a note, which become a pair:
        //
        Add(new(LatexCommandRole.WORDS, 1, false),
            "textbf", "textit", "textsl", "textsc", "textup", "textmd", "textrm", "textsf", "textnormal", "emph",
            "underline", "uline", "uuline", "uwave", "sout", "xout", "hl", "ul", "textsuperscript", "textsubscript",
            "enquote", "mbox", "fbox", "thanks", "alert", "structure");
        Add(new(LatexCommandRole.WORDS, 1, true), "footnote", "footnotetext", "marginpar", "makebox", "framebox", "hyperref");
        Add(new(LatexCommandRole.WORDS, 2, true, 1), "textcolor", "colorbox", "parbox", "raisebox");
        Add(new(LatexCommandRole.WORDS, 3, true, 2), "fcolorbox", "multirow");
        Add(new(LatexCommandRole.WORDS, 3, false, 2), "multicolumn");
        Add(new(LatexCommandRole.WORDS, 2, false, 1), "hyperlink", "hypertarget");
        Add(new(LatexCommandRole.WORDS, 2, false, 1) { HasRawFirstArgument = true }, "href");

        //
        // Headings and captions, which are segments of their own:
        //
        Add(new(LatexCommandRole.HEADING, 1, true),
            "part", "chapter", "section", "subsection", "subsubsection", "paragraph", "subparagraph", "addpart",
            "addchap", "addsec", "minisec", "caption", "subcaption", "title", "subtitle", "frametitle", "framesubtitle");
        Add(new(LatexCommandRole.HEADING, 2, true, 1), "captionof");

        Add(new(LatexCommandRole.ITEM, 0, true), "item");
        Add(new(LatexCommandRole.PARAGRAPH, 0, false), "par");
        Add(new(LatexCommandRole.LINE_BREAK, 0, true), "\\", "tabularnewline");

        //
        // Layout without words:
        //
        Add(new(LatexCommandRole.LAYOUT, 0, false),
            "noindent", "indent", "centering", "raggedright", "raggedleft", "justifying", "vfill", "hfill", "medskip",
            "smallskip", "bigskip", "newpage", "clearpage", "cleardoublepage", "maketitle", "tableofcontents",
            "listoffigures", "listoftables", "appendix", "frontmatter", "mainmatter", "backmatter", "hline",
            "phantomsection", "FloatBarrier", "small", "footnotesize", "scriptsize", "tiny", "large", "Large", "LARGE",
            "huge", "Huge", "normalsize", "bfseries", "itshape", "mdseries", "upshape", "slshape", "scshape",
            "rmfamily", "sffamily", "ttfamily", "normalfont", "em", "bf", "it", "rm", "sf", "tt", "sc", "sl",
            "selectfont", "relax", "null", "pause", "makeatletter", "makeatother", "boldmath", "unboldmath", "sloppy",
            "fussy", "onecolumn", "newline", "def", "gdef", "edef", "xdef");
        Add(new(LatexCommandRole.LAYOUT, 0, true),
            "pagebreak", "nopagebreak", "linebreak", "printbibliography", "toprule", "midrule", "bottomrule",
            "addlinespace", "twocolumn");
        Add(new(LatexCommandRole.LAYOUT, 1, false),
            "label", "vspace", "hspace", "stepcounter", "refstepcounter", "thispagestyle", "pagestyle", "input",
            "include", "bibliography", "bibliographystyle", "cline", "pagenumbering", "markright", "hypersetup",
            "graphicspath", "newlength", "selectlanguage", "usetheme", "usecolortheme", "usefonttheme", "useinnertheme",
            "useoutertheme", "arrayrulecolor", "addbibresource");
        Add(new(LatexCommandRole.LAYOUT, 1, true),
            "includegraphics", "rowcolor", "cellcolor", "columncolor", "usepackage", "RequirePackage", "documentclass",
            "captionsetup", "newcounter", "lstinputlisting", "inputminted");
        Add(new(LatexCommandRole.LAYOUT, 1, true) { TakesParenthesizedArgument = true }, "cmidrule");
        Add(new(LatexCommandRole.LAYOUT, 2, false),
            "setlength", "addtolength", "setcounter", "addtocounter", "markboth", "setkomafont", "addtokomafont");
        Add(new(LatexCommandRole.LAYOUT, 2, true), "newcommand", "renewcommand", "providecommand", "DeclareMathOperator", "newtheorem");
        Add(new(LatexCommandRole.LAYOUT, 3, false), "addcontentsline", "definecolor");
        Add(new(LatexCommandRole.LAYOUT, 3, true), "newenvironment", "renewenvironment");

        //
        // Things a reader sees, which a model keeps as they are:
        //
        Add(new(LatexCommandRole.INLINE, 1, true),
            "ref", "eqref", "pageref", "autoref", "cref", "Cref", "nameref", "vref", "cite", "citep", "citet",
            "citealp", "citealt", "citeauthor", "citeyear", "parencite", "textcite", "autocite", "footcite",
            "fullcite", "smartcite", "nocite", "gls", "Gls", "glspl", "Glspl", "acrshort", "acrlong", "acrfull", "ac",
            "Ac", "acp", "Acp", "acs", "acl", "acf", "index", "todo", "si", "num", "unit", "ang", "footnotemark");
        Add(new(LatexCommandRole.INLINE, 1, false), "texttt");
        Add(new(LatexCommandRole.INLINE, 2, true), "SI", "qty", "foreignlanguage", "crefrange");
        Add(new(LatexCommandRole.INLINE, 1, false) { HasRawFirstArgument = true }, "url", "path", "nolinkurl");
        Add(new(LatexCommandRole.INLINE, 0, false),
            "today", "LaTeX", "TeX", "LaTeXe", "xspace", "slash", "enspace", "quad", "qquad", "thinspace",
            "nobreakspace", "dag", "ddag", "P", "copyright", "textregistered", "texttrademark", "pounds", "euro",
            "texteuro", "textbackslash", "textasciitilde", "textasciicircum", "textbar", "textless", "textgreater",
            "textdegree", "textperthousand", "protect", "thepage");

        return commands.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static FrozenDictionary<string, LatexEnvironmentRole> CreateEnvironments()
    {
        var environments = new Dictionary<string, LatexEnvironmentRole>(StringComparer.Ordinal);
        void Add(LatexEnvironmentRole role, params string[] names)
        {
            foreach (var name in names)
                environments[name] = role;
        }

        Add(LatexEnvironmentRole.VERBATIM,
            "comment", "filecontents", "pgfpicture", "circuitikz", "forest", "picture", "asy", "thebibliography",
            "tabbing", "alltt", "python", "sagesilent", "sageblock", "dot2tex", "gnuplot");
        Add(LatexEnvironmentRole.MATH,
            "equation", "align", "alignat", "flalign", "gather", "multline", "eqnarray", "displaymath", "math", "dmath",
            "dgroup", "dseries", "xalignat", "xxalignat");
        Add(LatexEnvironmentRole.TABLE,
            "tabular", "tabularx", "tabulary", "longtable", "supertabular", "xtabular", "mpsupertabular", "tabu",
            "longtabu", "tblr", "longtblr", "talltblr", "NiceTabular", "NiceTabularX");
        Add(LatexEnvironmentRole.CONTAINER_WITH_TITLE, "frame", "block", "alertblock", "exampleblock");
        Add(LatexEnvironmentRole.CONTAINER_WITH_NAME,
            "theorem", "lemma", "corollary", "proposition", "definition", "example", "remark", "proof", "note",
            "exercise", "problem", "solution", "claim", "conjecture", "assumption", "hypothesis", "observation", "fact",
            "notation", "axiom", "satz", "beweis", "beispiel", "bemerkung", "folgerung");

        return environments.ToFrozenDictionary(StringComparer.Ordinal);
    }
}