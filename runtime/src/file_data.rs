use std::cmp::min;
use std::collections::VecDeque;
use std::convert::Infallible;
use crate::api_token::APIToken;
use crate::pandoc::PandocProcessBuilder;
use crate::pdfium::PdfiumInit;
use crate::prompt_injection::{Finding as PromptInjectionFinding, Sanitizer};
use async_stream::stream;
use axum::extract::Query;
use axum::extract::rejection::QueryRejection;
use axum::response::sse::{Event, Sse};
use base64::{engine::general_purpose, Engine as _};
use calamine::{open_workbook_auto, Error as CalamineError, Reader};
use chardetng::{EncodingDetector, Iso2022JpDetection, Utf8Detection};
use docx_to_md::{DocumentContainer, ImageHandlingMode as DocumentImageHandlingMode, Metadata as DocumentMetadata, ParserConfig as DocumentParserConfig};
use encoding_rs::Encoding;
use file_format::{FileFormat, Kind};
use futures::{Stream, StreamExt};
use pdfium_render::prelude::{Pdfium, PdfiumError, PdfiumInternalError};
use pptx_to_md::{DiagnosticSeverity, ImageHandlingMode, MarkdownOptions, ParserConfig, PresentationContainer, PresentationFormat, PresentationMetadata, ReadingOrder};
use serde::{Deserialize, Deserializer, Serialize};
use serde::de::{Error as SerdeError, Visitor};
use std::path::Path;
use std::pin::Pin;
use std::fmt;
use log::{debug, error, warn};
use tokio::io::AsyncReadExt;
use tokio::sync::mpsc;
use tokio_stream::wrappers::ReceiverStream;

#[derive(Debug, Serialize)]
pub struct Chunk {
    pub content: String,
    pub stream_id: String,
    pub metadata: Metadata,
}

impl Chunk {
    pub fn new(content: String, metadata: Metadata) -> Self {
        Chunk { content, stream_id: String::new(), metadata }
    }

    /// Creates a chunk which reports a failed extraction. Errors travel through the same
    /// schema as content chunks, so the .NET app is able to deserialize and surface them
    /// instead of silently treating a failure as empty file content.
    pub fn from_error(error: &ExtractionError) -> Self {
        Chunk {
            content: String::new(),
            stream_id: String::new(),
            metadata: Metadata::Error {
                code: error.code,
                message: error.message.clone(),
                page_number: error.page_number,
                detected_format: error.detected_format.clone(),
            },
        }
    }

    pub fn set_stream_id(&mut self, stream_id: &str) { self.stream_id = stream_id.to_string(); }

    /// Whether this chunk's content is prose a prompt injection could hide in.
    ///
    /// Image chunks carry base64 data, which must never reach the filter: it is not text, and
    /// the encoded-carrier scan would treat a photo as one enormous carrier. Chunks that only
    /// announce an error or an image carry nothing to filter either.
    fn carries_filterable_text(&self) -> bool {
        !matches!(
            self.metadata,
            Metadata::Image { .. }
                | Metadata::Error { .. }
                | Metadata::PromptInjection { .. }
                | Metadata::Document { image: Some(_), .. }
                | Metadata::Presentation { image: Some(_), .. }
        )
    }
}

#[derive(Debug, Serialize)]
pub enum Metadata {
    Text {
        line_number: usize
    },
    
    Pdf {
        page_number: usize
    },
    
    Spreadsheet {
        sheet_name: String,
        row_number: usize,
    },
    
    Document {
        page_number: Option<usize>,
        image: Option<Base64Image>,
    },

    Image {},
    
    Presentation {
        slide_number: u32,
        image: Option<Base64Image>,
    },

    Error {
        code: ExtractionErrorCode,
        message: String,
        page_number: Option<usize>,
        detected_format: Option<String>,
    },

    /// Reports that suspected prompt injections were filtered out of this document.
    ///
    /// This is a notice, not a failure: the document was read and the content around the
    /// filtered passages is intact. It travels as its own metadata variant rather than as an
    /// `ExtractionErrorCode`, because the app needs the findings themselves to tell the user
    /// what was removed, and a code carries no payload.
    PromptInjection {
        findings: Vec<PromptInjectionFinding>,

        /// How many passages were filtered. Can exceed the number of findings, which is
        /// capped, so the user still learns the true extent of the filtering.
        redacted_count: usize,
    },
}

/// Classifies why an extraction failed, so the .NET app can tell the user what happened
/// instead of showing an empty document.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize)]
#[serde(rename_all = "SCREAMING_SNAKE_CASE")]
pub enum ExtractionErrorCode {
    /// The request itself was malformed, e.g. a missing query parameter.
    InvalidRequest,

    FileNotFound,
    FileNotReadable,

    /// Another process holds the file open and denies us reading it.
    FileLocked,

    FormatDetectionFailed,
    NotAValidPdf,
    NotAValidSpreadsheet,
    PdfiumUnavailable,
    PdfEncrypted,
    PageExtractionFailed,
    NoTextExtracted,

    /// The content does not match the file extension. This is a notice, not a failure: we read
    /// the file according to its content and only tell the user about the wrong extension.
    ExtensionMismatch,

    /// The file was read as text, but its bytes are not text.
    NotTextContent,

    /// The file is an executable, no matter what its extension claims.
    ExecutableRejected,

    Unsupported,

    /// Any failure which does not carry a code of its own yet.
    Internal,
}

/// An extraction failure with a machine-readable code. It implements `std::error::Error`,
/// so it travels through the existing boxed error channel and `?` keeps working for the
/// error types of the underlying crates.
#[derive(Debug, Clone)]
pub struct ExtractionError {
    pub code: ExtractionErrorCode,
    pub message: String,
    pub page_number: Option<usize>,

    /// The format we identified by looking at the content, e.g. when it contradicts the file
    /// extension. The app names it so the user learns what the file really is.
    pub detected_format: Option<String>,
}

impl ExtractionError {
    pub fn new(code: ExtractionErrorCode, message: impl Into<String>) -> Self {
        Self { code, message: message.into(), page_number: None, detected_format: None }
    }

    pub fn on_page(code: ExtractionErrorCode, message: impl Into<String>, page_number: usize) -> Self {
        Self { code, message: message.into(), page_number: Some(page_number), detected_format: None }
    }

    /// Creates an error which names the format we identified by looking at the content.
    pub fn with_detected_format(code: ExtractionErrorCode, message: impl Into<String>, detected_format: &FileFormat) -> Self {
        Self { code, message: message.into(), page_number: None, detected_format: Some(detected_format.name().to_string()) }
    }

