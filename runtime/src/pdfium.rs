use std::error::Error;
use std::sync::Mutex;
use once_cell::sync::Lazy;
use pdfium_render::prelude::Pdfium;
use log::{error, info, warn};

pub static PDFIUM_LIB_PATH: Lazy<Mutex<Option<String>>> = Lazy::new(|| Mutex::new(None));
static PDFIUM: Lazy<Mutex<Option<Pdfium>>> = Lazy::new(|| Mutex::new(None));

pub trait PdfiumInit {
    fn ai_studio_init() -> Result<Pdfium, Box<dyn Error + Send + Sync>>;
}

impl PdfiumInit for Pdfium {

    /// Initializes the PDFium library for AI Studio.
    fn ai_studio_init() -> Result<Pdfium, Box<dyn Error + Send + Sync>> {
        let mut pdfium = PDFIUM.lock().unwrap();
        if let Some(pdfium) = pdfium.as_ref() {
            return Ok(pdfium.clone());
        }

        let loaded_pdfium = load_pdfium().map_err(|error| {
            Box::new(std::io::Error::other(error)) as Box<dyn Error + Send + Sync>
        })?;
        *pdfium = Some(loaded_pdfium.clone());

        Ok(loaded_pdfium)
    }
}

fn load_pdfium() -> Result<Pdfium, String> {
    let lib_path = PDFIUM_LIB_PATH.lock().unwrap().clone();
    if let Some(path) = lib_path.as_ref() {
        let pdfium_library_path = Pdfium::pdfium_platform_library_name_at_path(path);

        return match Pdfium::bind_to_library(&pdfium_library_path) {
            Ok(binding) => {
                info!("Loaded PDFium from '{path}'.", path = pdfium_library_path.to_string_lossy());
                Ok(Pdfium::new(binding))
            },
            Err(library_error) => {
                match Pdfium::bind_to_system_library() {
                    Ok(binding) => {
                        info!(
                            "Loaded PDFium from the system library after failing to load '{path}'.",
                            path = pdfium_library_path.to_string_lossy(),
                        );
                        Ok(Pdfium::new(binding))
                    },
                    Err(system_error) => {
                        let error_message = format!(
                            "Failed to load PDFium from '{path}' and the system library. Developer action (from repo root): run the build script once to download the required PDFium version: `cd app/Build` and `dotnet run build`. Details: library error: '{library_error}'; system error: '{system_error}'."
                        );

                        error!("{error_message}");
                        Err(error_message)
                    }
                }
            }
        }
    }

    warn!("No custom PDFium library path set; trying to load PDFium from the system library.");
    match Pdfium::bind_to_system_library() {
        Ok(binding) => {
            info!("Loaded PDFium from the system library.");
            Ok(Pdfium::new(binding))
        },
        Err(system_error) => {
            let error_message = format!(
                "Failed to load PDFium from the system library. Developer action (from repo root): run the build script once to download the required PDFium version: `cd app/Build` and `dotnet run build`. Details: '{system_error}'."
            );

            error!("{error_message}");
            Err(error_message)
        }
    }
}