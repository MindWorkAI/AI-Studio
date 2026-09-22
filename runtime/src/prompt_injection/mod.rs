//! Detects prompt injections in untrusted content and filters them out.
//!
//! Everything a user hands to a model from the outside world — a file, a web page, a
//! retrieval context — may contain instructions aimed at the model rather than text meant
//! for the reader. This module finds those and removes them, so the surrounding document
//! stays usable instead of being rejected as a whole.
//!
//! It works on a stream. `extract_data` yields a document chunk by chunk, and the sanitizer
//! sees each chunk as it passes, which is what makes a 3000-page document affordable: the
//! whole text never exists in memory at once, neither here nor in the .NET app.
//!
//! Patterns do not respect chunk boundaries, so a chunk is not released as soon as it was
//! scanned. The tail of the text stays behind and is prepended to the next chunk, and only
//! what precedes that tail is handed on. A phrase split across two PDF pages is therefore
//! still intact by the time it is scanned and can still be redacted, because nothing
//! containing it has left the sanitizer yet.

pub mod api;

mod decode;
mod normalize;
mod rules;

use rules::{Redaction, PHRASE_RULES, STRUCTURAL, STRUCTURAL_COMPACT, TYPOGLYCEMIA_KEYWORDS};
use serde::{Deserialize, Serialize};
use std::collections::HashSet;
use std::time::{Duration, Instant};

/// What replaces a redacted passage.
///
/// The wording is deliberately plain: the marker travels on to the model as part of the
/// document, and words like "injection" or "ignore" would make the marker itself look like
/// an attack to the next scan.
const REDACTION_MARKER: &str = "[AI Studio removed suspicious content here]";

/// How much text is held back to catch patterns that straddle a chunk boundary.
///
/// Comfortably above the longest pattern any rule can match, which is bounded by the
/// `{0,300}` spans in the markup rules.
const OVERLAP_BYTES: usize = 4_096;

/// How much new text has to arrive before the held-back buffer is scanned again.
///
/// Scanning on every chunk would re-scan the whole buffer each time. A text file arrives
/// line by line, so that would mean scanning several kilobytes per line — quadratic in the
/// size of the document. Waiting for a batch bounds it: every byte is scanned about twice,
/// once as new text and once as overlap.
const SCAN_BATCH_BYTES: usize = 8_192;

/// The most findings reported for one document. The report explains to a user what was
/// found; past a handful more entries add no insight, while redaction continues regardless.
const MAX_FINDINGS: usize = 8;

/// How much of the surrounding sentence a finding quotes.
const MAX_SNIPPET_LENGTH: usize = 240;

/// Where a quoted finding is cut off, so the snippet shows a sentence rather than a fragment.
const SENTENCE_BOUNDARIES: [char; 5] = ['.', '!', '?', '\r', '\n'];

/// The family of a detected prompt-injection rule.
///
/// The snake_case spelling is the wire format: it is what `phrases.toml` writes and what the
/// .NET app reads, so renaming a variant without renaming it there breaks both.
#[derive(Debug, Clone, Copy, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "snake_case")]
pub enum FindingCategory {
    Override,
    RoleOverride,
    Exfiltration,
    Jailbreak,
    AgentManipulation,
    DelimiterEvasion,
    MarkupEvasion,
    EncodingEvasion,
    Persistence,
    Evasion,
}

/// One detected injection attempt, as reported to the .NET app.
#[derive(Debug, Clone, Serialize, PartialEq, Eq)]
pub struct Finding {
    /// Which rule matched, e.g. `instruction_override`.
    pub rule_id: String,

    /// The rule's family, e.g. `FindingCategory::Exfiltration`.
    pub category: FindingCategory,

    /// The passage as it appeared in the document, for showing the user what was removed.
    pub snippet: String,
}

/// What the sanitizer saw across a whole document.
#[derive(Debug, Clone, Serialize, Default)]
pub struct Report {
    pub findings: Vec<Finding>,

    /// How many passages were replaced or removed. May exceed `findings.len()`, which is
    /// capped, so the user still learns the true extent of the filtering.
    pub redacted_count: usize,
}

impl Report {
    pub fn is_empty(&self) -> bool {
        self.redacted_count == 0
    }
}

/// A passage to remove, in byte offsets of the text being sanitized.
#[derive(Debug, Clone, Copy)]
struct Redactable {
    start: usize,
    end: usize,
    redaction: Redaction,
}