    /// Recovers the structured error from a boxed error. Errors which do not carry a code
    /// yet are reported as `Internal`, so every failure reaches the .NET app through the
    /// same schema.
    fn from_boxed(error: &(dyn std::error::Error + Send + Sync + 'static)) -> Self {
        match error.downcast_ref::<ExtractionError>() {
            Some(extraction_error) => extraction_error.clone(),
            None => Self::new(ExtractionErrorCode::Internal, error.to_string()),
        }
    }
}

impl fmt::Display for ExtractionError {
    fn fmt(&self, formatter: &mut fmt::Formatter) -> fmt::Result {
        match self.page_number {
            Some(page_number) => write!(formatter, "[{:?}] page {page_number}: {}", self.code, self.message),
            None => write!(formatter, "[{:?}] {}", self.code, self.message),
        }
    }
}

impl std::error::Error for ExtractionError {}

/// Detects whether a file system error means that another process holds the file open.
///
/// Windows answers with `ERROR_SHARING_VIOLATION` (32) or `ERROR_LOCK_VIOLATION` (33). This also
/// covers files on a network drive, because the SMB server enforces the lock and the client
/// surfaces the very same codes.
#[cfg(windows)]
fn is_locked_error(error: &std::io::Error) -> bool {
    matches!(error.raw_os_error(), Some(32) | Some(33))
}

/// Detects whether a file system error means that another process holds the file open.
///
/// Unix has no distinct error for this. A lock held through an SMB share surfaces as a permission
/// problem, which we cannot tell apart from an actual permission problem, so we never claim a file
/// is locked here.
#[cfg(not(windows))]
fn is_locked_error(_error: &std::io::Error) -> bool {
    false
}

/// Classifies a file system error, so a file which another program holds open is reported as such
/// instead of collapsing into a generic read failure.
fn classify_io_error(error: &std::io::Error) -> ExtractionErrorCode {
    if is_locked_error(error) {
        return ExtractionErrorCode::FileLocked;
    }

    match error.kind() {
        std::io::ErrorKind::NotFound => ExtractionErrorCode::FileNotFound,
        std::io::ErrorKind::InvalidData => ExtractionErrorCode::FormatDetectionFailed,
        _ => ExtractionErrorCode::FileNotReadable,
    }
}

#[derive(Debug, Serialize)]
pub struct Base64Image {
    pub id: String,
    pub content: String,
    pub segment: usize,
    pub is_end: bool,
    pub media_type: Option<String>,
}

impl Base64Image {
    fn new(id: String, content: String, segment: usize, is_end: bool, media_type: Option<String>) -> Self {
        Self { id, content, segment, is_end, media_type }
    }
}

const TO_MARKDOWN: &str = "markdown";

/// Pandoc's markup-free output format. We do not use it as content, only to find out whether a
/// conversion produced any readable text at all.
const PANDOC_PLAIN: &str = "plain";

const DOCX: &str = "docx";
const ODT: &str = "odt";
const HTML: &str = "html";
const IMAGE_SEGMENT_SIZE_IN_CHARS: usize = 8_192; // equivalent to ~ 5500 token

/// Every PDF file starts with this signature.
const PDF_MAGIC: &[u8] = b"%PDF-";

/// How many bytes we probe to verify the PDF signature. The few extra bytes beyond the
/// signature itself make the diagnostics useful when the signature does not match.
const PDF_HEADER_PROBE_SIZE: u64 = 8;

/// Last-resort payload used when even an error event cannot be serialized. It keeps the
/// chunk schema intact, so the .NET app never has to parse a bare string.
const FALLBACK_ERROR_EVENT_JSON: &str = r#"{"content":"","stream_id":"","metadata":{"Error":{"code":"INTERNAL","message":"The extraction error could not be serialized.","page_number":null,"detected_format":null}}}"#;

type Result<T> = std::result::Result<T, Box<dyn std::error::Error + Send + Sync>>;
type ChunkStream = Pin<Box<dyn Stream<Item = Result<Chunk>> + Send>>;

#[derive(Deserialize)]
pub struct ExtractDataQuery {
    path: String,
    stream_id: String,
    #[serde(deserialize_with = "deserialize_bool_case_insensitive")]
    extract_images: bool,

