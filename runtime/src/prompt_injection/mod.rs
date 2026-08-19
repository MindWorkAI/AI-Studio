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

mod decode;
mod normalize;
mod rules;

use rules::{Redaction, PHRASE_RULES, STRUCTURAL, STRUCTURAL_COMPACT, TYPOGLYCEMIA_KEYWORDS};
use serde::Serialize;
use std::collections::HashSet;

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

/// The most findings reported for one document. The report explains to a user what was
/// found; past a handful more entries add no insight, while redaction continues regardless.
const MAX_FINDINGS: usize = 8;

/// How much of the surrounding sentence a finding quotes.
const MAX_SNIPPET_LENGTH: usize = 240;

/// Where a quoted finding is cut off, so the snippet shows a sentence rather than a fragment.
const SENTENCE_BOUNDARIES: [char; 5] = ['.', '!', '?', '\r', '\n'];

/// One detected injection attempt, as reported to the .NET app.
#[derive(Debug, Clone, Serialize, PartialEq, Eq)]
pub struct Finding {
    /// Which rule matched, e.g. `instruction_override`.
    pub rule_id: String,

    /// The rule's family, e.g. `exfiltration`.
    pub category: String,

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

/// Filters prompt injections out of a document as it streams past.
pub struct Sanitizer {
    /// Text scanned but not yet released, so a pattern crossing into the next chunk can
    /// still be redacted.
    pending: String,

    findings: Vec<Finding>,
    seen: HashSet<(String, String)>,
    redacted_count: usize,
}

impl Default for Sanitizer {
    fn default() -> Self {
        Self::new()
    }
}

impl Sanitizer {
    pub fn new() -> Self {
        Self {
            pending: String::new(),
            findings: Vec::new(),
            seen: HashSet::new(),
            redacted_count: 0,
        }
    }

    /// Takes the next chunk and returns the text that is safe to release.
    ///
    /// The returned text is usually shorter than what went in: the tail is held back until
    /// the following chunk arrives. Call `finish` to get the remainder.
    pub fn sanitize(&mut self, chunk: &str) -> String {
        self.pending.push_str(chunk);
        let buffer = std::mem::take(&mut self.pending);
        let sanitized = self.scan_and_redact(buffer, false);

        // Hold back the tail, but never split a character in half:
        let split_at = sanitized.len().saturating_sub(OVERLAP_BYTES);
        let split_at = floor_char_boundary(&sanitized, split_at);

        self.pending = sanitized[split_at..].to_string();
        sanitized[..split_at].to_string()
    }

    /// Releases the held-back tail and returns what was found in the whole document.
    pub fn finish(mut self) -> (String, Report) {
        // Only now is the end of the text the actual end, so matches reaching it can be
        // acted on. Until this point they might still have continued into the next chunk.
        let buffer = std::mem::take(&mut self.pending);
        let remainder = self.scan_and_redact(buffer, true);
        let report = Report { findings: self.findings, redacted_count: self.redacted_count };

        (remainder, report)
    }

    /// Scans `text` and replaces what was found.
    ///
    /// `is_final` says whether the end of `text` is the end of the document. While it is
    /// not, a match touching that end is ignored: the text may continue in the next chunk,
    /// and redacting `instruction` before its `s` has arrived would leave the `s` behind.
    /// Nothing is lost by waiting because the tail containing the match is held back and
    /// scanned again.
    fn scan_and_redact(&mut self, text: String, is_final: bool) -> String {
        let mut redactions = Vec::new();

        self.collect_phrase_matches(&text, is_final, &mut redactions);
        self.collect_structural_matches(&text, is_final, &mut redactions);
        self.collect_encoded_matches(&text, is_final, &mut redactions);
        self.collect_spaced_and_shuffled_matches(&text, is_final, &mut redactions);

        if redactions.is_empty() {
            return text;
        }

        self.apply(&text, redactions)
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
        for index in STRUCTURAL.matching(text) {
            let (rule, pattern) = STRUCTURAL.rule(index);
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
            self.push_finding(&rule_id, &category, snippet_of(&block.text, 0, block.text.len()));
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

            for index in STRUCTURAL_COMPACT.matching(&spaced.text) {
                let (rule, pattern) = STRUCTURAL_COMPACT.rule(index);
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
                "evasion",
                snippet_of(text, start, end),
            );

            redactions.push(Redactable { start, end, redaction: Redaction::Marker });
        }
    }

    fn record(&mut self, text: &str, start: usize, end: usize, rule_id: &str, category: &str) {
        self.push_finding(rule_id, category, snippet_of(text, start, end));
    }

    fn push_finding(&mut self, rule_id: &str, category: &str, snippet: String) {
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
            category: category.to_string(),
            snippet,
        });
    }

    /// Replaces every redacted range, merging the ones that overlap.
    fn apply(&self, text: &str, mut redactions: Vec<Redactable>) -> String {
        redactions.sort_by_key(|redaction| (redaction.start, std::cmp::Reverse(redaction.end)));

        let mut result = String::with_capacity(text.len());
        let mut cursor = 0;

        for redaction in redactions {
            // Overlapping matches are common: a phrase and a structural rule often describe
            // the same sentence. Whatever was already replaced is skipped.
            if redaction.start < cursor {
                continue;
            }

            let start = floor_char_boundary(text, redaction.start);
            let end = ceil_char_boundary(text, redaction.end);
            if start >= end {
                continue;
            }

            result.push_str(&text[cursor..start]);
            if redaction.redaction == Redaction::Marker {
                result.push_str(REDACTION_MARKER);
            }

            cursor = end;
        }

        result.push_str(&text[cursor..]);
        result
    }
}

/// Returns the first rule that matches a decoded payload, if any.
fn first_hit(text: &str) -> Option<(String, String)> {
    if let Some(&index) = STRUCTURAL.matching(text).first() {
        let (rule, _) = STRUCTURAL.rule(index);
        return Some((rule.id.to_string(), rule.category.to_string()));
    }

    let normalized = normalize::collapse_whitespace(text);
    let rules = &*PHRASE_RULES;
    let matched = rules.automaton().find(&normalized.text)?;
    let (rule_id, category) = rules.rule_for(matched.pattern().as_usize());

    Some((rule_id.to_string(), category.to_string()))
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
    let mut result = sanitizer.sanitize(text);
    let (remainder, report) = sanitizer.finish();
    result.push_str(&remainder);

    (result, report)
}

#[cfg(test)]
mod tests;