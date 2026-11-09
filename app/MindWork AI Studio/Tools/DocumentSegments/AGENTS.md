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
  sends such a segment without its tags.
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

## Tests

The tests in `app/Tests/Tools/DocumentSegments/` play the model with a delegate, so they run without a
provider. Each format gets a test which writes every segment back unchanged and expects the same document.