    /// Whether suspected prompt injections are filtered out of the content.
    ///
    /// Defaults to filtering when the app does not say: leaving it out must not be a way to
    /// receive unfiltered content by accident.
    #[serde(default = "filter_by_default", deserialize_with = "deserialize_bool_case_insensitive")]
    filter_prompt_injections: bool,
}

fn filter_by_default() -> bool {
    true
}

fn deserialize_bool_case_insensitive<'de, D>(deserializer: D) -> std::result::Result<bool, D::Error>
where
    D: Deserializer<'de>,
{
    struct BoolVisitor;

    impl<'de> Visitor<'de> for BoolVisitor {
        type Value = bool;

        fn expecting(&self, formatter: &mut fmt::Formatter) -> fmt::Result {
            formatter.write_str("a boolean value")
        }

        fn visit_bool<E>(self, value: bool) -> std::result::Result<Self::Value, E> {
            Ok(value)
        }

        fn visit_str<E>(self, value: &str) -> std::result::Result<Self::Value, E>
        where
            E: SerdeError,
        {
            match value.to_ascii_lowercase().as_str() {
                "true" | "1" => Ok(true),
                "false" | "0" => Ok(false),
                _ => Err(E::invalid_value(serde::de::Unexpected::Str(value), &self)),
            }
        }
    }

    deserializer.deserialize_any(BoolVisitor)
}

/// Reports an extraction failure as a schema-conformant SSE event, so the .NET app is able
/// to deserialize it like any other chunk.
fn error_event(error: &ExtractionError, stream_id: Option<&str>) -> Event {
    let mut chunk = Chunk::from_error(error);
    if let Some(stream_id) = stream_id {
        chunk.set_stream_id(stream_id);
    }

    Event::default().json_data(&chunk).unwrap_or_else(|serialization_error| {
        error!("Failed to serialize an extraction error event: {serialization_error}");
        Event::default().data(FALLBACK_ERROR_EVENT_JSON)
    })
}

/// Serializes a content chunk as an SSE event, reporting a serialization failure as an error
/// event rather than dropping the chunk silently.
fn content_event(chunk: &Chunk, stream_id: &str, path: &str) -> Event {
    Event::default().json_data(chunk).unwrap_or_else(|e| {
        error!("Failed to serialize a content chunk for '{path}': {e}");
        error_event(&ExtractionError::new(ExtractionErrorCode::Internal, format!("Failed to serialize a content chunk: {e}")), Some(stream_id))
    })
}

/// Pairs the sanitized texts back up with the chunks they came from.
///
/// The sanitizer holds chunks back until it has seen enough text to scan across their
/// boundaries, and releases them in order. Their metadata waited here in the meantime,
/// which is what keeps a page's text under its own page number.
fn take_released(held: &mut VecDeque<(u64, Chunk)>, released: Vec<(u64, String)>) -> Vec<Chunk> {
    let mut chunks = Vec::with_capacity(released.len());

    for (id, text) in released {
        let Some((held_id, mut chunk)) = held.pop_front() else {
            error!("The prompt-injection filter released a chunk that was never held: {id}.");
            continue;
        };

        debug_assert_eq!(held_id, id, "chunks must be released in the order they arrived");
        chunk.content = text;
        chunks.push(chunk);
    }

    chunks
}

/// Runs one step of the prompt-injection filter off the async worker.
///
/// The scan is synchronous CPU work sitting in the middle of the stream that serves the SSE
/// response, which is exactly what pdfium and the presentation reader are kept away from. How
/// long one step runs is not bounded by the batch size either: a text file is chunked by line,
/// so a minified JSON or a log without line breaks arrives as one chunk of the whole file and
/// is scanned in a single call. Yielding between steps would not help there; the step itself
/// has to leave the worker.
///
/// The sanitizer is the scan's state, so it travels into the blocking thread and back out.
///
/// Returns `None` when the scan thread died. The sanitizer died with it, and what it still
/// held cannot be released: nothing has checked that content.
async fn scan_off_worker<F>(holder: &mut Option<Sanitizer>, step: F) -> Option<Vec<(u64, String)>>
where
    F: FnOnce(&mut Sanitizer) -> Vec<(u64, String)> + Send + 'static,
{
    let mut sanitizer = holder.take()?;
    match tokio::task::spawn_blocking(move || {
        let released = step(&mut sanitizer);
        (sanitizer, released)
    }).await {
        Ok((sanitizer, released)) => {
            *holder = Some(sanitizer);
            Some(released)
        },

        Err(e) => {
            error!("The prompt-injection filter failed while scanning: {e}");
            None
        },
    }
}

/// Hands one chunk to the filter, keeping the scan off the async worker.
async fn scan_push(holder: &mut Option<Sanitizer>, id: u64, content: String) -> Option<Vec<(u64, String)>> {
    // Most pushes only add their chunk to the buffer. Moving those to another thread would
    // cost more than doing them here, so only the ones that scan make the trip.
    if holder.as_ref().is_some_and(|sanitizer| !sanitizer.will_scan(content.len())) {
        return holder.as_mut().map(|sanitizer| sanitizer.push(id, &content));
    }

    scan_off_worker(holder, move |sanitizer| sanitizer.push(id, &content)).await
}

/// The error the app sees when the filter itself failed.
///
/// Reported as a failure rather than as unfiltered content: the point of the filter is that
/// nothing reaches a model unchecked, and a document nobody checked is exactly what the app
/// must not receive.
fn filter_failed_error() -> ExtractionError {
    ExtractionError::new(
        ExtractionErrorCode::Internal,
        "The prompt-injection filter failed, so the content was not passed on unchecked.".to_string(),
    )
}

pub async fn extract_data(
    _token: APIToken,
    query: std::result::Result<Query<ExtractDataQuery>, QueryRejection>,
) -> Sse<impl Stream<Item = std::result::Result<Event, Infallible>>> {
    let query = match query {
        Ok(Query(query)) => Ok(query),
        Err(e) => {
            let message = format!("Invalid query for '/retrieval/fs/extract': {e}");
            warn!("{message}");
            Err(ExtractionError::new(ExtractionErrorCode::InvalidRequest, message))
        },
    };

    let stream = stream! {
        match query {
            Ok(query) => {
                let stream_result = stream_data(&query.path, query.extract_images, &query.stream_id).await;
                let id_ref = &query.stream_id;
                let path_ref = &query.path;

                match stream_result {
                    Ok(mut stream) => {
                        //
                        // Every chunk of every file format passes through here, which is why the
                        // prompt-injection filter sits at this point: it needs to see the document
                        // as a whole, and this is the one place where the whole document goes by.
                        //
                        let mut sanitizer = query.filter_prompt_injections.then(Sanitizer::new);
                        let mut held: VecDeque<(u64, Chunk)> = VecDeque::new();
                        let mut next_chunk_id = 0u64;

                        while let Some(chunk) = stream.next().await {
                            match chunk {
                                Ok(mut chunk) => {
                                    chunk.set_stream_id(id_ref);

                                    if sanitizer.is_none() {
                                        yield Ok(content_event(&chunk, id_ref, path_ref));
                                        continue;
                                    }

                                    //
                                    // Image data and error notices are passed on untouched. They
                                    // must still wait for the text ahead of them, or a page's
                                    // image would overtake the page it belongs to.
                                    //
                                    if !chunk.carries_filterable_text() {
                                        let Some(released_chunks) = scan_off_worker(&mut sanitizer, Sanitizer::flush).await else {
                                            yield Ok(error_event(&filter_failed_error(), Some(id_ref)));
                                            break;
                                        };

                                        for released in take_released(&mut held, released_chunks) {
                                            yield Ok(content_event(&released, id_ref, path_ref));
                                        }

                                        yield Ok(content_event(&chunk, id_ref, path_ref));
                                        continue;
                                    }

                                    let id = next_chunk_id;
                                    next_chunk_id += 1;

                                    let content = std::mem::take(&mut chunk.content);
                                    held.push_back((id, chunk));

                                    let Some(released_chunks) = scan_push(&mut sanitizer, id, content).await else {
                                        yield Ok(error_event(&filter_failed_error(), Some(id_ref)));
                                        break;
                                    };

                                    for released in take_released(&mut held, released_chunks) {
                                        yield Ok(content_event(&released, id_ref, path_ref));
                                    }
                                },

                                Err(e) => {
                                    let extraction_error = ExtractionError::from_boxed(e.as_ref());
                                    error!("Extraction failed for '{path_ref}': {extraction_error}");

                                    // Whatever was read before the failure is still content the
                                    // app may show, so it is released before the error. A filter
                                    // that failed on top of that releases nothing; the extraction
                                    // error below is reported either way.
                                    if let Some(released_chunks) = scan_off_worker(&mut sanitizer, Sanitizer::flush).await {
                                        for released in take_released(&mut held, released_chunks) {
                                            yield Ok(content_event(&released, id_ref, path_ref));
                                        }
                                    }

                                    yield Ok(error_event(&extraction_error, Some(id_ref)));
                                    break;
                                },
                            }
                        }

                        //
                        // A filter that is gone by now was either never switched on, or it
                        // failed and said so. Only a live one still holds content back.
                        //
                        if sanitizer.is_some() {
                            let Some(released_chunks) = scan_off_worker(&mut sanitizer, Sanitizer::flush).await else {
                                yield Ok(error_event(&filter_failed_error(), Some(id_ref)));
                                return;
                            };

                            for released in take_released(&mut held, released_chunks) {
                                yield Ok(content_event(&released, id_ref, path_ref));
                            }
                        }

                        if let Some(sanitizer) = sanitizer {
                            //
                            // Logged for every document, not only for a filtered one: a scan
                            // that is too slow leaves no other trace, and reproducing it means
                            // having the same document at hand again.
                            //
                            let (scanned_bytes, scan_duration) = sanitizer.scan_stats();
                            debug!(
                                "Scanned {mib:.2} MiB of '{path_ref}' for prompt injections in {ms} ms ({throughput:.2} MiB/s).",
                                mib = scanned_bytes as f64 / 1_048_576.0,
                                ms = scan_duration.as_millis(),
                                throughput = scanned_bytes as f64 / 1_048_576.0 / scan_duration.as_secs_f64().max(f64::EPSILON),
                            );

                            let report = sanitizer.into_report();
                            if !report.is_empty() {
                                warn!(
                                    "Filtered {count} suspected prompt injection(s) out of '{path_ref}': {rules:?}",
                                    count = report.redacted_count,
                                    rules = report.findings.iter().map(|finding| finding.rule_id.as_str()).collect::<Vec<_>>(),
                                );

                                let mut notice = Chunk::new(String::new(), Metadata::PromptInjection {
                                    findings: report.findings,
                                    redacted_count: report.redacted_count,
                                });

                                notice.set_stream_id(id_ref);
                                yield Ok(content_event(&notice, id_ref, path_ref));
                            }
                        }
                    },

                    Err(e) => {
                        let extraction_error = ExtractionError::from_boxed(e.as_ref());
                        error!("Could not start the extraction stream for '{path_ref}': {extraction_error}");
                        yield Ok(error_event(&extraction_error, Some(id_ref)));
                    }
                };
            },

            Err(extraction_error) => {
                yield Ok(error_event(&extraction_error, None));
            },
        }
    };

    Sse::new(stream)
}

/// How a file is read.
///
/// Deriving the route from the extension and from the content separately is what lets us notice
/// when the two disagree, instead of trusting a possibly wrong extension blindly.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum ExtractionRoute {
    Pdf,
    Docx,
    Odt,
    PandocHtml,
    PresentationPptx,
    PresentationOdp,
    Spreadsheet,
    Csv,
    Text,
    Image,

    /// The file is an executable and is never read.
    Executable,

    /// A format we recognize but have no reader for, e.g. the legacy binary Office formats.
    Unsupported,
}

/// Derives the route from the file extension.
fn route_from_extension(ext: &str) -> Option<ExtractionRoute> {
    match ext {
        "pdf" => Some(ExtractionRoute::Pdf),
        DOCX => Some(ExtractionRoute::Docx),
        ODT => Some(ExtractionRoute::Odt),
        HTML | "htm" => Some(ExtractionRoute::PandocHtml),
        "csv" | "tsv" => Some(ExtractionRoute::Csv),
        "pptx" => Some(ExtractionRoute::PresentationPptx),
        "odp" => Some(ExtractionRoute::PresentationOdp),
        "xlsx" | "ods" | "xls" | "xlsm" | "xlsb" | "xla" | "xlam" => Some(ExtractionRoute::Spreadsheet),
        "jpg" | "jpeg" | "png" | "gif" | "bmp" | "tiff" | "svg" | "webp" | "heic" => Some(ExtractionRoute::Image),

        //
        // Everything else claims nothing in particular. Text formats end up here on purpose:
        // their content cannot be identified beyond "this is text", so there is nothing to
        // contradict. Every extension which does have a reader must be listed above, otherwise
        // a correctly named file looks like a mismatch.
        //
        _ => None,
    }
}

