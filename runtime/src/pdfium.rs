use std::sync::Mutex;
use once_cell::sync::Lazy;
use pdfium_render::prelude::Pdfium;

pub static PDFIUM_LIB_PATH: Lazy<Mutex<Option<String>>> = Lazy::new(|| Mutex::new(None));

pub trait PdfiumInit {
    fn ai_studio_init() -> Pdfium;
}

impl PdfiumInit for Pdfium {

    /// Initializes the PDFium library for AI Studio.
    fn ai_studio_init() -> Pdfium {
        let lib_path = PDFIUM_LIB_PATH.lock().unwrap();
        if let Some(path) = lib_path.as_ref() {
            return Pdfium::new(
                Pdfium::bind_to_library(Pdfium::pdfium_platform_library_name_at_path(path))
                    .or_else(|_| Pdfium::bind_to_system_library())
                    .unwrap(),
            );
        }

        Pdfium::new(Pdfium::bind_to_system_library().unwrap())
    }
}