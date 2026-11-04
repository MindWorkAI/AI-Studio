//! Renders the pages of a OneNote section as Markdown, for `stream_onenote_section` in `file_data.rs`.
//!
//! The text is what OneNote shows: runs marked as hidden are left out, among them the markers which
//! carry the targets of hyperlinks. Nothing is escaped except the column separator inside a table
//! cell, so the prompt-injection filter reads the words exactly as the AI does.

use chrono::{DateTime, SecondsFormat};
use onenote_parser::contents::{Content, EmbeddedFile, Image, List, NoteTag, OutlineElement, OutlineItem, ParagraphStyling, RichText, Table, TableCell, TextHyperlink};
use onenote_parser::page::{Page, PageContent};
use onenote_parser::section::Section;
use crate::file_data::push_presentation_metadata_field;

/// OneNote marks the place of the automatic number in the format of a numbered list with this
/// character, cf. MS-ONE 2.3.20. A bulleted list does not contain it.
const LIST_NUMBER_PLACEHOLDER: char = '\u{FFFD}';

/// The deepest page level OneNote knows: a page, its subpage, and the subpage of that.
const DEEPEST_PAGE_LEVEL: usize = 3;

/// The metadata comment in front of the first page, naming the section the pages belong to.
pub(crate) fn section_metadata_to_markdown(section: &Section) -> Option<String> {
    let mut fields = Vec::new();
    push_presentation_metadata_field(&mut fields, "Title", Some(section.display_name()).filter(|name| !name.trim().is_empty()));

    if fields.is_empty() { None } else { Some(format!("<!-- Section Metadata\n{}\n-->", fields.join("\n"))) }
}

/// Renders a page: its title as a heading, its metadata as a comment, and then its contents in the
/// order OneNote stores them.
///
/// The title is a first-level heading and a subpage goes one level deeper, so the headings show the
/// AI how the pages of the section belong together. Headings inside a page start below its title.
pub(crate) fn page_to_markdown(page: &Page) -> String {
    let title_level = usize::try_from(page.level()).unwrap_or(1).clamp(1, DEEPEST_PAGE_LEVEL);
    let mut writer = PageWriter { blocks: Vec::new(), title_level };

    if let Some(title) = page.title_text().map(clean_text).filter(|title| !title.trim().is_empty()) {
        writer.blocks.push(format!("{} {}", "#".repeat(title_level), single_line(&title)));
    }

    writer.blocks.extend(page_metadata_to_markdown(page));

    for content in page.contents() {
        match content {
            PageContent::Outline(outline) => writer.write_items(outline.items(), 0),
            PageContent::Image(image) => writer.write_image(image),
            PageContent::EmbeddedFile(file) => writer.write_embedded_file(file),

            // Ink carries no text of its own. What OneNote recognized in it follows below:
            PageContent::Ink(_) | PageContent::Unknown => {},
        }
    }

    if let Some(recognition) = page.ink_recognition() {
        let handwriting = clean_text(&recognition.text());
        if !handwriting.trim().is_empty() {
            writer.blocks.push(format!("Handwriting recognized by OneNote:\n{handwriting}"));
        }
    }

    writer.blocks.join("\n\n")
}

/// The metadata comment of a page: who wrote it, and when.
fn page_metadata_to_markdown(page: &Page) -> Option<String> {
    let mut fields = Vec::new();
    push_presentation_metadata_field(&mut fields, "Author", page.author());
    push_presentation_metadata_field(&mut fields, "Created", format_time(page.created_time().unix_timestamp(), page.created_time().nanosecond()).as_deref());
    push_presentation_metadata_field(&mut fields, "Modified", format_time(page.updated_time().unix_timestamp(), page.updated_time().nanosecond()).as_deref());

    if fields.is_empty() { None } else { Some(format!("<!-- Page Metadata\n{}\n-->", fields.join("\n"))) }
}

/// Formats a point in time the way the metadata of documents and presentations shows it.
fn format_time(unix_timestamp: i64, nanosecond: u32) -> Option<String> {
    DateTime::from_timestamp(unix_timestamp, nanosecond).map(|time| time.to_rfc3339_opts(SecondsFormat::Secs, true))
}

/// Collects the Markdown blocks of a page, which are separated by blank lines.
struct PageWriter {
    blocks: Vec<String>,
    title_level: usize,
}

impl PageWriter {
    /// Writes the items of an outline. `list_depth` counts the list items they are nested in.
    fn write_items(&mut self, items: &[OutlineItem], list_depth: usize) {
        //
        // OneNote numbers a list itself, counting the items of the same format in a row, cf.
        // MS-ONE 2.3.20. Anything else between them starts the count anew:
        //
        let mut number = 0;
        for item in items {
            match item {
                OutlineItem::Element(element) => {
                    let numbered = element.list_contents().first().filter(|list| is_numbered(list));
                    number = match numbered {
                        Some(list) => list.list_restart().and_then(|restart| u32::try_from(restart).ok()).unwrap_or(number + 1),
                        None => 0,
                    };

                    self.write_element(element, list_depth, number);
                },

                OutlineItem::Group(group) => {
                    number = 0;
                    self.write_items(group.outlines(), list_depth);
                },
            }
        }
    }