/// Derives the route from the content we identified.
///
/// `None` means the content does not point at any particular reader. Such a file keeps whatever
/// its extension asks for, and the text reader decides whether the bytes are readable at all.
fn route_from_content(fmt: FileFormat) -> Option<ExtractionRoute> {
    match fmt {
        FileFormat::PortableDocumentFormat => Some(ExtractionRoute::Pdf),
        FileFormat::OfficeOpenXmlDocument => Some(ExtractionRoute::Docx),
        FileFormat::OpendocumentText => Some(ExtractionRoute::Odt),
        FileFormat::HypertextMarkupLanguage => Some(ExtractionRoute::PandocHtml),
        FileFormat::OfficeOpenXmlPresentation => Some(ExtractionRoute::PresentationPptx),
        FileFormat::OpendocumentPresentation => Some(ExtractionRoute::PresentationOdp),

        // Calamine reads the legacy binary spreadsheet format as well:
        FileFormat::OfficeOpenXmlSpreadsheet
        | FileFormat::OpendocumentSpreadsheet
        | FileFormat::MicrosoftExcelSpreadsheet => Some(ExtractionRoute::Spreadsheet),

        FileFormat::PlainText => Some(ExtractionRoute::Text),

        //
        // The legacy binary Word and PowerPoint formats have no reader here: pptx_to_md only
        // handles PPTX and ODP, and docx_to_md only reads the XML-based DOCX and ODT. Saying so
        // is better than handing the file to a reader which is bound to fail.
        //
        FileFormat::MicrosoftWordDocument | FileFormat::MicrosoftPowerpointPresentation => Some(ExtractionRoute::Unsupported),

        _ => match fmt.kind() {
            Kind::Executable => Some(ExtractionRoute::Executable),
            Kind::Image => Some(ExtractionRoute::Image),
            Kind::Ebook | Kind::Archive | Kind::Compressed => Some(ExtractionRoute::Unsupported),
            _ => None,
        },
    }
}

async fn stream_data(file_path: &str, extract_images: bool, stream_id: &str) -> Result<ChunkStream> {
    if !Path::new(file_path).exists() {
        error!("File does not exist: '{file_path}'");
        return Err(ExtractionError::new(ExtractionErrorCode::FileNotFound, format!("The file does not exist: '{file_path}'.")).into());
    }

    let file_path_clone = file_path.to_owned();
    let fmt = match FileFormat::from_file(&file_path_clone) {
        Ok(format) => format,
        Err(error) => {
            //
            // Detecting the format opens the file, so this is the first place a file which another
            // program holds open fails. Reporting that as a format problem would send the user
            // looking in the wrong direction, hence we classify the error instead.
            //
            let code = classify_io_error(&error);
            error!("Failed to read '{file_path}' while determining its file format ({code:?}): {error}");
            return Err(ExtractionError::new(code, format!("The file could not be read: {error}")).into());
        },
    };

    let ext = Path::new(file_path)
        .extension()
        .and_then(|extension| extension.to_str())
        .map(str::to_ascii_lowercase)
        .unwrap_or_default();
    
    // The size is part of the diagnostics: a truncated or not-yet-available file on a network
    // share is what tells a broken extraction apart from a document without text.
    let file_size = match tokio::fs::metadata(file_path).await {
        Ok(metadata) => format!("{} bytes", metadata.len()),
        Err(error) => format!("unknown size ({error})"),
    };

    debug!("Extracting data from file: '{file_path}', {file_size}, format: '{fmt:?}', extension: '{ext}'");

    let extension_route = route_from_extension(ext.as_str());
    let content_route = route_from_content(fmt);

    //
    // The content decides whenever it points at a specific reader and contradicts the extension.
    // `Text` is excluded on purpose: it is the least specific answer, and letting it win would
    // cost a `.csv` its CSV fence. When the content says nothing, the extension keeps its say and
    // the text reader decides whether the bytes are readable at all.
    //
    let content_is_specific = matches!(content_route, Some(route) if route != ExtractionRoute::Text);
    let content_contradicts_extension = content_is_specific && content_route != extension_route;

    let route = match (extension_route, content_route) {
        _ if content_contradicts_extension => content_route.unwrap(),
        (Some(from_extension), _) => from_extension,
        (None, Some(from_content)) => from_content,
        (None, None) => ExtractionRoute::Text,
    };

    debug!("Reading '{file_path}' via {route:?} (extension: {extension_route:?}, content: {content_route:?}).");

    match route {
        ExtractionRoute::Executable => {
            error!("Refused to read '{file_path}': its content is an executable ({name}).", name = fmt.name());
            return Err(ExtractionError::with_detected_format(
                ExtractionErrorCode::ExecutableRejected,
                format!("The file is an executable ({name}), which is never read.", name = fmt.name()),
                &fmt,
            ).into());
        },

        ExtractionRoute::Unsupported => {
            return Err(ExtractionError::with_detected_format(
                ExtractionErrorCode::Unsupported,
                format!("The format '{name}' is not supported.", name = fmt.name()),
                &fmt,
            ).into());
        },

        ExtractionRoute::Image if !extract_images => {
            return Err(ExtractionError::new(ExtractionErrorCode::Unsupported, "Image extraction is disabled.").into());
        },

        _ => {},
    }

    let stream = match route {
        ExtractionRoute::Pdf => stream_pdf(file_path).await?,
        ExtractionRoute::Docx | ExtractionRoute::Odt => stream_document(file_path, extract_images, stream_id).await?,
        ExtractionRoute::PandocHtml => convert_with_pandoc(file_path, HTML, TO_MARKDOWN).await?,
        ExtractionRoute::PresentationPptx => stream_presentation(file_path, extract_images, PresentationFormat::Pptx).await?,
        ExtractionRoute::PresentationOdp => stream_presentation(file_path, extract_images, PresentationFormat::Odp).await?,
        ExtractionRoute::Spreadsheet => stream_spreadsheet_as_csv(file_path).await?,
        ExtractionRoute::Csv => stream_text_file(file_path, true, Some("csv".to_string())).await?,
        ExtractionRoute::Text => stream_text_file(file_path, false, None).await?,
        ExtractionRoute::Image => chunk_image(file_path).await?,

        // Handled above, before any reader was chosen:
        ExtractionRoute::Executable | ExtractionRoute::Unsupported => unreachable!(),
    };

    //
    // The file was readable, but not as its extension claims. We prepend a notice so the user
    // learns what the file really is, while the content itself is read correctly.
    //
    if content_contradicts_extension {
        warn!("The content of '{file_path}' is '{name}', which does not match its extension '{ext}'.", name = fmt.name());

        let notice = Chunk::from_error(&ExtractionError::with_detected_format(
            ExtractionErrorCode::ExtensionMismatch,
            format!("The content is '{name}', which does not match the file extension '{ext}'.", name = fmt.name()),
            &fmt,
        ));

        let notice_stream = stream! { yield Ok(notice); };
        return Ok(Box::pin(notice_stream.chain(stream)));
    }

    Ok(Box::pin(stream))
}

/// How many bytes we inspect for NUL bytes to tell binary content from text.
const BINARY_PROBE_SIZE: usize = 8_192;

