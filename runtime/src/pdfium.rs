use std::error::Error;
use std::sync::Mutex;
use once_cell::sync::{Lazy, OnceCell};
use pdfium_render::prelude::Pdfium;
use log::{error, info, warn};

pub static PDFIUM_LIB_PATH: Lazy<Mutex<Option<String>>> = Lazy::new(|| Mutex::new(None));
static PDFIUM: OnceCell<Pdfium> = OnceCell::new();

/// Grants one caller at a time the right to talk to PDFium.
static PDFIUM_ACCESS: Mutex<()> = Mutex::new(());

/// Runs the given action with PDFium all to itself.
///
/// PDFium is not thread-safe, and nothing else guarantees that for us: the `thread_safe` feature of
/// `pdfium-render` has only granted `Send` and `Sync` since its release 0.9.0 and no longer locks
/// anything, although its documentation still says so. Two documents read at the same time -- a
/// chat attachment while a data source is being indexed, say -- therefore corrupt PDFium's memory
/// and take the whole runtime down with a segmentation fault.
///
/// Every call to PDFium belongs in here, and so does everything holding a page or a document open:
/// closing them calls PDFium as well. What does not belong in here is anything that waits, our own
/// work on the extracted text above all, because everybody else waits along with it.
pub fn with_pdfium_access<T>(action: impl FnOnce() -> T) -> T {
    //
    // A panic while reading a document poisons this lock. Refusing every PDF from then on would
    // turn one broken document into a broken feature, so we take the lock either way: what the
    // panic left behind is inside PDFium, not inside the unit value we guard with.
    //
    let _access = PDFIUM_ACCESS.lock().unwrap_or_else(|poisoned| poisoned.into_inner());
    action()
}

pub trait PdfiumInit {
    fn ai_studio_init() -> Result<&'static Pdfium, Box<dyn Error + Send + Sync>>;
}

impl PdfiumInit for Pdfium {

    /// Initializes the PDFium library for AI Studio.
    fn ai_studio_init() -> Result<&'static Pdfium, Box<dyn Error + Send + Sync>> {
        PDFIUM.get_or_try_init(|| load_pdfium().map_err(|error| {
            Box::new(std::io::Error::other(error)) as Box<dyn Error + Send + Sync>
        }))
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
