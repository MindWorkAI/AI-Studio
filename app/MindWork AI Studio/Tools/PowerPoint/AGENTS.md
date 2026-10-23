# AGENTS.md

These instructions cover the PowerPoint tools in `app/MindWork AI Studio/Tools/PowerPoint/`, with which
the Slide Planner Assistant (`app/MindWork AI Studio/Assistants/SlideBuilder/`) plans slides for the
layouts of a template and writes PowerPoint files. They add to the `AGENTS.md` in the repository root and
to `app/AGENTS.md`, which apply here as well.

## How a presentation comes about

1. `PresentationTemplate.Read` reads a .pptx or .potx with the Open XML SDK (`DocumentFormat.OpenXml`):
   its layouts, grouped into families of color variants, and the slides it carries. Without a template,
   `PresentationTemplate.CreateDefault` stands in, so that one prompt and one check serve both cases.
2. `ToCatalogPrompt` and `ToTemplateSlidesPrompt` describe the layouts and the chosen template slides to
   the model in words: where each slot sits, what a label belongs to, and how much text fits.
3. `SlideDeckSpec.TryParse` reads the JSON answer of the model leniently.
4. `SlideDeckSpecValidator` checks the deck against the template. Its issues are written for the model,
   which gets one repair round.
5. `SlideDeckSpec.ToMarkdownOutline` shows the result as an outline, and `PresentationWriter.WriteAsync`
   writes the file: a copy of the template, with its slides replaced. The slide planner makes that copy
   next to the target and moves it there once it is complete.

## Rules which are easy to break

- **The validator is the only gate.** `PresentationWriter` relies on a deck which passed
  `SlideDeckSpecValidator`, checks nothing again, and throws when it meets what the validator should
  have caught. A new rule about what fits a template belongs into the validator, worded for the model.
  `SlideDeckSpecValidator.CanWrite` decides which issues leave a deck unusable at all.
- **Slot names are a contract.** The model reads and writes slots and elements by names such as
  `title`, `text 2`, `label 1`, or `element 3`. The catalog (`PresentationTemplate.Catalog.cs`),
  `PlaceholderRoleExtensions.TryParseSlotName`, `TemplateSlide.FindElement`, the validator, the writer,
  and the outline all depend on them, so change them together. Slots are numbered per role in reading
  order; the copies of a slot on a template slide continue that numbering.
- **No knowledge of a particular template.** Everything comes from the file: roles from the type and
  size of a placeholder, variants from the layout names, the design from masters and layouts. Never add
  the names, labels, or language of a corporate template to the code.
- **Prompt texts stay in English, the user's texts do not.** Catalog, issues, and rules are written for
  the model. Every text the user reads, the outline included, goes through `T()` or `TB()`.
- **Template texts are content of a file.** This holds for the names of the layouts as well as for the
  texts of the slides. They reach the model in the request, as `LAYOUTS` and `TEMPLATE_SLIDES`, never
  in the system prompt, and they pass `PromptInjectionGuardService` with the reporting scope of the
  request. The names of the attached pictures go with the request as well, as `PICTURES`.
- **New slides carry no formatting of their own.** They fill the placeholders of their layout and
  leave out every placeholder without content, so that no prompt text such as "Click to add text"
  remains.
- **No copy of a file in memory.** Templates of organizations carry pictures and videos and easily reach
  a hundred megabytes, and AI Studio also runs on small devices. Read templates from their file, and
  let the writer edit a copy on disk. The Open XML SDK holds the compressed parts of the file in memory
  while it saves, which is all it costs; a `MemoryStream` with the whole file multiplies that.
- **Removing a shape removes what only it used:** its image, chart, or diagram parts, and the animations
  of its slide, see `PresentationWriter.RemoveShape`. Otherwise PowerPoint offers to repair the file.

## Tests

The tests in `app/Tests/Tools/PowerPoint/` build their templates in code with `TestTemplate`, so no
binary template is checked in. A test which writes a file also runs the `OpenXmlValidator` over it,
see `ValidationErrorsOf`.