/// A chunk that was handed in but not released yet.
struct Part {
    /// The caller's handle for this chunk. `extract_data` uses it to pair the sanitized text
    /// back up with the chunk's metadata, which matters because that metadata ends up in the
    /// document: a page number travels with its page, and releasing text under the wrong one
    /// would put `# Page 41` in front of page 42's text.
    id: u64,
    text: String,
}

/// Filters prompt injections out of a document as it streams past.
pub struct Sanitizer {
    /// Chunks scanned but not released yet, so a pattern crossing a chunk boundary can still
    /// be redacted. Kept as separate chunks rather than one string so each one can be handed
    /// back under its own id.
    pending: Vec<Part>,
    pending_bytes: usize,

    /// Bytes added since the last scan. Scanning on every chunk would re-scan the whole
    /// held-back buffer each time, which turns a line-by-line text file into quadratic work.
    unscanned_bytes: usize,

    findings: Vec<Finding>,
    seen: HashSet<(String, String)>,
    redacted_count: usize,

    /// How much text was scanned and how long it took, for the log line the extraction writes
    /// when it is done. Without it, a slow scan is only visible by reproducing it.
    scanned_bytes: usize,
    scan_duration: Duration,
}

impl Default for Sanitizer {
    fn default() -> Self {
        Self::new()
    }
}

impl Sanitizer {
    pub fn new() -> Self {
        Self {
            pending: Vec::new(),
            pending_bytes: 0,
            unscanned_bytes: 0,
            findings: Vec::new(),
            seen: HashSet::new(),
            redacted_count: 0,
            scanned_bytes: 0,
            scan_duration: Duration::ZERO,
        }
    }

    /// Takes the next chunk under the caller's `id` and returns the chunks that are now safe
    /// to release, in order.
    ///
    /// Usually returns nothing: chunks are held until enough text has arrived to scan across
    /// their boundaries. Call `flush` to release what is left.
    pub fn push(&mut self, id: u64, text: &str) -> Vec<(u64, String)> {
        self.pending_bytes += text.len();
        self.unscanned_bytes += text.len();
        self.pending.push(Part { id, text: text.to_string() });

        if self.unscanned_bytes < SCAN_BATCH_BYTES {
            return Vec::new();
        }

        self.process(false)
    }

    /// Whether pushing this many bytes scans, rather than only buffering the chunk.
    ///
    /// Lets the caller move the scan off its thread without paying for the pushes that merely
    /// add a chunk to the buffer, which is most of them.
    pub fn will_scan(&self, incoming_bytes: usize) -> bool {
        self.unscanned_bytes + incoming_bytes >= SCAN_BATCH_BYTES
    }

    /// Releases every chunk still held back.
    ///
    /// Only now is the end of the buffered text the end of the document, so matches reaching
    /// it can finally be acted on.
    pub fn flush(&mut self) -> Vec<(u64, String)> {
        self.process(true)
    }

    /// How many bytes were scanned and how long that took.
    ///
    /// The scanned amount exceeds the document, because the held-back tail is scanned again
    /// with the chunk that follows it.
    pub fn scan_stats(&self) -> (usize, Duration) {
        (self.scanned_bytes, self.scan_duration)
    }

    /// What was found across the whole document.
    pub fn into_report(self) -> Report {
        Report { findings: self.findings, redacted_count: self.redacted_count }
    }

    /// Scans everything held back, redacts it, and decides what may be released.
    fn process(&mut self, is_final: bool) -> Vec<(u64, String)> {
        self.unscanned_bytes = 0;
        if self.pending.is_empty() {
            return Vec::new();
        }

        // The scan runs across chunk boundaries, so the chunks are joined for it and the
        // result is taken apart again afterwards.
        let mut buffer = String::with_capacity(self.pending_bytes);
        let mut spans = Vec::with_capacity(self.pending.len());
        for part in &self.pending {
            let start = buffer.len();
            buffer.push_str(&part.text);
            spans.push((part.id, start, buffer.len()));
        }

        let scan_start = Instant::now();
        let redactions = self.collect_redactions(&buffer, is_final);
        let mut parts = apply_to_parts(&buffer, &spans, redactions);
        self.scan_duration += scan_start.elapsed();
        self.scanned_bytes += buffer.len();

        if is_final {
            self.pending.clear();
            self.pending_bytes = 0;
            return parts;
        }

        // Hold back the last chunks, enough of them to cover any pattern that might continue
        // into the chunk still to come.
        let mut held_bytes = 0;
        let mut first_held = parts.len();
        while first_held > 0 && held_bytes < OVERLAP_BYTES {
            first_held -= 1;
            held_bytes += parts[first_held].1.len();
        }

        let held = parts.split_off(first_held);
        self.pending_bytes = held.iter().map(|(_, text)| text.len()).sum();
        self.pending = held.into_iter().map(|(id, text)| Part { id, text }).collect();

        parts
    }