    /// Writes an outline element and the elements nested in it.
    ///
    /// `number` is the position of the element in a numbered list, and is only read when the
    /// element is an item of one.
    fn write_element(&mut self, element: &OutlineElement, list_depth: usize, number: u32) {
        let list_marker = element.list_contents().first().map(|list| if is_numbered(list) { format!("{number}.") } else { "-".to_string() });

        //
        // A list item's marker goes in front of its first paragraph only. Four spaces per level
        // nest an item below its parent for both kinds of marker.
        //
        let mut prefix = list_marker.as_ref().map(|marker| format!("{}{marker} ", "    ".repeat(list_depth)));
        for content in element.contents() {
            match content {
                Content::RichText(text) => {
                    let paragraph = rich_text_to_markdown(text);
                    if paragraph.trim().is_empty() {
                        continue;
                    }

                    let tags = note_tags_to_markdown(text.note_tags());
                    let block = match prefix.take() {
                        Some(prefix) => format!("{prefix}{tags}{}", single_line(&paragraph)),
                        None => match heading_level(text.paragraph_style()) {
                            Some(level) => format!("{} {tags}{}", "#".repeat((self.title_level + level).min(6)), single_line(&paragraph)),
                            None => format!("{tags}{paragraph}"),
                        },
                    };

                    self.blocks.push(block);
                },

                Content::Table(table) => self.write_table(table),
                Content::Image(image) => self.write_image(image),
                Content::EmbeddedFile(file) => self.write_embedded_file(file),
                Content::Ink(_) | Content::Unknown => {},
            }
        }

        let child_depth = if list_marker.is_some() { list_depth + 1 } else { list_depth };
        self.write_items(element.children(), child_depth);
    }

    /// Writes a table. OneNote knows no header row, so the first row takes that place, which a
    /// Markdown table requires.
    fn write_table(&mut self, table: &Table) {
        let rows: Vec<Vec<String>> = table.contents().iter()
            .map(|row| row.contents().iter().map(|cell| self.cell_to_markdown(cell)).collect())
            .collect();

        let columns = rows.iter().map(Vec::len).max().unwrap_or(0);
        if columns == 0 {
            return;
        }

        let mut lines = Vec::with_capacity(rows.len() + 1);
        for (index, row) in rows.into_iter().enumerate() {
            let mut cells = row;
            cells.resize(columns, String::new());
            lines.push(format!("| {} |", cells.join(" | ")));

            if index == 0 {
                lines.push(format!("|{}|", vec![" --- "; columns].join("|")));
            }
        }

        self.blocks.push(lines.join("\n"));
    }

    /// A cell holds outline elements of its own, while a Markdown cell holds a single line. Its
    /// paragraphs are therefore joined by line break tags, and its column separators escaped.
    fn cell_to_markdown(&self, cell: &TableCell) -> String {
        let mut writer = PageWriter { blocks: Vec::new(), title_level: self.title_level };
        for element in cell.contents() {
            writer.write_element(element, 0, 0);
        }

        writer.blocks.join("<br>").replace('\n', "<br>").replace('|', "\\|")
    }

    /// Writes the text OneNote recognized in an image. The image itself travels separately.
    fn write_image(&mut self, image: &Image) {
        let text = image.alt_text().or(image.text()).map(clean_text).unwrap_or_default();
        if !text.trim().is_empty() {
            self.blocks.push(format!("Text in an image: {text}"));
        }
    }

    /// Names a file attached to the page, in a comment: its content stays in the file, so its name
    /// alone must not make a page look readable.
    fn write_embedded_file(&mut self, file: &EmbeddedFile) {
        let name = clean_text(file.filename());
        let mut fields = Vec::new();
        push_presentation_metadata_field(&mut fields, "Attached file", Some(name.as_str()).filter(|name| !name.trim().is_empty()));

        if let Some(field) = fields.pop() {
            self.blocks.push(format!("<!-- {field} -->"));
        }
    }
}

/// Whether a list is numbered rather than bulleted, cf. MS-ONE 2.3.20.
fn is_numbered(list: &List) -> bool {
    list.list_format().contains(&LIST_NUMBER_PLACEHOLDER)
}

/// The level of a paragraph formatted with one of OneNote's heading styles, which are named `h1`
/// to `h6`.
fn heading_level(style: &ParagraphStyling) -> Option<usize> {
    let level = style.style_id()?.strip_prefix('h')?.parse::<usize>().ok()?;
    (1..=6).contains(&level).then_some(level)
}

