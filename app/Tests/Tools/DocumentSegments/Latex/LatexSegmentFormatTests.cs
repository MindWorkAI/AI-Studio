using System.Text;

using AIStudio.Tools.DocumentSegments;
using AIStudio.Tools.DocumentSegments.Latex;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.DocumentSegments.Latex;

/// <summary>
/// Checks which parts of a LaTeX document a model works on, and that everything else stays as it is.
/// </summary>
[TestFixture]
public sealed class LatexSegmentFormatTests
{
    private const string PARAGRAPH = "Read the <1>manual</1> first, see Section\u00A0<2/> and <3/>. <4/>Then run <5/> & check <6/> in 50\u202F% of the cases.";

    //
    // The line endings of this file depend on the checkout, so the document gets the ones of LaTeX files on Linux and macOS:
    //
    private static readonly string DOCUMENT = """
                                              \documentclass{article}
                                              \usepackage[ngerman]{babel}
                                              \title{Mission to Mars}
                                              \author{Ada Lovelace}
                                              \newcommand{\project}{Ares}
                                              \begin{document}
                                              \maketitle

                                              \section{Getting started}\label{sec:start}
                                              Read the \emph{manual} first, see Section~\ref{sec:start}
                                              and \cite[p.~5]{knuth}. % a comment stays
                                              Then run \texttt{make} \& check $x^2$ in 50\,\% of the cases.

                                              \begin{itemize}
                                                \item Install the \textbf{tools}
                                                \item[Note] Open the \href{https://example.org/a%20b}{website}
                                              \end{itemize}

                                              \begin{figure}[htbp]
                                                \centering
                                                \includegraphics[width=\linewidth]{mars.png}
                                                \caption[Short caption]{The red planet}
                                                \label{fig:mars}
                                              \end{figure}

                                              \begin{tabular}{ll}
                                                \hline
                                                Name & Value \\
                                                Speed & 42 \\
                                              \end{tabular}

                                              \begin{verbatim}
                                              verbatim stays
                                              \end{verbatim}

                                              \begin{equation}
                                                E = mc^2 \text{ equation stays}
                                              \end{equation}

                                              M\"uller wrote {\bfseries bold words} on \today.
                                              \end{document}
                                              """.ReplaceLineEndings("\n");

    private readonly LatexSegmentFormat format = new();
    private string testFolder = string.Empty;
    private string source = string.Empty;
    private string target = string.Empty;

