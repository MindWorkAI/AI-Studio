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
use calamine::{open_workbook_auto, Reader};
use file_format::{FileFormat, Kind};
use futures::{Stream, StreamExt};
use pdfium_render::prelude::Pdfium;
use pptx_to_md::{DiagnosticSeverity, ImageHandlingMode, MarkdownOptions, ParserConfig, PresentationContainer, PresentationFormat, PresentationMetadata, ReadingOrder};
use serde::{Deserialize, Deserializer, Serialize};
use serde::de::{Error as SerdeError, Visitor};
use std::path::Path;
use std::pin::Pin;
use std::fmt;
use log::{debug, error, warn};
use tokio::io::AsyncBufReadExt;
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

pub async fn extract_data(
    _token: APIToken,
    query: std::result::Result<Query<ExtractDataQuery>, QueryRejection>,
) -> Sse<impl Stream<Item = std::result::Result<Event, Infallible>>> {
    let query = match query {
        Ok(Query(query)) => Ok(query),
        Err(e) => {
            let message = format!("Invalid query for '/retrieval/fs/extract': {e}");
            warn!("{message}");
            Err(message)
        },
    };

    let stream = stream! {
        match query {
            Ok(query) => {
                let stream_result = stream_data(&query.path, query.extract_images).await;
                let id_ref = &query.stream_id;

                match stream_result {
                    Ok(mut stream) => {
                        while let Some(chunk) = stream.next().await {
                            match chunk {
                                Ok(mut chunk) => {
                                    chunk.set_stream_id(id_ref);
                                    yield Ok(Event::default().json_data(&chunk).unwrap_or_else(|e| Event::default().data(format!("Error: {e}"))));
                                },

                                Err(e) => {
                                    yield Ok(Event::default().json_data(format!("Error: {e}")).unwrap_or_else(|_| Event::default().data(format!("Error: {e}"))));
                                    break;
                                },
                            }
                        }
                    },

                    Err(e) => {
                        yield Ok(Event::default().json_data(format!("Error starting stream: {e}")).unwrap_or_else(|_| Event::default().data(format!("Error starting stream: {e}"))));
                    }
                };
            },

            Err(e) => {
                yield Ok(Event::default().json_data(format!("Error starting stream: {e}")).unwrap_or_else(|_| Event::default().data(format!("Error starting stream: {e}"))));
            },
        }
    };

    Sse::new(stream)
}

async fn stream_data(file_path: &str, extract_images: bool) -> Result<ChunkStream> {
    if !Path::new(file_path).exists() {
        error!("File does not exist: '{file_path}'");
        return Err("File does not exist.".into());
    }

    let file_path_clone = file_path.to_owned();
    let fmt = match FileFormat::from_file(&file_path_clone) {
        Ok(format) => format,
        Err(error) => {
            error!("Failed to determine file format for '{file_path}': {error}");
            return Err(format!("Failed to determine file format for '{file_path}': {error}").into());
        },
    };

    let ext = Path::new(file_path)
        .extension()
        .and_then(|extension| extension.to_str())
        .map(str::to_ascii_lowercase)
        .unwrap_or_default();
    debug!("Extracting data from file: '{file_path}', format: '{fmt:?}', extension: '{ext}'");

    let stream = match ext.as_str() {
        DOCX | ODT => {
            let from = if ext == DOCX { "docx" } else { "odt" };
            convert_with_pandoc(file_path, from, TO_MARKDOWN).await?
        }

        "csv" | "tsv" => {
            stream_text_file(file_path, true, Some("csv".to_string())).await?
        },

        "pptx" => stream_presentation(file_path, extract_images, PresentationFormat::Pptx).await?,
        "odp" => stream_presentation(file_path, extract_images, PresentationFormat::Odp).await?,
        
        "xlsx" | "ods" | "xls" | "xlsm" | "xlsb" | "xla" | "xlam" => {
            stream_spreadsheet_as_csv(file_path).await?
        }
        
        _ => match fmt.kind() {
            Kind::Document => match fmt {
                FileFormat::PortableDocumentFormat => stream_pdf(file_path).await?,

                FileFormat::MicrosoftWordDocument => {
                    convert_with_pandoc(file_path, "docx", TO_MARKDOWN).await?
                },

                FileFormat::OfficeOpenXmlDocument => {
                    convert_with_pandoc(file_path, fmt.extension(), TO_MARKDOWN).await?
                },

                _ => stream_text_file(file_path, false, None).await?,
            },
            
            Kind::Ebook => return Err("Ebooks not yet supported".into()),

            Kind::Image => {
                if !extract_images {
                    return Err("Image extraction is disabled.".into());
                }

                chunk_image(file_path).await?
            },
            
            Kind::Other => match fmt {
                FileFormat::HypertextMarkupLanguage => {
                    convert_with_pandoc(file_path, fmt.extension(), TO_MARKDOWN).await?
                },

                _ => stream_text_file(file_path, false, None).await?,
            },
            
            Kind::Presentation => match fmt {
                FileFormat::OfficeOpenXmlPresentation => {
                    stream_presentation(file_path, extract_images, PresentationFormat::Pptx).await?
                },
                FileFormat::OpendocumentPresentation => {
                    stream_presentation(file_path, extract_images, PresentationFormat::Odp).await?
                }

                _ => stream_text_file(file_path, false, None).await?,
            },
            
            Kind::Spreadsheet => stream_spreadsheet_as_csv(file_path).await?,

            _ => stream_text_file(file_path, false, None).await?,
        },
    };

    Ok(Box::pin(stream))
}