    /// Collects everything to redact in `text`.
    ///
    /// `is_final` says whether the end of `text` is the end of the document. While it is
    /// not, a match touching that end is ignored: the text may continue in the next chunk,
    /// and redacting `instruction` before its `s` has arrived would leave the `s` behind.
    /// Nothing is lost by waiting because the chunk containing the match is held back and
    /// scanned again.
    fn collect_redactions(&mut self, text: &str, is_final: bool) -> Vec<Redactable> {
        let mut redactions = Vec::new();

        self.collect_phrase_matches(text, is_final, &mut redactions);
        self.collect_structural_matches(text, is_final, &mut redactions);
        self.collect_encoded_matches(text, is_final, &mut redactions);
        self.collect_spaced_and_shuffled_matches(text, is_final, &mut redactions);

        redactions
    }

    /// Whether a match may be acted on, or has to wait for more text.
    fn is_settled(text: &str, end: usize, is_final: bool) -> bool {
        is_final || end < text.len()
    }

    /// Matches the fixed phrase list against the whitespace-collapsed, lowercased text.
    fn collect_phrase_matches(&mut self, text: &str, is_final: bool, redactions: &mut Vec<Redactable>) {
        let normalized = normalize::collapse_whitespace(text);
        let rules = &*PHRASE_RULES;

        for matched in rules.automaton().find_iter(&normalized.text) {
            let (rule_id, category) = rules.rule_for(matched.pattern().as_usize());
            let (start, end) = normalized.to_source_range(matched.start(), matched.end());
            if !Self::is_settled(text, end, is_final) {
                continue;
            }

            self.record(text, start, end, rule_id, category);
            redactions.push(Redactable { start, end, redaction: Redaction::Marker });
        }
    }

    /// Matches the structural patterns against the text as it stands.
    fn collect_structural_matches(&mut self, text: &str, is_final: bool, redactions: &mut Vec<Redactable>) {
        for (rule, pattern) in STRUCTURAL.rules() {
            for matched in pattern.find_iter(text) {
                if !Self::is_settled(text, matched.end(), is_final) {
                    continue;
                }

                // A silent rule removes an invisible carrier; quoting it would show the
                // user something they never saw, so only visible matches are reported.
                if rule.redaction == Redaction::Marker {
                    self.record(text, matched.start(), matched.end(), rule.id, rule.category);
                } else {
                    self.redacted_count += 1;
                }

                redactions.push(Redactable {
                    start: matched.start(),
                    end: matched.end(),
                    redaction: rule.redaction,
                });
            }
        }
    }

    /// Scans what base64 and hex carriers decode to, and redacts the carrier on a hit.
    fn collect_encoded_matches(&mut self, text: &str, is_final: bool, redactions: &mut Vec<Redactable>) {
        let blocks = decode::find_base64_blocks(text)
            .into_iter()
            .chain(decode::find_hex_blocks(text));

        for block in blocks {
            if !Self::is_settled(text, block.end, is_final) {
                continue;
            }

            let Some((rule_id, category)) = first_hit(&block.text) else {
                continue;
            };

            // The decoded phrase exists nowhere in the document, so the encoded block is
            // what has to go. The snippet quotes the decoded text, because that is what
            // explains to the user why the block was removed.
            self.push_finding(&rule_id, category, snippet_of(&block.text, 0, block.text.len()));
            redactions.push(Redactable {
                start: block.start,
                end: block.end,
                redaction: Redaction::Marker,
            });
        }
    }

