# AGENTS.md

These instructions cover the Word tools in `app/MindWork AI Studio/Tools/Word/`, with which AI Studio
exports an answer into a Word template of the user. They add to the `AGENTS.md` in the repository root and
to `app/AGENTS.md`, which apply here as well. Without a template, Pandoc writes the document instead; that
path does not run through here.

## How a document comes about

1. `WordDocumentFile.CreateAsync` copies the template into the temporary folder with `TemplateCopy` from
   `app/MindWork AI Studio/Tools/OpenXml/`, and streams the result into the target once it is complete.
2. `WordDocumentWriter.Write` opens the copy with the Open XML SDK (`DocumentFormat.OpenXml`), turns a
   template (.dotx) into a document, and replaces the body: everything but the properties of the last
   section goes, together with whatever only the body used. Before, `MergeSections` hands the last
   section what the document keeps of the sections before it.
3. `MarkdownWordConverter` reads the Markdown with Markdig, through `Markdown.SAFE_MARKDOWN_PIPELINE`,
   and turns its blocks into paragraphs and tables. `WordStyles` finds the styles they use in the
   template, and `WordNumbering` adds the numbering of the lists.

Some things Markdown knows have no counterpart which a template could style: math stays in the notation
of TeX, a picture is replaced by its description, and the footnotes follow the text as a list, with their
numbers raised, rather than going to the foot of the page.

## Rules which are easy to break

- **No knowledge of a particular template.** Everything comes from the file. Never add the names,
  styles, or language of a corporate template to the code.
- **Styles are found by name, never by ID.** Word stores the built-in styles under their English name,
  e.g., "heading 1", in every language, while the ID follows the language of the Word which saved the
  file: a German Word stores the first heading as "berschrift1". `WordStyles` looks up the name and adds
  a plain definition when the template lacks a style; a heading level it lacks looks like the nearest
  one above it which the template defines.
- **The top heading level of the answer becomes the first one of the template.** Models start with
  `##`, and templates often style or number their chapters from the first level on. The level is read
  from the answer alone (`MarkdownWordConverter.TopHeadingLevelOf`), because the sources AI Studio
  adds below it always start with `#`.
- **New content carries no formatting of its own.** Paragraphs, lists, and tables take their look from
  the styles of the template; a table takes the style the template names for new tables. Only what
  Markdown itself marks, such as bold text or the alignment of a column, is formatted directly.
- **Lists take the numbering of the template.** "List Bullet", "List Number", or "List Paragraph" of a
  template often bring the bullets or numbers of the organization along. `MarkdownWordConverter.ListLookOf`
  picks the first one whose numbering is of the kind of the list; only without one does a list get a
  numbering of its own. The further paragraphs of an item switch the numbering of their style off.
- **Every numbered list counts on its own.** Word goes on counting across all lists which share a
  numbering instance, so `WordNumbering` gives each numbered list an instance of its own, which restarts
  the count, also when it takes the numbering definition of the template.
- **The blocks are spaced the way the template spaces its own text.** When the default paragraph style
  leaves no space after a paragraph (`WordStyles.LeavesNoSpaceAfterParagraphs`), the template separates
  its paragraphs with empty ones, and `MarkdownWordConverter.Convert` does the same between the blocks
  of the answer. A heading brings its own space along.
- **A body and a table cell end with a paragraph,** and two tables never follow each other directly,
  or Word joins them. `MarkdownWordConverter.EndWithParagraph` and the case for tables see to it.
- **No copy of a file in memory.** Templates of organizations carry pictures and easily reach a hundred
  megabytes. Let the writer edit a copy on disk; a `MemoryStream` with the whole file multiplies what
  the Open XML SDK holds anyway.
- **Write only into the file the user chose.** Inside the Flatpak, the save dialog grants access to
  exactly that file. `TemplateCopy` keeps this rule; use it rather than a `FileStream` of your own.
- **The last section inherits what the removed sections state.** Corporate templates state their headers
  and footers once, in the first section, e.g., a cover page, and the sections after it inherit them, as
  Word lets a section without a header or footer of a kind do. `WordDocumentWriter.MergeSections` hands
  them to the last section before the others go. The first page of the document is the first page of
  the template: its own header and footer come from the first section, and an empty paragraph makes up
  for a larger top margin there, so that the text starts below a picture in that header.
- **Removing the body removes what only it used:** pictures, charts, embedded objects, the headers of
  removed sections, the targets of links, comments, and notes, see
  `WordDocumentWriter.RemoveWhatOnlyTheBodyUsed`. Otherwise Word offers to repair the file, or the
  document carries hidden remains of the template.

## Tests

The tests in `app/Tests/Tools/Word/` build their templates in code with `TestWordTemplate`, so no binary
template is checked in. A test which writes a document also runs the `OpenXmlValidator` over it, see
`ValidationErrorsOf`.