/// Renders the note tags of a paragraph. A tag with a check box becomes a Markdown task, every
/// other tag is named by its label, e.g., "Important" or "Question".
fn note_tags_to_markdown(tags: &[NoteTag]) -> String {
    let mut markdown = String::new();
    for tag in tags {
        let Some(definition) = tag.definition() else {
            continue;
        };

        if definition.shape().is_checkable() {
            markdown.push_str(if tag.item_status().completed() { "[x] " } else { "[ ] " });
        } else {
            let label = single_line(&clean_text(definition.label()));
            if !label.trim().is_empty() {
                markdown.push_str(&format!("({}) ", label.trim()));
            }
        }
    }

    markdown
}

/// Renders the visible text of a paragraph, with its hyperlinks as Markdown links.
///
/// The paragraph is split into its text runs, which are formatted alike, and at the ends of its
/// hyperlinks. Both are given as offsets in UTF-16 code units.
fn rich_text_to_markdown(text: &RichText) -> String {
    let content = text.text();
    let styles = text.text_run_formatting();
    if styles.is_empty() {
        return clean_text(content);
    }

    let byte_offsets = byte_offsets_by_utf16_offset(content);
    let length = byte_offsets.len() - 1;

    //
    // Each run ends where the next one starts, and the last one at the end of the paragraph. This
    // is how OneNote stores them, cf. MS-ONE 2.3.76:
    //
    let mut runs = Vec::with_capacity(styles.len());
    let mut start = 0;
    for (index, style) in styles.iter().enumerate() {
        let end = text.text_run_indices().get(index).map_or(length, |&end| end as usize).clamp(start, length);
        runs.push((start, end, style));
        start = end;
    }

    let links = text.hyperlinks();
    let mut boundaries = vec![0, length];
    boundaries.extend(runs.iter().map(|&(_, end, _)| end));
    boundaries.extend(links.iter().flat_map(|link| [link.start() as usize, link.end() as usize]).filter(|&offset| offset <= length));
    boundaries.sort_unstable();
    boundaries.dedup();

    let mut markdown = String::new();
    let mut open_link: Option<&TextHyperlink> = None;
    for window in boundaries.windows(2) {
        let (start, end) = (window[0], window[1]);
        let hidden = runs.iter().find(|&&(run_start, run_end, _)| run_start <= start && start < run_end).is_some_and(|&(_, _, style)| style.hidden());
        if hidden {
            continue;
        }

        if open_link.is_none() {
            open_link = links.iter().find(|link| link.start() as usize == start);
            if open_link.is_some() {
                markdown.push('[');
            }
        }

        markdown.push_str(&content[byte_offsets[start]..byte_offsets[end]]);

        if let Some(link) = open_link.filter(|link| link.end() as usize == end) {
            markdown.push_str(&format!("]({})", link.target()));
            open_link = None;
        }
    }

    if let Some(link) = open_link {
        markdown.push_str(&format!("]({})", link.target()));
    }

    clean_text(&markdown)
}

/// Maps every UTF-16 offset of a text to the byte offset of the character it falls into, plus the
/// end of the text. An offset inside a surrogate pair maps to the start of its character, so a
/// boundary there never splits a character.
fn byte_offsets_by_utf16_offset(text: &str) -> Vec<usize> {
    let mut offsets = Vec::with_capacity(text.len() + 1);
    for (byte_offset, character) in text.char_indices() {
        for _ in 0..character.len_utf16() {
            offsets.push(byte_offset);
        }
    }

    offsets.push(text.len());
    offsets
}

/// Removes the control characters OneNote uses internally. A vertical tab is OneNote's line break
/// inside a paragraph and becomes a line break.
fn clean_text(text: &str) -> String {
    text.chars()
        .filter_map(|character| match character {
            '\u{000B}' => Some('\n'),
            '\n' | '\t' => Some(character),
            '\u{FDDF}' => None,
            _ if character.is_control() => None,
            _ => Some(character),
        })
        .collect()
}

/// Puts text which Markdown needs on one line, e.g., a heading or a list item, on one line.
fn single_line(text: &str) -> String {
    text.split_whitespace().collect::<Vec<_>>().join(" ")
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn offsets_inside_a_surrogate_pair_never_split_its_character() {
        let text = "a😀b";
        let offsets = byte_offsets_by_utf16_offset(text);

        assert_eq!(offsets, vec![0, 1, 1, 5, 6]);
        assert_eq!(&text[offsets[1]..offsets[3]], "😀");
    }

    #[test]
    fn the_internal_control_characters_of_onenote_are_removed() {
        assert_eq!(clean_text("first\u{000B}second\u{FDDF}\u{0007}"), "first\nsecond");
    }

    #[test]
    fn a_title_across_lines_ends_up_on_one_line() {
        assert_eq!(single_line(" A  title\nacross lines "), "A title across lines");
    }

    #[test]
    fn points_in_time_read_like_the_metadata_of_documents() {
        assert_eq!(format_time(0, 0).as_deref(), Some("1970-01-01T00:00:00Z"));
    }
}
