use std::path::Path;
use std::io::{BufRead, BufReader};
use base64::{engine::general_purpose, Engine as _};
use calamine::{open_workbook_auto, Reader};
use file_format::{FileFormat, Kind};
use pdfium_render::prelude::Pdfium;
use std::error::Error;

use std::fs::File;
use std::io::Read;
use std::process::Command;
use rocket::post;
use rocket::response::stream::{Event, EventStream};
use rocket::{State, Shutdown};
use rocket::fs::{relative, FileServer};
use rocket::form::Form;
use rocket::serde::{Serialize, Deserialize};
use rocket::tokio::sync::broadcast::{channel, Sender, error::RecvError};
use rocket::tokio::select;

#[derive(Debug)]
pub struct Chunk {
    pub content: String,
    pub metadata: Metadata,
}

#[derive(Debug)]
pub enum Metadata {
    Text { line_number: usize },
    Pdf { page_number: usize },
    Spreadsheet { sheet_name: String, row_number: usize },
    Document,
    Image,
}

const TO_MARKDOWN: &str = "markdown";
const DOCX: &str = "docx";
const ODT: &str = "odt";

#[post("/system/file-data/extract", data = "<file_path>")]
pub async fn extract_file_data(
    file_path: String,
    queue: &State<Sender<ExtractEvent>>,
    mut shutdown: Shutdown
) -> EventStream![] {
    let mut rx = queue.subscribe();
    let path = file_path.clone();

    // Start extraction in a separate task
    let extractor = rocket::tokio::task::spawn_blocking(move || {
        stream_data(&path).map(|iter| {
            iter.map(|chunk| {
                chunk.map_err(|e| format!("Chunk error: {}", e))
            })
        })
    });

    EventStream! {
        let mut extraction_stream = match extractor.await {
            Ok(Ok(stream)) => stream,
            Ok(Err(e)) => {
                yield Event::json(&ExtractEvent::Error(e.to_string()));
                return;
            }
            Err(e) => {
                yield Event::json(&ExtractEvent::Error(format!("Task failed: {}", e)));
                return;
            }
        };

        loop {
            let chunk = select! {
                chunk = extraction_stream.next() => chunk,
                _ = &mut shutdown => break,
            };

            match chunk {
                Some(Ok(chunk)) => {
                    let event = ExtractEvent::Chunk {
                        content: chunk.content,
                        metadata: match chunk.metadata {
                            Metadata::Text { line_number } =>
                                MetadataRepr::Text { line_number },
                            Metadata::Pdf { page_number } =>
                                MetadataRepr::Pdf { page_number },
                         }
                    };
                    yield Event::json(&event);
                }
                Some(Err(e)) => {
                    yield Event::json(&ExtractEvent::Error(e));
                }
                None => break,
            }
        }

        yield Event::json(&ExtractEvent::Completed);
    }
}


// Serialisierbare Datentypen
#[derive(serde::Serialize)]
#[serde(tag = "type", content = "data")]
enum ExtractEvent {
    Chunk {
        content: String,
        metadata: MetadataRepr,
    },
    Error(String),
    Completed,
}

#[derive(serde::Serialize)]
#[serde(tag = "type")]
enum MetadataRepr {
    Text { line_number: usize },
    Pdf { page_number: usize },
    Spreadsheet { sheet_name: String, row_number: usize },
    Document,
    Image,
}


/// Streams the content of a file in chunks with format-specific metadata.
///
/// Takes a file path as input and returns a stream of chunks containing
/// content segments with associated metadata. Supports various file types
/// including documents, spreadsheets, presentations, images, and PDFs.
///
/// The streaming process works as follows:
/// - Verifies the file exists
/// - Detects the file format using content and extension
/// - Processes content incrementally based on format:
///   - Text files: Streams line by line with line numbers
///   - PDFs: Extracts text page by page with page numbers
///   - Spreadsheets: Outputs rows with sheet names and row numbers
///   - Office documents: Converts to Markdown as single chunk
///   - Images: Returns Base64 encoding as single chunk
///   - HTML files: Converts to Markdown as single chunk
///
/// # Parameters
/// - `file_path`: Path to the file to process (platform independent)
///
/// # Returns
/// Returns a `Result` containing:
/// - `Ok`: Boxed iterator yielding `Result<Chunk>` items
/// - `Err`: Initial processing error (e.g., file not found)
///
/// Each iterator item represents either:
/// - `Ok(Chunk)`: Content segment with metadata
/// - `Err`: Error during chunk processing
///
/// # Chunk Structure
/// - `content`: Text segment or Base64 image data
/// - `metadata`: Context information including:
///   - Line numbers for text files
///   - Page numbers for PDFs
///   - Sheet/row numbers for spreadsheets
///   - Document type markers for office formats
///   - Image type marker for images
///
/// # Errors
/// - Initial errors: File not found, format detection failures
/// - Chunk-level errors: Format-specific parsing errors
/// - Pandoc conversion failures for office documents
///
/// # Examples
/// ```
/// let chunk_stream = stream_data("data.txt")?;
/// for chunk_result in chunk_stream {
///     match chunk_result {
///         Ok(chunk) => {
///             println!("Metadata: {:?}", chunk.metadata);
///             println!("Content: {}", chunk.content);
///         }
///         Err(e) => eprintln!("Error: {}", e),
///     }
/// }
/// ```
fn stream_data(
    file_path: &str,
) -> Result<Box<dyn Iterator<Item = Result<Chunk, Box<dyn Error>>>>, Box<dyn Error>> {
    if !Path::new(file_path).exists() {
        return Err(Box::from("File does not exist."));
    }

    let fmt = FileFormat::from_file(file_path)?;
    let ext = file_path.split('.').last().unwrap_or("");

    match ext {
        DOCX | ODT => {
            let from = if ext == DOCX { "docx" } else { "odt" };
            convert_with_pandoc(file_path, from, TO_MARKDOWN)
        }
        "xlsx" | "ods" | "xls" | "xlsm" | "xlsb" | "xla" | "xlam" => {
            stream_spreadsheet_as_csv(file_path)
        }
        _ => match fmt.kind() {
            Kind::Document => match fmt {
                FileFormat::PortableDocumentFormat => read_pdf(file_path),
                FileFormat::MicrosoftWordDocument => {
                    convert_with_pandoc(file_path, "docx", TO_MARKDOWN)
                }
                FileFormat::OfficeOpenXmlDocument => {
                    convert_with_pandoc(file_path, fmt.extension(), TO_MARKDOWN)
                }
                _ => stream_text_file(file_path),
            },
            Kind::Ebook => Err(Box::from("Ebooks not yet supported")),
            Kind::Image => chunk_image(file_path),
            Kind::Other => match fmt {
                FileFormat::HypertextMarkupLanguage => {
                    convert_with_pandoc(file_path, fmt.extension(), TO_MARKDOWN)
                }
                _ => stream_text_file(file_path),
            },
            Kind::Presentation => match fmt {
                FileFormat::OfficeOpenXmlPresentation => {
                    convert_with_pandoc(file_path, fmt.extension(), TO_MARKDOWN)
                }
                _ => stream_text_file(file_path),
            },
            Kind::Spreadsheet => stream_spreadsheet_as_csv(file_path),
            _ => stream_text_file(file_path),
        },
    }
}

