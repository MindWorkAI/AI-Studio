use std::cmp::min;
use std::convert::Infallible;
use crate::api_token::APIToken;
use crate::pandoc::PandocProcessBuilder;
use crate::pdfium::PdfiumInit;
use async_stream::stream;
use axum::extract::Query;
use axum::extract::rejection::QueryRejection;
use axum::response::sse::{Event, Sse};
use base64::{engine::general_purpose, Engine as _};
use calamine::{open_workbook_auto, Error as CalamineError, Reader};
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
use tokio::io::{AsyncBufReadExt, AsyncReadExt};
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
    
    Document {},
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
    pub is_end: bool
}

impl Base64Image {
    fn new(id: String, content: String, segment: usize, is_end: bool) -> Self {
        Self { id, content, segment, is_end }
    }
}

const TO_MARKDOWN: &str = "markdown";
const DOCX: &str = "docx";
const ODT: &str = "odt";
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
                let stream_result = stream_data(&query.path, query.extract_images).await;
                let id_ref = &query.stream_id;
                let path_ref = &query.path;

                match stream_result {
                    Ok(mut stream) => {
                        while let Some(chunk) = stream.next().await {
                            match chunk {
                                Ok(mut chunk) => {
                                    chunk.set_stream_id(id_ref);
                                    yield Ok(Event::default().json_data(&chunk).unwrap_or_else(|e| {
                                        error!("Failed to serialize a content chunk for '{path_ref}': {e}");
                                        error_event(&ExtractionError::new(ExtractionErrorCode::Internal, format!("Failed to serialize a content chunk: {e}")), Some(id_ref))
                                    }));
                                },

                                Err(e) => {
                                    let extraction_error = ExtractionError::from_boxed(e.as_ref());
                                    error!("Extraction failed for '{path_ref}': {extraction_error}");
                                    yield Ok(error_event(&extraction_error, Some(id_ref)));
                                    break;
                                },
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
    PandocDocx,
    PandocOdt,
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
        DOCX => Some(ExtractionRoute::PandocDocx),
        ODT => Some(ExtractionRoute::PandocOdt),
        "html" | "htm" => Some(ExtractionRoute::PandocHtml),
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
        FileFormat::OfficeOpenXmlDocument => Some(ExtractionRoute::PandocDocx),
        FileFormat::OpendocumentText => Some(ExtractionRoute::PandocOdt),
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
        // handles PPTX and ODP, and Pandoc cannot read the binary .doc format at all. Saying so
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

async fn stream_data(file_path: &str, extract_images: bool) -> Result<ChunkStream> {
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
        ExtractionRoute::PandocDocx => convert_with_pandoc(file_path, DOCX, TO_MARKDOWN).await?,
        ExtractionRoute::PandocOdt => convert_with_pandoc(file_path, ODT, TO_MARKDOWN).await?,
        ExtractionRoute::PandocHtml => convert_with_pandoc(file_path, "html", TO_MARKDOWN).await?,
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

async fn stream_text_file(file_path: &str, use_md_fences: bool, fence_language: Option<String>) -> Result<ChunkStream> {
    let file = tokio::fs::File::open(file_path).await?;
    let reader = tokio::io::BufReader::new(file);
    let mut lines = reader.lines();
    let mut line_number = 0;

    // The stream outlives this call, so it needs its own copy of the path for diagnostics:
    let path = file_path.to_owned();

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

        loop {
            match lines.next_line().await {
                Ok(Some(line)) => {
                    line_number += 1;
                    yield Ok(Chunk::new(
                        line,
                        Metadata::Text { line_number }
                    ));
                },

                Ok(None) => break,

                //
                // Reading a line fails when the bytes are not valid UTF-8, which means the file is
                // not text at all. Treating that like the end of the file, as this loop did before,
                // turned every binary file into an empty document without a word.
                //
                Err(e) => {
                    let code = if e.kind() == std::io::ErrorKind::InvalidData {
                        ExtractionErrorCode::NotTextContent
                    } else {
                        classify_io_error(&e)
                    };

                    error!("Reading '{path}' as text failed after {line_number} line(s) ({code:?}): {e}");
                    yield Err(ExtractionError::new(code, format!("The file could not be read as text: {e}")).into());
                    break;
                },
            }
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
    
    let stream = stream! {
        if output.status.success() {
            match String::from_utf8(output.stdout.clone()) {
                Ok(content) => yield Ok(Chunk::new(
                    content,
                    Metadata::Document {}
                )),
                Err(e) => yield Err(e.into()),
            }
        } else {
            yield Err(format!(
                "Pandoc error: {}",
                String::from_utf8_lossy(&output.stderr)
            ).into());
        }
    };

    Ok(Box::pin(stream))
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
                            is_end
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