    [SetUp]
    public void CreateDocument()
    {
        this.testFolder = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "mindwork-ai-studio-tests", Guid.NewGuid().ToString("N"))).FullName;
        this.source = Path.Join(this.testFolder, "source.tex");
        this.target = Path.Join(this.testFolder, "target.tex");
        this.WriteSource(DOCUMENT);
    }

    [TearDown]
    public void RemoveFolder() => Directory.Delete(this.testFolder, true);

    [Test]
    public async Task TheWordsGoToTheModelAndTheMarkupBecomesTags()
    {
        var keys = await this.KeysAsync();

        Assert.Multiple(() =>
        {
            Assert.That(keys, Does.Contain("Mission to Mars"), "The title in the preamble is read.");
            Assert.That(keys, Does.Contain("Getting started"));
            Assert.That(keys, Does.Contain(PARAGRAPH), "Emphasis is a pair; references, citations, comments, code, and formulas are tags of their own; escaped characters and spaces reach the model as they look.");
            Assert.That(keys, Does.Contain("Install the <1>tools</1>"));
            Assert.That(keys, Does.Contain("Note"), "The label of an item is a segment of its own.");
            Assert.That(keys, Does.Contain("Open the <1>website</1>"), "The model never sees the address of a link.");
            Assert.That(keys, Is.SupersetOf(new[] { "Short caption", "The red planet", "Name", "Value", "Speed" }));
            Assert.That(keys, Does.Contain("Müller wrote <1>bold words</1> on <2/>."), "An accent becomes its letter, and the switch to bold belongs to the markup of its group.");
            Assert.That(keys, Does.Not.Contain("ll"), "The columns of a table stay out.");
            Assert.That(keys, Has.None.Contains("Ada"), "The preamble stays out apart from the title.");
            Assert.That(keys, Has.None.Contains("Ares"), "Definitions of macros stay out.");
            Assert.That(keys, Has.None.Contains("stays"), "Verbatim text, formulas, and comments stay out.");
            Assert.That(keys, Has.None.Contains("htbp"));
            Assert.That(keys, Has.None.Contains("mars.png"));
        });
    }

    [Test]
    public async Task WritingTheSourceBackChangesNothing()
    {
        var result = await this.format.WriteAsync(this.source, this.target, await this.ResultsAsync(key => key), string.Empty, NullLogger.Instance, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(SegmentWriteOutcome.WRITTEN));
            Assert.That(File.ReadAllText(this.target), Is.EqualTo(DOCUMENT));
        });
    }

    [Test]
    public async Task NewWordsKeepTheirMarkup()
    {
        var result = await this.format.WriteAsync(this.source, this.target, await this.ResultsAsync(key => key.ToUpperInvariant()), "de-DE", NullLogger.Instance, CancellationToken.None);
        var written = File.ReadAllText(this.target);

        Assert.Multiple(() =>
        {
            Assert.That(result.SimplifiedCount, Is.Zero);
            Assert.That(written, Does.Contain(@"\usepackage[ngerman]{babel}"), "The language settings stay as they are.");
            Assert.That(written, Does.Contain(@"\title{MISSION TO MARS}"));
            Assert.That(written, Does.Contain(@"\author{Ada Lovelace}"));
            Assert.That(written, Does.Contain(@"\section{GETTING STARTED}\label{sec:start}"));
            Assert.That(written, Does.Contain(@"READ THE \emph{MANUAL} FIRST, SEE SECTION~\ref{sec:start} AND \cite[p.~5]{knuth}.  % a comment stays" + "\n" + @"THEN RUN \texttt{make} \& CHECK $x^2$ IN 50\,\% OF THE CASES."), "The comment keeps its line, after a space of its own, and the escaped characters are escaped again.");
            Assert.That(written, Does.Contain(@"\item INSTALL THE \textbf{TOOLS}"));
            Assert.That(written, Does.Contain(@"\item[NOTE] OPEN THE \href{https://example.org/a%20b}{WEBSITE}"));
            Assert.That(written, Does.Contain(@"\includegraphics[width=\linewidth]{mars.png}"));
            Assert.That(written, Does.Contain(@"\caption[SHORT CAPTION]{THE RED PLANET}"));
            Assert.That(written, Does.Contain(@"\begin{tabular}{ll}"));
            Assert.That(written, Does.Contain(@"NAME & VALUE \\"));
            Assert.That(written, Does.Contain(@"SPEED & 42 \\"));
            Assert.That(written, Does.Contain("verbatim stays"));
            Assert.That(written, Does.Contain(@"\text{ equation stays}"));
            Assert.That(written, Does.Contain(@"MÜLLER WROTE {\bfseries BOLD WORDS} ON \today{}."), "A command before new words gets an empty group, which keeps them apart.");
        });
    }

    [Test]
    public async Task NewWordsCannotBreakTheMarkup()
    {
        var results = await this.ResultsAsync(key => key switch
        {
            "Getting started" => @"Kosten: 50 % & $5 für #1_a {x} ^ ~ \ fertig",
            "Short caption" => "Kurz [mit] Klammern",
            "Name" => "[Name]",
            "Install the <1>tools</1>" => "Installiere\n\ndie <1>Werkzeuge</1>",
            "Müller wrote <1>bold words</1> on <2/>." => "<2/>schrieb Müller <1>fette Wörter</1>.",

            _ => key,
        });

        var result = await this.format.WriteAsync(this.source, this.target, results, string.Empty, NullLogger.Instance, CancellationToken.None);
        var written = File.ReadAllText(this.target);

        Assert.Multiple(() =>
        {
            Assert.That(result.SimplifiedCount, Is.Zero);
            Assert.That(written, Does.Contain(@"\section{Kosten: 50 \% \& \$5 für \#1\_a \{x\} \^{} \textasciitilde{} \textbackslash{} fertig}"));
            Assert.That(written, Does.Contain(@"\caption[Kurz [mit{]} Klammern]{The red planet}"), "A closing bracket would end the optional argument.");
            Assert.That(written, Does.Contain(@"{[}Name] & Value \\"), "A bracket at the start of a cell would be taken for an optional argument of the line break before it.");
            Assert.That(written, Does.Contain(@"\item Installiere  die \textbf{Werkzeuge}"), "A blank line would end the item.");
            Assert.That(written, Does.Contain(@"\today{}schrieb Müller {\bfseries fette Wörter}."), "Letters right after a command would become part of its name.");
        });
    }

    [Test]
    public async Task EscapedCharactersReachTheModelAsTheyLook()
    {
        this.WriteSource(@"\glqq Stra\ss e\grqq{} im Caf\'e, z.\,B. 5\,\%.");

        Assert.That(await this.KeysAsync(), Is.EqualTo(new[] { "„Straße“ im Café, z.\u202FB. 5\u202F%." }));
    }

    [Test]
    public async Task APartOfADocumentIsReadAsAWhole()
    {
        this.WriteSource("Some \\textit{text}.\r\n\r\nA stray } brace stays.\r\n");

        Assert.That(await this.KeysAsync(), Is.EqualTo(new[] { "Some <1>text</1>." }), "Without a preamble, everything is the body; a paragraph whose markup the reader cannot follow stays as it is.");
    }

    [Test]
    public async Task SlidesOfBeamerKeepTheirTitlesAndOverlays()
    {
        this.WriteSource("""
                         \begin{frame}{Results}
                           \begin{itemize}
                             \item<2-> First \alert<3>{point}
                           \end{itemize}
                         \end{frame}
                         """.ReplaceLineEndings("\n"));

        var keys = await this.KeysAsync();
        await this.format.WriteAsync(this.source, this.target, await this.ResultsAsync(key => key.ToUpperInvariant()), string.Empty, NullLogger.Instance, CancellationToken.None);
        var written = File.ReadAllText(this.target);

        Assert.Multiple(() =>
        {
            Assert.That(keys, Is.EqualTo(new[] { "Results", "First <1>point</1>" }));
            Assert.That(written, Does.Contain(@"\begin{frame}{RESULTS}"));
            Assert.That(written, Does.Contain(@"\item<2-> FIRST \alert<3>{POINT}"));
        });
    }

    [Test]
    public async Task AChangedSourceWritesNothing()
    {
        var result = await this.format.WriteAsync(this.source, this.target, new SegmentResults(new Dictionary<string, SegmentOutcome>()), "de-DE", NullLogger.Instance, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(SegmentWriteOutcome.SOURCE_CHANGED));
            Assert.That(File.Exists(this.target), Is.False);
        });
    }

    private void WriteSource(string text) => File.WriteAllText(this.source, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    private async Task<List<string>> KeysAsync() => (await this.format.ReadAsync(this.source, CancellationToken.None)).Select(segment => segment.Key).ToList();

    private async Task<SegmentResults> ResultsAsync(Func<string, string> transform)
    {
        var segments = await this.format.ReadAsync(this.source, CancellationToken.None);
        return new SegmentResults(segments
            .Where(segment => segment.HasWords)
            .DistinctBy(segment => segment.Key)
            .ToDictionary(segment => segment.Key, segment => SegmentOutcome.Transformed(transform(segment.Key))));
    }
}