fn stream_text_file(file_path: &str) -> Result<Box<dyn Iterator<Item = Result<Chunk, Box<dyn Error>>>>, Box<dyn Error>> {
    let file = File::open(file_path)?;
    let reader = BufReader::new(file);
    let iter = reader.lines()
        .enumerate()
        .map(|(i, line)| {
            Ok(Chunk {
                content: line?,
                metadata: Metadata::Text { line_number: i + 1 },
            })
        });
    Ok(Box::new(iter))
}

fn read_pdf(file_path: &str) -> Result<Box<dyn Iterator<Item = Result<Chunk, Box<dyn Error>>>>, Box<dyn Error>> {
    let pdfium = Pdfium::default();
    let doc = pdfium.load_pdf_from_file(file_path, None)?;
    let pages = doc.pages();
    let chunks: Vec<_> = pages.iter()
        .enumerate()
        .map(|(i, page)| {
            let content = page.text()?.all();
            Ok(Chunk {
                content,
                metadata: Metadata::Pdf { page_number: i + 1 },
            })
        })
        .collect();
    Ok(Box::new(chunks.into_iter()))
}

fn stream_spreadsheet_as_csv(file_path: &str) -> Result<Box<dyn Iterator<Item = Result<Chunk, Box<dyn Error>>>>, Box<dyn Error>> {
    let mut workbook = open_workbook_auto(file_path)?;
    let mut chunks = Vec::new();

    for sheet_name in workbook.sheet_names() {
        let range = workbook.worksheet_range(&sheet_name)?;
        for (row_idx, row) in range.rows().enumerate() {
            let content = row.iter()
                .map(|cell| cell.to_string())
                .collect::<Vec<_>>()
                .join(",");
            chunks.push(Ok(Chunk {
                content,
                metadata: Metadata::Spreadsheet {
                    sheet_name: sheet_name.clone(),
                    row_number: row_idx + 1,
                },
            }));
        }
    }
    Ok(Box::new(chunks.into_iter()))
}

fn convert_with_pandoc(
    file_path: &str,
    from: &str,
    to: &str,
) -> Result<Box<dyn Iterator<Item = Result<Chunk, Box<dyn Error>>>>, Box<dyn Error>> {
    let output = Command::new("pandoc")
        .arg(file_path)
        .args(&["-f", from, "-t", to])
        .output()?;
    if output.status.success() {
        let content = String::from_utf8(output.stdout)?;
        Ok(Box::new(std::iter::once(Ok(Chunk {
            content,
            metadata: Metadata::Document,
        }))))
    } else {
        Err(Box::from(String::from_utf8_lossy(&output.stderr).into_owned()))
    }
}

fn read_img_as_base64(file_path: &str) -> Result<String, Box<dyn Error>> {
    let img_result = File::open(file_path);

    match img_result {
        Ok(mut img) => {
            let mut buff = Vec::new();
            img.read_to_end(&mut buff)?;

            let base64 = general_purpose::STANDARD.encode(&buff);
            Ok(base64)
        }
        Err(e) => Err(Box::from(format!("{}", e))),
    }
}

fn chunk_image(file_path: &str) -> Result<Box<dyn Iterator<Item = Result<Chunk, Box<dyn Error>>>>, Box<dyn Error>> {
    let base64 = read_img_as_base64(file_path)?;
    Ok(Box::new(std::iter::once(Ok(Chunk {
        content: base64,
        metadata: Metadata::Image,
    }))))
}
