# ReverseMarkdown Blocks in Inline Elements

- Status: Active
- Introduced: 2026-11-26
- Remove after: when a ReverseMarkdown release keeps the words of the blocks apart which it writes into the text of an inline element, such as a link, see [mysticmind/reversemarkdown-net#440](https://github.com/mysticmind/reversemarkdown-net/issues/440)
- Code references:
  - `app/MindWork AI Studio/Tools/HTMLParser.cs` (`UnwrapBlocksInsideInlineElements`, `IsBlockInsideInlineElement`, `NeedsSeparator`, `INLINE_ELEMENTS`, `BLOCK_ELEMENTS`)
  - `app/Tests/Tools/HTMLParserConversionTests.cs` (`BlocksInsideALinkKeepTheirWordsApart`)
  - `app/TrimSmokeTest/MarkdownChecks.cs` (`TEASER_HTML`, `TEASER_MARKDOWN`)

## User Impact

AI Studio converts web pages and the HTML of mails to Markdown with ReverseMarkdown before a model reads them, e.g., for the web search, the Read Web Page tool, and the mailboxes. Many pages wrap a whole teaser in a link: a heading, a line of metadata, and a summary, each a block of its own. A link holds no blocks in Markdown, so ReverseMarkdown 6 writes the texts of those blocks one after another into the text of the link, without anything in between. A teaser then reads, e.g., "Java77.721 Abrufeheise security" instead of "Java 77.721 Abrufe heise security". On the start page of a news site, this glued about 140 words together; the text of an article itself is rarely affected. ReverseMarkdown 5 had no such problem, since it kept the blocks, although as invalid Markdown.

## Compatibility Behavior

The converter of `HTMLParser` runs a preprocessing step of its own, through the preprocessing API of ReverseMarkdown. The step finds every block element inside an inline element, such as a `div`, a heading, or a list inside a link, and replaces it by its content. Before and after that content, it puts a space, unless there is whitespace already. Spaces next to the blocks alone were not enough: a first attempt which only added them still left most teasers on the start page of a news site glued together. Tables and `pre` keep their structure, and inside `pre` nothing changes at all.

The result is what ReverseMarkdown makes of such a block anyway, only with the words apart. Pages without blocks inside inline elements convert exactly as without this step; on eight real pages, only the two start pages of news sites changed.

## Removal Checklist

- Check that the new ReverseMarkdown release separates the blocks inside an inline element itself, e.g., with the test `BlocksInsideALinkKeepTheirWordsApart` and the preprocessing step switched off.
- Remove `UnwrapBlocksInsideInlineElements`, `IsBlockInsideInlineElement`, `NeedsSeparator`, `INLINE_ELEMENTS`, and `BLOCK_ELEMENTS` from `HTMLParser`, and the `Preprocess` entry from its configuration.
- Keep `BlocksInsideALinkKeepTheirWordsApart` and the teaser of `MarkdownChecks`: they then check the library itself.
- Update this document's status to `Removed`.