/// Reads a text file and decodes it, no matter which encoding it uses.
///
/// Insisting on UTF-8 is not enough in practice: text files written on Windows are frequently
/// encoded in Windows-1252, where umlauts are single bytes which UTF-8 rejects. Such a file used
/// to look like it was not text at all.
async fn read_text_file(file_path: &str) -> Result<String> {
    let bytes = tokio::fs::read(file_path).await.map_err(|error| ExtractionError::new(
        classify_io_error(&error),
        format!("The file could not be read: {error}"),
    ))?;

    //
    // A byte order mark is authoritative and also covers UTF-16, which the detector below does not
    // recognize. We therefore check it first and let `decode` act on it.
    //
    if let Some((encoding, _)) = Encoding::for_bom(&bytes) {
        let (text, _, _) = encoding.decode(&bytes);
        debug!("Decoded '{file_path}' as {name}, chosen by its byte order mark.", name = encoding.name());
        return Ok(text.into_owned());
    }

    //
    // Without a byte order mark, every byte sequence decodes into *something*, so the decoder can
    // no longer tell us that a file is binary. NUL bytes do: they do not occur in text, and after
    // the check above no UTF-16 file can reach this point.
    //
    let probe_length = min(bytes.len(), BINARY_PROBE_SIZE);
    if bytes[..probe_length].contains(&0) {
        return Err(ExtractionError::new(
            ExtractionErrorCode::NotTextContent,
            "The file contains binary data and is not a text file.",
        ).into());
    }

    //
    // Both options are about untrusted web content which may run scripts, which is not what we
    // read here: these are local files the user picked, so allowing both guesses gives the better
    // detection.
    //
    let mut detector = EncodingDetector::new(Iso2022JpDetection::Allow);
    detector.feed(&bytes, true);

    let (text, encoding, had_errors) = detector.guess(None, Utf8Detection::Allow).decode(&bytes);
    if had_errors {
        warn!("Decoding '{file_path}' as {name} replaced malformed sequences.", name = encoding.name());
    } else {
        debug!("Decoded '{file_path}' as {name}.", name = encoding.name());
    }

    Ok(text.into_owned())
}

async fn stream_text_file(file_path: &str, use_md_fences: bool, fence_language: Option<String>) -> Result<ChunkStream> {
    let text = read_text_file(file_path).await?;
    let mut line_number = 0;

    let stream = stream! {

        if use_md_fences {
            match fence_language {
                Some(lang) if lang.trim().is_empty() => {
                    yield Ok(Chunk::new("```".to_string(), Metadata::Text { line_number }));
                },

                Some(lang) => {
                    yield Ok(Chunk::new(format!("```{}", lang.trim()), Metadata::Text { line_number }));
                },

                None => {
                    yield Ok(Chunk::new("```".to_string(), Metadata::Text { line_number }));
                }
            };
        }

        for line in text.lines() {
            line_number += 1;
            yield Ok(Chunk::new(
                line.to_string(),
                Metadata::Text { line_number }
            ));
        }

        if use_md_fences {
            yield Ok(Chunk::new("```\n".to_string(), Metadata::Text { line_number }));
        }
    };

    Ok(Box::pin(stream))
}

/// Verifies the file really is a PDF before handing it to PDFium. Without this check, a file
/// which only carries the `.pdf` extension, or whose bytes are not available, would end up in
/// the text branch and silently produce empty content.
async fn ensure_pdf_header(file_path: &str) -> Result<()> {
    let file = tokio::fs::File::open(file_path).await.map_err(|error| ExtractionError::new(
        classify_io_error(&error),
        format!("The file could not be opened: {error}"),
    ))?;

    let file_size = file.metadata().await.map_err(|error| ExtractionError::new(
        classify_io_error(&error),
        format!("The file size could not be read: {error}"),
    ))?.len();

    let mut header = Vec::with_capacity(PDF_HEADER_PROBE_SIZE as usize);
    file.take(PDF_HEADER_PROBE_SIZE).read_to_end(&mut header).await.map_err(|error| ExtractionError::new(
        classify_io_error(&error),
        format!("The first bytes of the file could not be read: {error}"),
    ))?;

    if header.starts_with(PDF_MAGIC) {
        return Ok(());
    }

    let header_hex = header.iter().map(|byte| format!("{byte:02x}")).collect::<Vec<_>>().join(" ");
    error!("The file '{file_path}' does not start with the PDF signature; size: {file_size} bytes, first bytes: [{header_hex}].");

    Err(ExtractionError::new(
        ExtractionErrorCode::NotAValidPdf,
        format!("The file does not start with the PDF signature. Size: {file_size} bytes, first bytes: [{header_hex}]."),
    ).into())
}

/// Classifies why PDFium refused to open a document, so the cause reaches the user instead of
/// collapsing into a generic failure.
fn classify_pdf_load_error(error: &PdfiumError) -> ExtractionError {
    let code = match error {
        PdfiumError::PdfiumLibraryInternalError(internal_error) => match internal_error {
            // The document is encrypted or its security settings forbid access:
            PdfiumInternalError::PasswordError | PdfiumInternalError::SecurityError => ExtractionErrorCode::PdfEncrypted,

            // Pdfium could not read the file itself, e.g. because a network share went away:
            PdfiumInternalError::FileError => ExtractionErrorCode::FileNotReadable,

            _ => ExtractionErrorCode::NotAValidPdf,
        },

        _ => ExtractionErrorCode::NotAValidPdf,
    };

    ExtractionError::new(code, format!("The PDF could not be opened: {error}"))
}

async fn stream_pdf(file_path: &str) -> Result<ChunkStream> {
    ensure_pdf_header(file_path).await?;

    let path = file_path.to_owned();
    let (tx, rx) = mpsc::channel(10);

    tokio::task::spawn_blocking(move || {
        let pdfium = match Pdfium::ai_studio_init() {
            Ok(pdfium) => pdfium,
            Err(e) => {
                let _ = tx.blocking_send(Err(ExtractionError::new(
                    ExtractionErrorCode::PdfiumUnavailable,
                    format!("The PDF engine could not be initialized: {e}"),
                ).into()));
                return;
            }
        };
        let doc = match pdfium.load_pdf_from_file(&path, None) {
            Ok(document) => document,
            Err(e) => {
                let _ = tx.blocking_send(Err(classify_pdf_load_error(&e).into()));
                return;
            }
        };

        let mut number_of_pages = 0;
        let mut number_of_characters = 0;
        let mut number_of_failed_pages = 0;
        let mut receiver_gone = false;

        for (num_page, page) in doc.pages().iter().enumerate() {
            let page_number = num_page + 1;
            number_of_pages = page_number;

            let content = match page.text().map(|t| t.all()) {
                Ok(text_content) => text_content,
                Err(e) => {
                    //
                    // A single unreadable page must not end the document: we report it as a
                    // non-fatal error chunk and continue with the next page. Sending it as an
                    // `Err` would stop the consumer and silently truncate everything after it.
                    //
                    number_of_failed_pages += 1;
                    warn!("The text of page {page_number} of '{path}' could not be extracted: {e}");

                    if tx.blocking_send(Ok(Chunk::from_error(&ExtractionError::on_page(
                        ExtractionErrorCode::PageExtractionFailed,
                        format!("The text of page {page_number} could not be extracted: {e}"),
                        page_number,
                    )))).is_err() {
                        receiver_gone = true;
                        break;
                    }

                    continue;
                }
            };

            number_of_characters += content.chars().count();

            if tx.blocking_send(Ok(Chunk::new(
                content,
                Metadata::Pdf { page_number }
            ))).is_err() {
                receiver_gone = true;
                break;
            }
        }

        if receiver_gone {
            debug!("The consumer stopped reading the PDF stream of '{path}' after {number_of_pages} page(s).");
            return;
        }

        debug!("Extracted {number_of_characters} character(s) from {number_of_pages} page(s) of '{path}'; failed pages: {number_of_failed_pages}.");

        //
        // Without this marker, a PDF without a text layer and a broken extraction both arrive as
        // an empty document, and the AI would answer as if the file had no content at all.
        //
        if number_of_characters == 0 {
            warn!("No text could be extracted from '{path}': {number_of_pages} page(s), {number_of_failed_pages} failed page(s). The PDF may consist of scanned images without a text layer.");

            let _ = tx.blocking_send(Ok(Chunk::from_error(&ExtractionError::new(
                ExtractionErrorCode::NoTextExtracted,
                format!("No text could be extracted from {number_of_pages} page(s). The PDF may consist of scanned images without a text layer."),
            ))));
        }
    });

    Ok(Box::pin(ReceiverStream::new(rx)))
}

