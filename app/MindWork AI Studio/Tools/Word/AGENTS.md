# AGENTS.md

These instructions cover the Word tools in `app/MindWork AI Studio/Tools/Word/`, with which AI Studio
exports an answer into a Word template of the user. They add to the `AGENTS.md` in the repository root and
to `app/AGENTS.md`, which apply here as well. Without a template, Pandoc writes the document instead; that
path does not run through here.

## How a document comes about

1. `WordDocumentFile.CreateAsync` copies the template into the temporary folder with `TemplateCopy` from
   `app/MindWork AI Studio/Tools/OpenXml/`, and streams the result into the target once it is complete.
2. `WordDocumentWriter.Write` opens the copy with the Open XML SDK (`DocumentFormat.OpenXml`), turns a
   template (.dotx) into a document, and replaces the body: everything goes but a cover page with what
   follows it up to the text, and the properties of the last section, together with whatever only the
   body used. Before, `WordTemplatePages` reads what the pages keep, and `MergeSections` hands the last
   section the layout of the main section and what the document keeps of the sections before it.
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
  `##`, and templates often style or number their chapters from the first level on. The levels are
  read from the answer alone (`MarkdownWordConverter.HeadingLevelsOf`), because the sources AI Studio
  adds below it always start with `#`.
- **A title box of the template takes the heading the answer starts with.** A text box on the first
  page with a paragraph in the style "Title", found by its name, is the title box (`WordShapes.TitleBoxOf`).
  Its first title paragraph takes the text of that heading, with the look of its first run, e.g.,
  white letters; further title paragraphs go, and subtitles stay empty. The heading goes from the
  text then, and the top level of the headings after it becomes the first one of the template
  (`WordHeadingLevels.TopLevelBelowTitle`). Without a heading at the start, the box keeps its sample text.
- **New content carries no formatting of its own.** Paragraphs, lists, and tables take their look from
  the styles of the template; a table takes the style the template names for new tables. Only what
  Markdown itself marks, such as bold text or the alignment of a column, is formatted directly.
- **Lists take the numbering of the template.** "List Bullet", "List Number", or "List Paragraph" of a
  template often bring the bullets or numbers of the organization along. `MarkdownWordConverter.ListLookOf`
  picks the first one whose numbering is of the kind of the list; only without one does a list get a
  numbering of its own. The further paragraphs of an item switch the numbering of their style off.
- **List items keep the indentation of their style.** Word lets the indentation of a style win over
  the one of the numbering the style brings along, but not when the paragraph names the numbering
  itself, as every item of the answer does. So the items of the first level state the indentation
  of their style directly; otherwise they move in as far as the numbering says, which often differs.
  This only restores the look of the template, unlike formatting of their own.
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
- **The pages of the answer take the layout of the main section.** The main section holds the text of
  the template up to its last heading after the first page (`WordTemplatePages.MainSection`), i.e.,
  the layout of its chapters. Sections after it go with the sample text, e.g., pages of instructions
  with headers of their own, which would otherwise show on every page. The last section becomes a
  copy of the main one. When a cover page and what follows it share a section with the first
  chapter, often with narrower margins than the sections of the chapters after it, they end with a
  copy of that section, and the answer starts on a new page.
- **The last section inherits what the removed sections state.** Corporate templates state their headers
  and footers once, in the first section, e.g., a cover page, and the sections after it inherit them, as
  Word lets a section without a header or footer of a kind do. `WordDocumentWriter.MergeSections` hands
  them to the last section before the others go. The first page of the document is the first page of
  the template: its own header and footer come from the first section, and an empty paragraph makes up
  for a larger top margin there, so that the text starts below a picture in that header.
- **A cover page stays, with what follows it up to the text.** A first page without any text of its
  own is a cover page, e.g., the one of a report with its number, title, and authors as placeholders.
  It stays as it is, and so does what the template places between it and its first heading, e.g., a
  table of document properties and a table of contents, with their sections (`WordTemplatePages.FrontMatter`).
  The answer starts after them. A table of contents keeps its field, but not the entries for the
  sample chapters, which would show errors for their page numbers. Word updates the fields of the
  document when it opens it, after asking, so that the table lists the headings of the answer.
- **The text of the first page starts where the one of the template does.** Templates also move it
  down with empty paragraphs, e.g., below a picture and a title. `WordTemplatePages` keeps the empty
  paragraphs before the first text of the first page, without their content. The first page ends
  with the first page break the file states; where Word breaks a page because its text fills it, the
  file marks this only as far as Word laid the text out when it saved the file.
- **A first page with columns of its own stays a section of its own.** Some templates run the text
  of their first page in other columns than on the pages after it, e.g., next to a wide margin for
  pictures, which is on the left on the first page and on the right on the others.
  `WordDocumentWriter.FirstPageSectionOf` keeps a copy of the first section then, and `EndFirstPage`
  ends it where `WordLayoutEstimate` finds the page well filled. Only Word knows where it breaks a
  page, so the estimate errs on the safe side: it counts the characters with the width of a common
  font, but fills only part of the page, for fonts with wider characters. A first page with room to
  spare looks better than one which overflows into a second page in its layout. A heading never ends
  the page, and when everything fits, the document has only the section of the first page.
- **Shapes of the design stay, shapes of the sample text go.** Templates float pictures, text boxes,
  and lines around their text, e.g., pictures with captions in a wide margin, a box for the title
  above the text, or a picture behind the whole page. `WordShapes` tells them from the sample content
  by where they lie, never by what they show: a shape outside the area in which the text of its page
  runs, or one behind the text which the text does not flow around, belongs to the design. The first
  page keeps those of the first page of the template, while a cover page stays as a whole anyway; a
  next page of its own keeps those of the first page of the main section. Their paragraphs go with the body,
  so the shapes move to the first paragraph of their page, with positions in relation to the page.
  A position in relation to a paragraph is only as exact as `WordLayoutEstimate` finds the paragraph.
  The shapes keep their markup for Word 2010 and later, without the alternative for older versions.
  Shapes only in the older markup (VML), frames, and embedded objects are not taken over.
- **The text stays clear of the shapes of the footers.** Word keeps the text away from the text of a
  footer, but not from the shapes floating in it, e.g., an address in a text box. Templates keep
  their sample text short enough to end above them; an answer runs on. So
  `WordDocumentWriter.KeepTextClearOfFooters` moves the bottom margin of each section up as far as
  such a shape reaches across the text, see `WordShapes.FootOf`. Only the footers the pages of the
  section show count, and only shapes in the lower half of the page which lie across the text: a
  logo in a wide margin beside the text is in nobody's way.
- **No header or footer belongs to two sections.** Word refuses such a document as damaged, although
  the validator of the Open XML SDK lets it pass. When the first page stays a section of its own, the
  last section inherits its headers and footers, as Word lets it, rather than referring to them as well.
- **Removing the body removes what only it used:** pictures, charts, embedded objects, the headers of
  removed sections, the targets of links, comments, and notes, see
  `WordDocumentWriter.RemoveWhatOnlyTheBodyUsed`. Otherwise Word offers to repair the file, or the
  document carries hidden remains of the template. It runs last, once the content is in place, so
  that whatever the document still refers to stays, e.g., the header of a first page of its own.

## Tests

The tests in `app/Tests/Tools/Word/` build their templates in code with `TestWordTemplate`, so no binary
template is checked in. A test which writes a document also runs the `OpenXmlValidator` over it, see
`ValidationErrorsOf`.