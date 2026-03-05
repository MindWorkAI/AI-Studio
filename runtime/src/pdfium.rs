use std::sync::Mutex;
use once_cell::sync::Lazy;
use pdfium_render::prelude::Pdfium;
use log::{error, warn};

pub static PDFIUM_LIB_PATH: Lazy<Mutex<Option<String>>> = Lazy::new(|| Mutex::new(None));

pub trait PdfiumInit {
    fn ai_studio_init() -> Result<Pdfium, Box<dyn std::error::Error + Send + Sync>>;
}

impl PdfiumInit for Pdfium {

    /// Initializes the PDFium library for AI Studio.
    fn ai_studio_init() -> Result<Pdfium, Box<dyn std::error::Error + Send + Sync>> {
        let lib_path = PDFIUM_LIB_PATH.lock().unwrap();
        if let Some(path) = lib_path.as_ref() {
            return match Pdfium::bind_to_library(Pdfium::pdfium_platform_library_name_at_path(path)) {
                Ok(binding) => Ok(Pdfium::new(binding)),
                Err(library_error) => {
                    match Pdfium::bind_to_system_library() {
                        Ok(binding) => Ok(Pdfium::new(binding)),
                        Err(system_error) => {
                            error!(
                                "Failed to load PDFium from '{path}' and the system library. Developer action (from repo root): run the build script once to download the required PDFium version: `cd app/Build` and `dotnet run build`. Details: library error: '{library_error}'; system error: '{system_error}'."
                            );

                            Err(Box::new(system_error))
                        }
                    }
                }
            }
        }

        warn!("No custom PDFium library path set; trying to load PDFium from the system library.");
        match Pdfium::bind_to_system_library() {
            Ok(binding) => Ok(Pdfium::new(binding)),
            Err(system_error) => {
                error!(
                    "Failed to load PDFium from the system library. Developer action (from repo root): run the build script once to download the required PDFium version: `cd app/Build` and `dotnet run build`. Details: '{system_error}'."
                );

                Err(Box::new(system_error))
            }
        }
    }
}