/// Classifies a spreadsheet failure, so an unreadable file, e.g. on a network share which went
/// away, is not reported as a corrupt workbook.
fn classify_spreadsheet_error_code(error: &CalamineError) -> ExtractionErrorCode {
    match error {
        CalamineError::Io(io_error) => classify_io_error(io_error),
        _ => ExtractionErrorCode::NotAValidSpreadsheet,
    }
}

async fn stream_spreadsheet_as_csv(file_path: &str) -> Result<ChunkStream> {
    let path = file_path.to_owned();
    let (tx, rx) = mpsc::channel(10);

    tokio::task::spawn_blocking(move || {
        let mut workbook = match open_workbook_auto(&path) {
            Ok(w) => w,
            Err(e) => {
                let _ = tx.blocking_send(Err(ExtractionError::new(
                    classify_spreadsheet_error_code(&e),
                    format!("The spreadsheet could not be opened: {e}"),
                ).into()));
                return;
            }
        };

        for sheet_name in workbook.sheet_names() {
            let range = match workbook.worksheet_range(&sheet_name) {
                Ok(r) => r,
                Err(e) => {
                    //
                    // One unreadable sheet must not end the workbook: we report it as a non-fatal
                    // error chunk and continue with the next sheet. Sending it as an `Err` would
                    // stop the consumer and silently drop all remaining sheets.
                    //
                    warn!("The sheet '{sheet_name}' of '{path}' could not be read: {e}");

                    if tx.blocking_send(Ok(Chunk::from_error(&ExtractionError::new(
                        classify_spreadsheet_error_code(&e),
                        format!("The sheet '{sheet_name}' could not be read: {e}"),
                    )))).is_err() {
                        return;
                    }

                    continue;
                }
            };

            let mut row_idx = 0;
            tx.blocking_send(Ok(Chunk::new(
                "```csv".to_string(),
                Metadata::Spreadsheet {
                    sheet_name: sheet_name.clone(),
                    row_number: row_idx,
                }
            ))).ok();
            
            for row in range.rows() {
                row_idx += 1;
                let content = row.iter()
                    .map(|cell| cell.to_string())
                    .collect::<Vec<_>>()
                    .join(",");

                if tx.blocking_send(Ok(Chunk::new(
                    content,
                    Metadata::Spreadsheet {
                        sheet_name: sheet_name.clone(),
                        row_number: row_idx,
                    }
                ))).is_err() {
                    return;
                }
            }

            tx.blocking_send(Ok(Chunk::new(
                "```".to_string(),
                Metadata::Spreadsheet {
                    sheet_name: sheet_name.clone(),
                    row_number: row_idx,
                }
            ))).ok();
        }
    });

    Ok(Box::pin(ReceiverStream::new(rx)))
}

async fn convert_with_pandoc(
    file_path: &str,
    from: &str,
    to: &str,
) -> Result<ChunkStream> {
    let output = PandocProcessBuilder::new()
        .with_input_file(file_path)
        .with_input_format(from)
        .with_output_format(to)
        .build()
        .command.output().await?;

    let exit_code = output.status.code();
    let stderr_text = String::from_utf8_lossy(&output.stderr).trim().to_string();
    debug!("Pandoc converted '{file_path}' from '{from}' to '{to}': exit={exit_code:?}, {stdout_length} byte(s) of output.", stdout_length = output.stdout.len());

    if !stderr_text.is_empty() {
        warn!("Pandoc reported while converting '{file_path}': {stderr_text}");
    }

    if !output.status.success() {
        return Err(ExtractionError::new(
            ExtractionErrorCode::Internal,
            format!("Pandoc failed with exit code {exit_code:?}: {stderr_text}"),
        ).into());
    }

    let content = String::from_utf8(output.stdout).map_err(|e| ExtractionError::new(
        ExtractionErrorCode::Internal,
        format!("The output of Pandoc was not valid UTF-8: {e}"),
    ))?;

    //
    // Pandoc succeeded, yet nothing came out. Passing that on as content would hand an empty
    // document to the AI, which is exactly what this whole path must not do.
    //
    if content.trim().is_empty() || !pandoc_found_readable_text(file_path, from, &content).await {
        return Err(ExtractionError::new(
            ExtractionErrorCode::NoTextExtracted,
            format!("Pandoc read the file without finding any readable text{separator}{stderr_text}", separator = if stderr_text.is_empty() { "." } else { ": " }),
        ).into());
    }

    let stream = stream! {
        yield Ok(Chunk::new(
            content,
            Metadata::Document {
                page_number: None,
                image: None,
            }
        ));
    };

    Ok(Box::pin(stream))
}

/// Decides whether a conversion produced actual text rather than just structure.
///
/// HTML is the one input where markup can masquerade as content: a page which builds its text with
/// scripts converts into nothing but fenced divs and class names. That looks like content, yet it
/// says nothing, and the AI would be asked to work with it. Pandoc's plain output settles the
/// question, because it carries no markup at all. Documents such as `.docx` carry their text
/// statically, so the check above is enough for them and they are spared the extra conversion.
async fn pandoc_found_readable_text(file_path: &str, from: &str, content: &str) -> bool {
    if from != HTML {
        return true;
    }

    let output = PandocProcessBuilder::new()
        .with_input_file(file_path)
        .with_input_format(from)
        .with_output_format(PANDOC_PLAIN)
        .build()
        .command.output().await;

    match output {
        Ok(output) if output.status.success() => {
            let has_text = !String::from_utf8_lossy(&output.stdout).trim().is_empty();
            if !has_text {
                warn!("'{file_path}' converted into {length} character(s) of pure structure without any readable text.", length = content.trim().len());
            }

            has_text
        },

        //
        // We could not find out, so we do not claim the file is empty. The content we already have
        // is the better answer than an error we cannot justify.
        //
        Ok(output) => {
            warn!("Could not check '{file_path}' for readable text, Pandoc exited with {code:?}.", code = output.status.code());
            true
        },

        Err(e) => {
            warn!("Could not check '{file_path}' for readable text: {e}");
            true
        },
    }
}

async fn chunk_image(file_path: &str) -> Result<ChunkStream> {
    let data = tokio::fs::read(file_path).await?;
    let base64 = general_purpose::STANDARD.encode(&data);

    let stream = stream! {
        yield Ok(Chunk::new(
            base64,
            Metadata::Image {},
        ));
    };

    Ok(Box::pin(stream))
}