    /// Catches text written one character at a time and keywords with shuffled middles.
    fn collect_spaced_and_shuffled_matches(&mut self, text: &str, is_final: bool, redactions: &mut Vec<Redactable>) {
        let spaced = normalize::extract_spaced_letters(text);
        if !spaced.text.is_empty() {
            let rules = &*PHRASE_RULES;

            // The spaced passages carry no spaces any more, so both the phrase list and the
            // structural patterns are applied in their space-free variants.
            for matched in rules.compact_automaton().find_iter(&spaced.text) {
                let (rule_id, category) = rules.rule_for(matched.pattern().as_usize());
                let (start, end) = spaced.to_source_range(matched.start(), matched.end());
                if !Self::is_settled(text, end, is_final) {
                    continue;
                }

                self.record(text, start, end, rule_id, category);
                redactions.push(Redactable { start, end, redaction: Redaction::Marker });
            }

            for (rule, pattern) in STRUCTURAL_COMPACT.rules() {
                for matched in pattern.find_iter(&spaced.text) {
                    let (start, end) = spaced.to_source_range(matched.start(), matched.end());
                    if !Self::is_settled(text, end, is_final) {
                        continue;
                    }

                    self.record(text, start, end, rule.id, rule.category);
                    redactions.push(Redactable { start, end, redaction: Redaction::Marker });
                }
            }
        }

        for (start, end, keyword) in typoglycemia_hits(text) {
            if !Self::is_settled(text, end, is_final) {
                continue;
            }

            self.push_finding(
                &format!("typoglycemia:{keyword}"),
                FindingCategory::Evasion,
                snippet_of(text, start, end),
            );

            redactions.push(Redactable { start, end, redaction: Redaction::Marker });
        }
    }

    fn record(&mut self, text: &str, start: usize, end: usize, rule_id: &str, category: FindingCategory) {
        self.push_finding(rule_id, category, snippet_of(text, start, end));
    }

    fn push_finding(&mut self, rule_id: &str, category: FindingCategory, snippet: String) {
        self.redacted_count += 1;

        let key = (rule_id.to_string(), snippet.clone());
        if !self.seen.insert(key) {
            // The same passage is seen again whenever a chunk boundary makes us rescan the
            // held-back tail. Counting it twice would misreport the extent of the filtering.
            self.redacted_count -= 1;
            return;
        }

        if self.findings.len() >= MAX_FINDINGS {
            return;
        }

        self.findings.push(Finding {
            rule_id: rule_id.to_string(),
            category,
            snippet,
        });
    }

}

/// Applies every redaction to the joined buffer and hands each chunk back separately.
///
/// `spans` says which byte range of `buffer` belongs to which chunk. A redaction may cross
/// a chunk boundary — that is the whole reason the chunks were joined — so the text it
/// removes is taken out of every chunk it touches, while the marker replacing it goes into
/// the chunk where the match began.
fn apply_to_parts(
    buffer: &str,
    spans: &[(u64, usize, usize)],
    mut redactions: Vec<Redactable>,
) -> Vec<(u64, String)> {
    let mut parts: Vec<(u64, String)> = spans.iter().map(|(id, _, _)| (*id, String::new())).collect();
    if redactions.is_empty() {
        for (index, (_, start, end)) in spans.iter().enumerate() {
            parts[index].1.push_str(&buffer[*start..*end]);
        }

        return parts;
    }

    redactions.sort_by_key(|redaction| (redaction.start, std::cmp::Reverse(redaction.end)));

    // Copies a byte range of the buffer into the chunks it belongs to.
    let copy = |from: usize, to: usize, parts: &mut Vec<(u64, String)>| {
        for (index, (_, span_start, span_end)) in spans.iter().enumerate() {
            let start = from.max(*span_start);
            let end = to.min(*span_end);
            if start < end {
                parts[index].1.push_str(&buffer[start..end]);
            }
        }
    };

    // Which chunk a position belongs to, for placing the marker.
    let chunk_of = |position: usize| {
        spans
            .iter()
            .position(|(_, start, end)| position >= *start && position < *end)
            .unwrap_or(spans.len().saturating_sub(1))
    };

    let mut cursor = 0;
    for redaction in redactions {
        // Overlapping matches are common: a phrase and a structural rule often describe the
        // same sentence. Whatever was already replaced is skipped.
        if redaction.start < cursor {
            continue;
        }

        let start = floor_char_boundary(buffer, redaction.start);
        let end = ceil_char_boundary(buffer, redaction.end);
        if start >= end {
            continue;
        }

        copy(cursor, start, &mut parts);
        if redaction.redaction == Redaction::Marker {
            parts[chunk_of(start)].1.push_str(REDACTION_MARKER);
        }

        cursor = end;
    }

    copy(cursor, buffer.len(), &mut parts);
    parts
}

/// Returns the first rule that matches a decoded payload, if any.
fn first_hit(text: &str) -> Option<(String, FindingCategory)> {
    // Stops at the first rule that matches; which one it is only decides how the finding is
    // labelled, and the carrier is removed either way.
    if let Some((rule, _)) = STRUCTURAL.rules().find(|(_, pattern)| pattern.is_match(text)) {
        return Some((rule.id.to_string(), rule.category));
    }

    let normalized = normalize::collapse_whitespace(text);
    let rules = &*PHRASE_RULES;
    let matched = rules.automaton().find(&normalized.text)?;
    let (rule_id, category) = rules.rule_for(matched.pattern().as_usize());

    Some((rule_id.to_string(), category))
}

