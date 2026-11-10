# AGENTS.md

These instructions cover the document segments in `app/MindWork AI Studio/Tools/DocumentSegments/`, with
which an assistant lets a model work on the text of a whole document, e.g., translate it, and writes a new
document of the same type in which everything but the text stays as it was. They add to the `AGENTS.md` in
the repository root and to `app/AGENTS.md`, which apply here as well.

## How a document comes about

1. An `IDocumentSegmentFormat` reads the document into `DocumentSegment`s, in the order of the document:
   paragraphs, headings, table cells, or texts of attributes. Each one holds a `TaggedText`, the words with
   numbered tags for the inline structure: a pair such as `<1>…</1>` for something with words, e.g., a link
   or a bold run, and a tag of its own such as `<2/>` for something without, e.g., a tab or a picture. Only
   the format knows what a number stands for.
2. `SegmentBatchPlanner` groups the segments with letters into batches, each text once.
3. The assistant passes the texts through `PromptInjectionGuardService`, within one scope for the whole run.
   A segment which the filter changed never reaches a model and stays as it is (`SegmentOutcome.FILTERED`).
4. `SegmentTransformer` sends each batch as JSON (`SegmentBatch`, written and read by `SegmentJson`) with the
   instructions of the assistant, checks the answer, and asks once more for what is missing or whose tags do
   not fit. An answer which breaks off splits its batch.
5. `SegmentResults` keeps what became of each segment, by its text. The format reads the source again, writes
   nothing when the results miss one of its segments (`SegmentWriteOutcome.SOURCE_CHANGED`), and otherwise
   writes the new texts into a copy of the source, which goes into the file the user chose.

## Rules which are easy to break

- **Nothing here knows what the model does.** The instructions and the system prompt come from the
  assistant, so the same classes serve a translation as well as a correction. Keep words such as
  "translate" out of this folder, apart from examples.
- **Tags are the only structure a model sees.** Never put markup of a format, such as XML or Markdown
  syntax which the format must keep, into a segment as words: make it a tag of its own. Words of a
  document which read as a tag (`TaggedText.ContainsTagLikeText`) would be taken for one, so a format
  leaves such a segment as it is, or sends it without tags when it has no markup to guard.
- **A pair stays within its pair, a tag of its own may move.** `TaggedText.HasSameTagsAs` checks exactly
  this, so a format can nest its parts again as before. Whatever must keep its order, such as the start
  and the end of a field, belongs into one pair.
- **Words beat structure.** A segment whose tags still do not fit after the second request keeps the words
  of the model: the format writes them without the inline structure and counts the segment as simplified.
  Only a segment without any answer keeps its original text.
- **Prompt texts stay in English, the user's texts do not.** Requests and issues are written for the model.
  The texts of a document are data: they go into the request, never into the system prompt.
- **Write only into the file the user chose.** The rules of `app/MindWork AI Studio/Tools/PowerPoint/AGENTS.md`
  hold for every format of Office documents: no copy of the whole file in memory, and `TemplateCopy` from
  `app/MindWork AI Studio/Tools/OpenXml/` for the copy in the temporary folder.

## Word documents

`Word/WordSegmentFormat` reads the paragraphs of the body, the tables, the text boxes, the headers and
footers, and the notes as segments, and leaves everything else of the document alone.

- **Move, never copy.** The writer moves the elements a tag stands for into the new paragraph. A text box
  holds paragraphs of its own, which are segments as well; only moving keeps their new words, whichever
  paragraph is written first.
- **Containers are pairs, fields are pairs, the rest is a tag of its own.** Links, content controls, and the
  insertions of the change tracking enclose runs, so the writer creates them anew around the new words. A field
  whose result lies in the same paragraph is a pair around its result, which keeps its start, instruction,
  and end in their order. Content controls bound to data, or offering a choice, a date, or a check box, stay
  as they are.
- **Bookmarks and comment ranges are no tags.** Their starts go before the new words, their ends after them,
  so a range only widens to its paragraph.
- **Changes of formatting are pairs as well.** Within a pair, or outside of any, the words in the most common
  formatting stay words, and those in any other formatting, e.g., a word in bold, get a pair of their own.
  Spaces never get one. A paragraph with more than `FormatSpans.MAX_PAIRS` such pairs keeps one formatting
  for its new words instead and counts as simplified: every pair is one more thing a model has to keep in
  place. `FormatSpans` serves every format which works this way, PowerPoint included.

## PowerPoint presentations

`PowerPoint/PowerPointSegmentFormat` reads the paragraphs of the shapes, groups, and tables of every slide, and
those of its notes, slide by slide. The rules of `app/MindWork AI Studio/Tools/PowerPoint/AGENTS.md` about
files hold here as well; its classes for the Slide Assistant are not used.

- **Masters and layouts stay as they are.** Their texts are prompts for authors, which every slide replaces.
- **A link is formatting.** PowerPoint keeps it in the properties of a run, so it becomes a pair like bold.
  Line breaks and fields, such as the number of the slide, are tags of their own.
- **The properties of the end of a paragraph come last.** The writer inserts new runs before them, as the
  schema demands.
- **Language and spell check are no formatting.** A run differs from another only in what changes its look;
  new runs carry the target language and no mark of an earlier spell check.

## Web pages

`Html/HtmlSegmentFormat` reads the runs of words between the blocks of a page, its title, its describing meta
elements, and the attributes people read (`alt`, `title`, `placeholder`, `aria-label`). Like Markdown, it
replaces the words in the source by their positions and leaves the rest byte for byte.

- **Elements which mark up words are pairs, the rest of a run is a tag of its own.** Code, pictures, line
  breaks, form fields, scripts, and comments stay as they are; preformatted blocks stay out entirely.
- **Words travel decoded and come back encoded.** The model gets the words as a browser shows them, entities
  decoded and white space collapsed. `HtmlSegmentReader.EncodedText` and `EncodedAttribute` encode them again,
  so new words never break the markup.
- **Attributes come first.** A readable attribute may lie within the markup of a block, e.g., a picture in a
  paragraph, so the writer replaces the attributes, reads the blocks of that page again, and replaces them then.
- **A run whose nodes leave markup in between stays as it is.** HtmlAgilityPack fixes broken markup in its
  tree, not in the source; what the tree does not cover would otherwise go to the model as words.

## Tests

The tests in `app/Tests/Tools/DocumentSegments/` play the model with a delegate, so they run without a
provider. Each format gets a test which writes every segment back unchanged and expects the same document.
Office documents are built in code, e.g., by `TestWordDocument` or by `TestTemplate` of the Slide Assistant,
so no binary document is checked in, and a test which writes one compares what the `OpenXmlValidator` finds
with what it finds in the source.