async fn stream_document(file_path: &str, extract_images: bool, stream_id: &str) -> Result<ChunkStream> {
    let path = Path::new(file_path).to_owned();
    let stream_id = stream_id.to_owned();
    let parser_config = DocumentParserConfig::builder()
        .extract_images(extract_images)
        .compress_images(true)
        .quality(75)
        .image_handling_mode(DocumentImageHandlingMode::Manually)
        .include_document_metadata(true)
        .include_headers_footers(true)
        .include_footnotes(true)
        .include_endnotes(true)
        .include_comments(true)
        .include_page_number_as_comment(false)
        .build();
    let (tx, rx) = mpsc::channel(32);
    let worker_error_tx = tx.clone();

    // Page iteration performs synchronous ZIP/XML work and image compression,
    // so the complete producer must stay outside Tokio's asynchronous workers.
    let worker = tokio::task::spawn_blocking(move || {
        //
        // Failures travel through the error channel, which logs them with the file path and the
        // classified code once they arrive. Logging them here as well would only duplicate that.
        //
        let document = match DocumentContainer::open(&path, parser_config) {
            Ok(document) => document,
            Err(e) => {
                let _ = tx.blocking_send(Err(ExtractionError::new(
                    ExtractionErrorCode::FileNotReadable,
                    format!("The document could not be read: {e}"),
                ).into()));
                return;
            },
        };
        let mut metadata_md = document_metadata_to_markdown(document.metadata());
        let pages = match document.iter_pages() {
            Ok(pages) => pages,
            Err(e) => {
                let _ = tx.blocking_send(Err(ExtractionError::new(
                    ExtractionErrorCode::FileNotReadable,
                    format!("The pages of the document could not be read: {e}"),
                ).into()));
                return;
            },
        };

        let mut number_of_pages = 0;
        let mut number_of_characters = 0;

        //
        // A failing page ends the whole document here, unlike a PDF page: the page iterator gives
        // up for good once it hit an error, so everything behind that page is lost as well. This
        // is why neither failure below reports `PageExtractionFailed`. That code means that a
        // single page is missing while the rest stays usable, and the app would hand the truncated
        // document to the AI on those grounds.
        //
        for page_result in pages {
            let page = match page_result {
                Ok(page) => page,
                Err(e) => {
                    let _ = tx.blocking_send(Err(ExtractionError::new(
                        ExtractionErrorCode::Internal,
                        format!("A page of the document could not be read: {e}"),
                    ).into()));
                    return;
                },
            };
            let mut content = match page.to_markdown() {
                Ok(content) => content,
                Err(e) => {
                    let _ = tx.blocking_send(Err(ExtractionError::new(
                        ExtractionErrorCode::Internal,
                        format!("Page {page_number} of the document could not be converted: {e}", page_number = page.page_number),
                    ).into()));
                    return;
                },
            };

            number_of_pages = page.page_number;
            number_of_characters += content.chars().count();

            if let Some(metadata) = metadata_md.take() {
                content = format!("{metadata}\n\n{content}");
            }
            if tx.blocking_send(Ok(Chunk::new(content, Metadata::Document {
                page_number: Some(page.page_number),
                image: None,
            }))).is_err() {
                return;
            }

            for image in page.images.values() {
                let base64_data = image.base64();
                let image_id = format!("{stream_id}-{}-{}", page.page_number, image.id);
                let mut offset = 0;
                let mut segment_index = 0;
                while offset < base64_data.len() {
                    let end = min(offset + IMAGE_SEGMENT_SIZE_IN_CHARS, base64_data.len());
                    let base64_image = Base64Image::new(image_id.clone(), base64_data[offset..end].to_string(), segment_index, end == base64_data.len(), Some(image.media_type.clone()));
                    if tx.blocking_send(Ok(Chunk::new(String::new(), Metadata::Document {
                        page_number: Some(page.page_number),
                        image: Some(base64_image),
                    }))).is_err() {
                        return;
                    }
                    offset = end;
                    segment_index += 1;
                }
            }
        }

        debug!("Extracted {number_of_characters} character(s) from {number_of_pages} page(s) of '{path}'.", path = path.display());

        //
        // Without this marker, a document without any text and a broken extraction both arrive as
        // an empty document, and the AI would answer as if the file had no content at all.
        //
        if number_of_characters == 0 {
            warn!("No text could be extracted from '{path}': {number_of_pages} page(s).", path = path.display());

            let _ = tx.blocking_send(Ok(Chunk::from_error(&ExtractionError::new(
                ExtractionErrorCode::NoTextExtracted,
                format!("No text could be extracted from {number_of_pages} page(s) of the document."),
            ))));
        }
    });

    tokio::spawn(async move {
        if let Err(e) = worker.await {
            let _ = worker_error_tx.send(Err(ExtractionError::new(
                ExtractionErrorCode::Internal,
                format!("The document parser task failed: {e}"),
            ).into())).await;
        }
    });

    Ok(Box::pin(ReceiverStream::new(rx)))
}

async fn stream_presentation(file_path: &str, extract_images: bool, format: PresentationFormat) -> Result<ChunkStream> {
    let path = Path::new(file_path).to_owned();

    let parser_config = ParserConfig::builder()
        .extract_images(extract_images)
        .compress_images(true)
        .quality(75)
        .image_handling_mode(ImageHandlingMode::Manually)
        .include_presentation_metadata(true)
        .build();

    let markdown_options = MarkdownOptions {
        reading_order: ReadingOrder::Spatial,
        include_slide_number_as_comment: true,
        include_speaker_notes: true,
        include_comments: true,
        render_unsupported_comments: true,
    };

    let mut streamer = tokio::task::spawn_blocking(move || {
        PresentationContainer::open_as(&path, parser_config, format).map_err(|e| Box::new(e) as Box<dyn std::error::Error + Send + Sync>)
    }).await??;

    let (tx, rx) = mpsc::channel(32);
    let worker_error_tx = tx.clone();

    // Slide iteration performs synchronous ZIP/XML work and image compression,
    // so the complete producer must stay outside Tokio's asynchronous workers.
    let worker = tokio::task::spawn_blocking(move || {
        let mut metadata_md = presentation_metadata_to_markdown(streamer.metadata());

        for slide_result in streamer.iter_slides() {
            let slide = match slide_result {
                Ok(slide) => slide,
                Err(e) => {
                    let _ = tx.blocking_send(Err(Box::new(e) as Box<dyn std::error::Error + Send + Sync>));
                    return;
                },
            };

            for diagnostic in &slide.diagnostics {
                let source = diagnostic.source.as_deref().unwrap_or("presentation");
                match diagnostic.severity {
                    DiagnosticSeverity::Warning => warn!(
                        "Presentation slide {} warning in '{}': {}",
                        slide.slide_number,
                        source,
                        diagnostic.message
                    ),
                    DiagnosticSeverity::Error => error!(
                        "Presentation slide {} error in '{}': {}",
                        slide.slide_number,
                        source,
                        diagnostic.message
                    ),
                }
            }

            let mut content = match slide.to_markdown(&markdown_options) {
                Ok(content) => content,
                Err(e) => {
                    let _ = tx.blocking_send(Err(Box::new(e) as Box<dyn std::error::Error + Send + Sync>));
                    return;
                },
            };

            if let Some(metadata) = metadata_md.take() {
                content = format!("{metadata}\n\n{content}");
            }

            let chunk = Chunk::new(
                content,
                Metadata::Presentation {
                    slide_number: slide.slide_number,
                    image: None,
                }
            );

            if tx.blocking_send(Ok(chunk)).is_err() {
                return;
            }

            if let Some(images) = slide.load_images_manually() {
                for image in images.iter() {
                    let base64_data = &image.base64_content;
                    let total_length = base64_data.len();
                    let mut offset = 0;
                    let mut segment_index = 0;

                    while offset < total_length {
                        let end = min(offset + IMAGE_SEGMENT_SIZE_IN_CHARS, total_length);
                        let segment_content = &base64_data[offset..end];
                        let is_end = end == total_length;

                        let base64_image = Base64Image::new(
                        image.img_ref.id.clone(),
                        segment_content.to_string(),
                        segment_index,
                        is_end,
                        None,
                        );

                        let chunk = Chunk::new(
                            String::new(),
                            Metadata::Presentation {
                                slide_number: slide.slide_number,
                                image: Some(base64_image),
                            }
                        );

                        if tx.blocking_send(Ok(chunk)).is_err() {
                            return;
                        }

                        offset = end;
                        segment_index += 1;
                    }
                }
            }
        }
    });

    tokio::spawn(async move {
        if let Err(e) = worker.await {
            let _ = worker_error_tx.send(Err(format!("Presentation parser task failed: {e}").into())).await;
        }
    });

    Ok(Box::pin(ReceiverStream::new(rx)))
}

fn presentation_metadata_to_markdown(metadata: &PresentationMetadata) -> Option<String> {
    let mut fields = Vec::new();
    push_presentation_metadata_field(&mut fields, "Title", metadata.title.as_deref());
    push_presentation_metadata_field(&mut fields, "Author", metadata.author.as_deref());
    push_presentation_metadata_field(&mut fields, "Last Modified By", metadata.last_modified_by.as_deref());
    push_presentation_metadata_field(&mut fields, "Subject", metadata.subject.as_deref());
    push_presentation_metadata_field(&mut fields, "Description", metadata.description.as_deref());
    if !metadata.keywords.is_empty() {
        fields.push(format!(
            "Keywords: {}",
            sanitize_presentation_metadata_value(&metadata.keywords.join("; "))
        ));
    }
    push_presentation_metadata_field(&mut fields, "Created", metadata.created_at.as_deref());
    push_presentation_metadata_field(&mut fields, "Modified", metadata.modified_at.as_deref());

    if fields.is_empty() {
        None
    } else {
        Some(format!(
            "<!-- Presentation Metadata\n{}\n-->",
            fields.join("\n")
        ))
    }
}