async fn stream_text_file(file_path: &str, use_md_fences: bool, fence_language: Option<String>) -> Result<ChunkStream> {
    let file = tokio::fs::File::open(file_path).await?;
    let reader = tokio::io::BufReader::new(file);
    let mut lines = reader.lines();
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

        while let Ok(Some(line)) = lines.next_line().await {
            line_number += 1;
            yield Ok(Chunk::new(
                line,
                Metadata::Text { line_number }
            ));
        }

        if use_md_fences {
            yield Ok(Chunk::new("```\n".to_string(), Metadata::Text { line_number }));
        }
    };

    Ok(Box::pin(stream))
}

async fn stream_pdf(file_path: &str) -> Result<ChunkStream> {
    let path = file_path.to_owned();
    let (tx, rx) = mpsc::channel(10);

    tokio::task::spawn_blocking(move || {
        let pdfium = match Pdfium::ai_studio_init() {
            Ok(pdfium) => pdfium,
            Err(e) => {
                let _ = tx.blocking_send(Err(e));
                return;
            }
        };
        let doc = match pdfium.load_pdf_from_file(&path, None) {
            Ok(document) => document,
            Err(e) => {
                let _ = tx.blocking_send(Err(e.into()));
                return;
            }
        };

        for (num_page, page) in doc.pages().iter().enumerate() {
            let content = match page.text().map(|t| t.all()) {
                Ok(text_content) => text_content,
                Err(e) => {
                    let _ = tx.blocking_send(Err(e.into()));
                    continue;
                }
            };

            if tx.blocking_send(Ok(Chunk::new(
                content, 
                Metadata::Pdf { page_number: num_page + 1 }
            ))).is_err() {
                break;
            }
        }
    });

    Ok(Box::pin(ReceiverStream::new(rx)))
}

async fn stream_spreadsheet_as_csv(file_path: &str) -> Result<ChunkStream> {
    let path = file_path.to_owned();
    let (tx, rx) = mpsc::channel(10);

    tokio::task::spawn_blocking(move || {
        let mut workbook = match open_workbook_auto(&path) {
            Ok(w) => w,
            Err(e) => {
                let _ = tx.blocking_send(Err(e.into()));
                return;
            }
        };

        for sheet_name in workbook.sheet_names() {
            let range = match workbook.worksheet_range(&sheet_name) {
                Ok(r) => r,
                Err(e) => {
                    let _ = tx.blocking_send(Err(e.into()));
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