/// Finds words that are a letter-shuffled variant of a watched keyword.
///
/// `ignroe` reads as `ignore` to a model but matches no phrase. Same first and last letter,
/// same letters in between, different order.
fn typoglycemia_hits(text: &str) -> Vec<(usize, usize, &'static str)> {
    let mut hits = Vec::new();

    for (start, word) in ascii_words(text) {
        for keyword in TYPOGLYCEMIA_KEYWORDS {
            if is_shuffled_variant(word, keyword) {
                hits.push((start, start + word.len(), *keyword));
                break;
            }
        }
    }

    hits
}

/// Yields the ASCII letter runs of a text with their byte offsets.
fn ascii_words(text: &str) -> Vec<(usize, &str)> {
    let bytes = text.as_bytes();
    let mut words = Vec::new();
    let mut index = 0;

    while index < bytes.len() {
        if !bytes[index].is_ascii_alphabetic() {
            index += 1;
            continue;
        }

        let start = index;
        while index < bytes.len() && bytes[index].is_ascii_alphabetic() {
            index += 1;
        }

        // Matches the length window the keyword list covers:
        if index - start >= 5 && index - start <= 12 {
            words.push((start, &text[start..index]));
        }
    }

    words
}

fn is_shuffled_variant(word: &str, keyword: &str) -> bool {
    if word.len() != keyword.len() || word.eq_ignore_ascii_case(keyword) {
        return false;
    }

    let word = word.as_bytes();
    let keyword = keyword.as_bytes();
    if !word[0].eq_ignore_ascii_case(&keyword[0]) || !word[word.len() - 1].eq_ignore_ascii_case(&keyword[keyword.len() - 1]) {
        return false;
    }

    let mut counts = [0i32; 26];
    for index in 1..word.len() - 1 {
        let word_letter = word[index].to_ascii_lowercase();
        if !word_letter.is_ascii_lowercase() {
            return false;
        }

        counts[(word_letter - b'a') as usize] += 1;
        counts[(keyword[index] - b'a') as usize] -= 1;
    }

    counts.iter().all(|&count| count == 0)
}

/// Quotes a match together with enough of its sentence to be recognisable.
fn snippet_of(text: &str, start: usize, end: usize) -> String {
    let start = floor_char_boundary(text, start.min(text.len()));
    let end = ceil_char_boundary(text, end.min(text.len())).max(start);

    let sentence_start = text[..start]
        .rfind(SENTENCE_BOUNDARIES)
        .map(|index| index + 1)
        .unwrap_or(0);

    let sentence_end = text[end..]
        .find(SENTENCE_BOUNDARIES)
        .map(|index| end + index + 1)
        .unwrap_or(text.len());

    let sentence_start = floor_char_boundary(text, sentence_start);
    let sentence_end = ceil_char_boundary(text, sentence_end);
    let quoted = &text[sentence_start..sentence_end];

    let normalized: String = quoted.split_whitespace().collect::<Vec<_>>().join(" ");
    if normalized.chars().count() <= MAX_SNIPPET_LENGTH {
        return normalized;
    }

    let truncated: String = normalized.chars().take(MAX_SNIPPET_LENGTH - 3).collect();
    format!("{truncated}...")
}

/// `str::floor_char_boundary` is still unstable, so both directions are done here.
fn floor_char_boundary(text: &str, index: usize) -> usize {
    let mut index = index.min(text.len());
    while index > 0 && !text.is_char_boundary(index) {
        index -= 1;
    }

    index
}

fn ceil_char_boundary(text: &str, index: usize) -> usize {
    let mut index = index.min(text.len());
    while index < text.len() && !text.is_char_boundary(index) {
        index += 1;
    }

    index
}

/// Sanitizes a text that is not streamed, such as a web page or a retrieval context.
pub fn sanitize_text(text: &str) -> (String, Report) {
    let mut sanitizer = Sanitizer::new();
    let mut result = String::with_capacity(text.len());

    for (_, part) in sanitizer.push(0, text) {
        result.push_str(&part);
    }

    for (_, part) in sanitizer.flush() {
        result.push_str(&part);
    }

    (result, sanitizer.into_report())
}

#[cfg(test)]
mod tests;