fn document_metadata_to_markdown(metadata: &DocumentMetadata) -> Option<String> {
    let mut fields = Vec::new();
    push_presentation_metadata_field(&mut fields, "Title", metadata.title.as_deref());
    push_presentation_metadata_field(&mut fields, "Subject", metadata.subject.as_deref());
    push_presentation_metadata_field(&mut fields, "Author", metadata.author.as_deref());
    push_presentation_metadata_field(&mut fields, "Last Modified By", metadata.last_modified_by.as_deref());
    push_presentation_metadata_field(&mut fields, "Description", metadata.description.as_deref());
    if !metadata.keywords.is_empty() {
        fields.push(format!("Keywords: {}", sanitize_presentation_metadata_value(&metadata.keywords.join("; "))));
    }
    push_presentation_metadata_field(&mut fields, "Created", metadata.created_at.as_deref());
    push_presentation_metadata_field(&mut fields, "Modified", metadata.modified_at.as_deref());
    for (name, value) in &metadata.custom {
        fields.push(format!("Custom {name}: {}", sanitize_presentation_metadata_value(value)));
    }
    if fields.is_empty() { None } else { Some(format!("<!-- Document Metadata\n{}\n-->", fields.join("\n"))) }
}

fn push_presentation_metadata_field(fields: &mut Vec<String>, label: &str, value: Option<&str>) {
    if let Some(value) = value {
        fields.push(format!(
            "{label}: {}",
            sanitize_presentation_metadata_value(value)
        ));
    }
}

fn sanitize_presentation_metadata_value(value: &str) -> String {
    value
        .split_whitespace()
        .collect::<Vec<_>>()
        .join(" ")
        .replace("--", "&#45;&#45;")
}

#[cfg(test)]
mod tests {
    use super::*;

    /// Base64 image data must never reach the prompt-injection filter. It is not prose, and
    /// the filter's encoded-carrier scan would treat a photo as one enormous carrier and
    /// replace it with a marker, destroying the image.
    #[test]
    fn image_chunks_are_kept_away_from_the_filter() {
        let image = Chunk::new("iVBORw0KGgo".to_string(), Metadata::Image {});
        assert!(!image.carries_filterable_text());

        let base64_image = Base64Image::new("id".to_string(), "data".to_string(), 0, true, None);
        let slide_image = Chunk::new(String::new(), Metadata::Presentation {
            slide_number: 1,
            image: Some(base64_image),
        });

        assert!(!slide_image.carries_filterable_text());
    }

    #[test]
    fn text_chunks_go_through_the_filter() {
        let page = Chunk::new("Some page text.".to_string(), Metadata::Pdf { page_number: 1 });
        assert!(page.carries_filterable_text());

        let line = Chunk::new("Some line.".to_string(), Metadata::Text { line_number: 1 });
        assert!(line.carries_filterable_text());

        let row = Chunk::new("a,b,c".to_string(), Metadata::Spreadsheet {
            sheet_name: "Sheet1".to_string(),
            row_number: 1,
        });

        assert!(row.carries_filterable_text());
    }

    /// A slide's Markdown is text even though the same metadata variant also carries images.
    #[test]
    fn slide_text_without_an_image_goes_through_the_filter() {
        let slide = Chunk::new("# Slide title".to_string(), Metadata::Presentation {
            slide_number: 1,
            image: None,
        });

        assert!(slide.carries_filterable_text());
    }

    /// Notices are generated by the runtime itself and would only be scanned in circles.
    #[test]
    fn notices_are_kept_away_from_the_filter() {
        let error = Chunk::from_error(&ExtractionError::new(ExtractionErrorCode::Internal, "failed"));
        assert!(!error.carries_filterable_text());

        let notice = Chunk::new(String::new(), Metadata::PromptInjection {
            findings: Vec::new(),
            redacted_count: 1,
        });

        assert!(!notice.carries_filterable_text());
    }

    /// Dumps the text pdfium extracts from a PDF, so the prompt-injection throughput test can
    /// measure the scan against a real document instead of synthetic prose.
    ///
    /// Ignored by default: it needs a PDF, the pdfium library, and minutes rather than
    /// milliseconds. Run it as
    ///
    /// ```text
    /// AI_STUDIO_DUMP_PDF=/path/to/document.pdf \
    /// AI_STUDIO_DUMP_OUT=/path/to/corpus.txt \
    /// cargo test dump_pdf_text -- --ignored --nocapture
    /// ```
    ///
    /// The pages are separated by a record separator rather than a newline, so the throughput
    /// test can split them back into exactly the chunks the sanitizer sees in production. A
    /// newline would be indistinguishable from the ones inside a page.
    #[tokio::test]
    #[ignore]
    async fn dump_pdf_text() {
        let source = std::env::var("AI_STUDIO_DUMP_PDF").expect("set AI_STUDIO_DUMP_PDF to the PDF to dump");
        let target = std::env::var("AI_STUDIO_DUMP_OUT").expect("set AI_STUDIO_DUMP_OUT to the file to write");

        // The library ships next to the runtime and is not on the loader path during a test:
        let library_directory = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("resources/libraries");
        *crate::pdfium::PDFIUM_LIB_PATH.lock().unwrap() = Some(library_directory.to_string_lossy().to_string());

        let mut stream = stream_pdf(&source).await.expect("the PDF must be readable");
        let mut pages = Vec::new();
        let mut failed_pages = 0;

        while let Some(chunk) = stream.next().await {
            let chunk = chunk.expect("no page may fail the whole document");
            match chunk.metadata {
                Metadata::Pdf { .. } => pages.push(chunk.content),
                _ => failed_pages += 1,
            }
        }

        let dump = pages.join("\u{1E}");
        std::fs::write(&target, &dump).expect("the dump must be writable");

        println!(
            "Dumped {pages} page(s) ({bytes} bytes, {failed} non-text chunk(s)) from '{source}' to '{target}'.",
            pages = pages.len(),
            bytes = dump.len(),
            failed = failed_pages,
        );

        assert!(!pages.is_empty(), "the PDF produced no text pages");
    }

    /// Moving the scan to a blocking thread must not change what the filter releases: the same
    /// chunks under the same ids in the same order, whether a push scanned here or elsewhere.
    #[tokio::test]
    async fn moving_the_scan_off_the_worker_changes_nothing() {
        let pages: Vec<String> = (0..40)
            .map(|index| format!("Page {index}: {}", "ordinary prose about mixing consoles. ".repeat(20)))
            .collect();

        let mut direct = Sanitizer::new();
        let mut expected = Vec::new();
        for (id, page) in pages.iter().enumerate() {
            expected.extend(direct.push(id as u64, page));
        }

        expected.extend(direct.flush());

        let mut holder = Some(Sanitizer::new());
        let mut moved = Vec::new();
        for (id, page) in pages.iter().enumerate() {
            moved.extend(scan_push(&mut holder, id as u64, page.clone()).await.expect("the filter must survive a push"));
        }

        moved.extend(scan_off_worker(&mut holder, Sanitizer::flush).await.expect("the filter must survive the flush"));

        assert_eq!(moved, expected);
        assert!(!moved.is_empty(), "the pages must come back out");
    }

    /// Without a filter there is no scan to move, and no failure to report either.
    #[tokio::test]
    async fn scanning_without_a_filter_reports_nothing_to_release() {
        let mut holder: Option<Sanitizer> = None;

        assert!(scan_push(&mut holder, 0, "some text".to_string()).await.is_none());
        assert!(scan_off_worker(&mut holder, Sanitizer::flush).await.is_none());
    }
}