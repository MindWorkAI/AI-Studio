use log::{error, info, warn};
use axum::extract::Query;
use axum::Json;
use file_format::FileFormat;
use serde::{Deserialize, Serialize};
use std::path::{Path, PathBuf};
use tauri_plugin_dialog::{DialogExt, FileDialogBuilder};
use crate::api_token::APIToken;
use crate::app_window::MAIN_WINDOW;
use crate::file_data::is_executable_content;

#[cfg(any(windows, target_os = "macos"))]
use std::process::Command;

#[cfg(target_os = "linux")]
use ashpd::desktop::open_uri::{OpenDirectoryRequest, OpenFileRequest};

#[cfg(windows)]
use std::os::windows::process::CommandExt;

/// Microsoft documents CREATE_NO_WINDOW as a process creation flag with value 0x08000000.
#[cfg(windows)]
const CREATE_NO_WINDOW: u32 = 0x08000000;

#[derive(Clone, Deserialize)]
pub struct PreviousDirectory {
    path: String,
}

#[derive(Deserialize)]
pub struct SelectDirectoryQuery {
    title: String,
}

#[derive(Clone, Deserialize)]
pub struct FileTypeFilter {
    filter_name: String,
    filter_extensions: Vec<String>,
}

#[derive(Clone, Deserialize)]
pub struct SelectFileOptions {
    title: String,
    previous_file: Option<PreviousFile>,
    filter: Option<FileTypeFilter>,
}

#[derive(Clone, Deserialize)]
pub struct SaveFileOptions {
    title: String,
    previous_file: Option<PreviousFile>,
    filter: Option<FileTypeFilter>,
}

#[derive(Clone, Deserialize)]
pub struct OpenPathOptions {
    path: String,
}

#[derive(Clone, Deserialize)]
pub struct OpenDocumentOptions {
    path: String,

    /// The page to show, counted from one, or `None` when the document has no page to show.
    page: Option<u32>,
}

#[derive(Serialize)]
pub struct DirectorySelectionResponse {
    user_cancelled: bool,
    selected_directory: String,
}

#[derive(Serialize)]
pub struct FileSelectionResponse {
    user_cancelled: bool,
    selected_file_path: String,
}

#[derive(Serialize)]
pub struct FilesSelectionResponse {
    user_cancelled: bool,
    selected_file_paths: Vec<String>,
}

#[derive(Serialize)]
pub struct FileSaveResponse {
    user_cancelled: bool,
    save_file_path: String,
}

#[derive(Serialize)]
pub struct OpenPathResponse {
    success: bool,
    issue: String,
}

#[derive(Serialize)]
pub struct OpenDocumentResponse {
    success: bool,

    /// Whether the document was handed to a program together with the page it should show.
    ///
    /// False means the document opens on its first page: no page was asked for, the system uses a
    /// program we cannot tell a page, or the attempt to start that program failed. None of these
    /// is an error — the document opens either way — so the app only notes it in its log.
    page_applied: bool,

    issue: String,
}

#[derive(Clone, Deserialize)]
pub struct PreviousFile {
    file_path: String,
}

/// Let the user select a directory.
pub async fn select_directory(
    _token: APIToken,
    Query(query): Query<SelectDirectoryQuery>,
    previous_directory: Option<Json<PreviousDirectory>>,
) -> Json<DirectorySelectionResponse> {
    let main_window_lock = MAIN_WINDOW.lock().unwrap();
    let main_window = match main_window_lock.as_ref() {
        Some(window) => window,
        None => {
            error!(Source = "Tauri"; "Cannot open directory dialog: main window not available.");
            return Json(DirectorySelectionResponse {
                user_cancelled: true,
                selected_directory: String::from(""),
            });
        }
    };

    let mut dialog = main_window.dialog().file().set_parent(main_window).set_title(&query.title);
    if let Some(previous) = previous_directory {
        dialog = dialog.set_directory(previous.path.clone());
    }

    drop(main_window_lock);

    let folder_path = dialog.blocking_pick_folder();
    match folder_path {
        Some(path) => {
            match path.into_path() {
                Ok(pb) => {
                    info!("User selected directory: {pb:?}");
                    Json(DirectorySelectionResponse {
                        user_cancelled: false,
                        selected_directory: pb.to_string_lossy().to_string(),
                    })
                }
                Err(e) => {
                    error!(Source = "Tauri"; "Failed to convert directory path: {e}");
                    Json(DirectorySelectionResponse {
                        user_cancelled: true,
                        selected_directory: String::new(),
                    })
                }
            }
        },

        None => {
            info!("User cancelled directory selection.");
            Json(DirectorySelectionResponse {
                user_cancelled: true,
                selected_directory: String::from(""),
            })
        },
    }
}

/// Let the user select a file.
pub async fn select_file(
    _token: APIToken,
    payload: Json<SelectFileOptions>,
) -> Json<FileSelectionResponse> {
    // Create a new file dialog builder:
    let file_dialog = MAIN_WINDOW
        .lock()
        .unwrap()
        .as_ref()
        .map(|w| w.dialog().file().set_parent(w).set_title(&payload.title));

    let Some(mut file_dialog) = file_dialog else {
        error!(Source = "Tauri"; "Cannot open file dialog: main window not available.");
        return Json(FileSelectionResponse {
            user_cancelled: true,
            selected_file_path: String::from(""),
        });
    };

    // Set the file type filter if provided:
    file_dialog = apply_filter(file_dialog, &payload.filter);

    // Set the previous file path if provided:
    if let Some(previous) = &payload.previous_file {
        let previous_path = previous.file_path.as_str();
        file_dialog = file_dialog.set_directory(previous_path);
    }

    // Show the file dialog and get the selected file path:
    let file_path = file_dialog.blocking_pick_file();
    match file_path {
        Some(path) => match path.into_path() {
            Ok(pb) => {
                info!("User selected file: {pb:?}");
                Json(FileSelectionResponse {
                    user_cancelled: false,
                    selected_file_path: pb.to_string_lossy().to_string(),
                })
            }
            Err(e) => {
                error!(Source = "Tauri"; "Failed to convert file path: {e}");
                Json(FileSelectionResponse {
                    user_cancelled: true,
                    selected_file_path: String::new(),
                })
            }
        },

        None => {
            info!("User cancelled file selection.");
            Json(FileSelectionResponse {
                user_cancelled: true,
                selected_file_path: String::from(""),
            })
        },
    }
}

/// Let the user select some files.
pub async fn select_files(
    _token: APIToken,
    payload: Json<SelectFileOptions>,
) -> Json<FilesSelectionResponse> {
    // Create a new file dialog builder:
    let file_dialog = MAIN_WINDOW
        .lock()
        .unwrap()
        .as_ref()
        .map(|w| w.dialog().file().set_parent(w).set_title(&payload.title));

    let Some(mut file_dialog) = file_dialog else {
        error!(Source = "Tauri"; "Cannot open file dialog: main window not available.");
        return Json(FilesSelectionResponse {
            user_cancelled: true,
            selected_file_paths: Vec::new(),
        });
    };

    // Set the file type filter if provided:
    file_dialog = apply_filter(file_dialog, &payload.filter);

    // Set the previous file path if provided:
    if let Some(previous) = &payload.previous_file {
        let previous_path = previous.file_path.as_str();
        file_dialog = file_dialog.set_directory(previous_path);
    }

    // Show the file dialog and get the selected file path:
    let file_paths = file_dialog.blocking_pick_files();
    match file_paths {
        Some(paths) => {
            let converted: Vec<String> = paths.into_iter().filter_map(|p| p.into_path().ok()).map(|pb| pb.to_string_lossy().to_string()).collect();
            info!("User selected {} files.", converted.len());
            Json(FilesSelectionResponse {
                user_cancelled: false,
                selected_file_paths: converted,
            })
        }

        None => {
            info!("User cancelled file selection.");
            Json(FilesSelectionResponse {
                user_cancelled: true,
                selected_file_paths: Vec::new(),
            })
        },
    }
}

pub async fn save_file(_token: APIToken, payload: Json<SaveFileOptions>) -> Json<FileSaveResponse> {
    // Create a new file dialog builder:
    let file_dialog = MAIN_WINDOW
        .lock()
        .unwrap()
        .as_ref()
        .map(|w| w.dialog().file().set_parent(w).set_title(&payload.title));

    let Some(mut file_dialog) = file_dialog else {
        error!(Source = "Tauri"; "Cannot open save dialog: main window not available.");
        return Json(FileSaveResponse {
            user_cancelled: true,
            save_file_path: String::from(""),
        });
    };

    // Set the file type filter if provided:
    file_dialog = apply_filter(file_dialog, &payload.filter);

    // Set the initial directory and file name if provided:
    if let Some(previous) = &payload.previous_file {
        let (directory, file_name) = split_save_file_path(&previous.file_path);
        if let Some(directory) = directory {
            file_dialog = file_dialog.set_directory(directory);
        }
        if let Some(file_name) = file_name {
            file_dialog = file_dialog.set_file_name(file_name);
        }
    }

    // Displays the file dialogue box and select the file:
    let file_path = file_dialog.blocking_save_file();
    match file_path {
        Some(path) => match path.into_path() {
            Ok(pb) => {
                info!("User selected file for writing operation: {pb:?}");
                Json(FileSaveResponse {
                    user_cancelled: false,
                    save_file_path: pb.to_string_lossy().to_string(),
                })
            }
            Err(e) => {
                error!(Source = "Tauri"; "Failed to convert save file path: {e}");
                Json(FileSaveResponse {
                    user_cancelled: true,
                    save_file_path: String::new(),
                })
            }
        },

        None => {
            info!("User cancelled file selection.");
            Json(FileSaveResponse {
                user_cancelled: true,
                save_file_path: String::from(""),
            })
        },
    }
}

pub async fn open_path_in_file_manager(
    _token: APIToken,
    payload: Json<OpenPathOptions>,
) -> Json<OpenPathResponse> {
    let requested_path = PathBuf::from(payload.path.trim());
    if requested_path.as_os_str().is_empty() {
        return Json(OpenPathResponse {
            success: false,
            issue: String::from("The path is empty."),
        });
    }

    match open_file_manager_target(&requested_path).await {
        Ok(()) => Json(OpenPathResponse {
            success: true,
            issue: String::new(),
        }),

        Err(issue) => {
            error!(Source = "Tauri"; "{issue}");
            Json(OpenPathResponse {
                success: false,
                issue,
            })
        }
    }
}

async fn open_file_manager_target(requested_path: &Path) -> Result<(), String> {
    let Some(target) = resolve_file_manager_target(requested_path) else {
        let issue = format!(
            "The path does not exist and its parent folder could not be found: {}",
            requested_path.to_string_lossy(),
        );
        return Err(issue);
    };

    #[cfg(target_os = "linux")]
    {
        return match open_path_in_linux_file_manager(&target).await {
            Ok(()) => {
                info!("Opened file manager for path: {:?}", target.path);
                Ok(())
            }

            Err(issue) => Err(issue),
        };
    }

    #[cfg(any(windows, target_os = "macos"))]
    {
        let mut command = create_file_manager_command(&target);

        #[cfg(windows)]
        command.creation_flags(CREATE_NO_WINDOW);

        match command.spawn() {
            Ok(_) => {
                info!("Opened file manager for path: {:?}", target.path);
                Ok(())
            }

            Err(error) => {
                let issue = format!("Failed to open the file manager: {error}");
                Err(issue)
            }
        }
    }
}

/// Opens a document in the program the system uses for it, on the given page where that is possible.
///
/// The page is best effort and never decides whether this succeeded: a viewer which cannot be told
/// a page still shows the document, which is what the user asked for by clicking a source.
pub async fn open_document(
    _token: APIToken,
    payload: Json<OpenDocumentOptions>,
) -> Json<OpenDocumentResponse> {
    let requested_path = PathBuf::from(payload.path.trim());
    if let Some(issue) = refuse_document(&requested_path) {
        error!(Source = "Tauri"; "Refused to open a document: {issue}");
        return Json(OpenDocumentResponse {
            success: false,
            page_applied: false,
            issue,
        });
    }

    //
    // A page of zero is how a caller says it has none: a slide and a spreadsheet row are not
    // pages, and neither is a passage whose page the index never learned.
    //
    let page = payload.page.filter(|page| *page > 0);
    if let Some(page) = page && try_open_at_page(&requested_path, page).await {
        info!("Opened document at page {page}: {requested_path:?}");
        return Json(OpenDocumentResponse {
            success: true,
            page_applied: true,
            issue: String::new(),
        });
    }

    match tauri_plugin_opener::open_path(&requested_path, None::<&str>) {
        Ok(()) => {
            info!("Opened document: {requested_path:?}");
            Json(OpenDocumentResponse {
                success: true,
                page_applied: false,
                issue: String::new(),
            })
        },

        Err(error) => {
            let issue = format!("Failed to open the document: {error}");
            error!(Source = "Tauri"; "{issue}");
            Json(OpenDocumentResponse {
                success: false,
                page_applied: false,
                issue,
            })
        },
    }
}

/// Extensions which start something instead of being something.
///
/// Such a file gives nothing away by its content — a `.desktop` entry and a `.cmd` script are
/// plain text, a `.lnk` is a shortcut — so its name is the only thing left to recognize it by.
const LAUNCHER_EXTENSIONS: [&str; 10] = [
    "desktop", "command", "lnk", "url", "bat", "cmd", "ps1", "vbs", "scpt", "app",
];

/// Says why a document must not be opened, or `None` when it may be.
///
/// The path arrives from a data source: a folder the user pointed us at, or an ERI server which is
/// free to name any file it likes. This endpoint hands a file to whatever the system has registered
/// for it, so the line worth drawing is that a document is opened and a program is never started.
/// It is drawn here because this is the one place every caller passes through.
fn refuse_document(requested_path: &Path) -> Option<String> {
    if requested_path.as_os_str().is_empty() {
        return Some(String::from("The path is empty."));
    }

    if !requested_path.is_file() {
        return Some(format!("The path is not a file: {}", requested_path.to_string_lossy()));
    }

    let extension = requested_path.extension()
        .map(|extension| extension.to_string_lossy().to_ascii_lowercase())
        .unwrap_or_default();

    if LAUNCHER_EXTENSIONS.contains(&extension.as_str()) {
        return Some(format!(
            "A file of type '{extension}' starts a program instead of showing a document and is not opened: {}",
            requested_path.to_string_lossy(),
        ));
    }

    match FileFormat::from_file(requested_path) {
        Ok(format) if is_executable_content(format) => Some(format!(
            "The file is a program, not a document, and is not opened: {}",
            requested_path.to_string_lossy(),
        )),

        //
        // A file whose content we cannot place is not a file we refuse. The format is asked in
        // order to catch a program carrying a harmless extension, nothing else; what the system
        // makes of anything else is the system's decision, as it is for every other file.
        //
        Ok(_) => None,

        Err(error) => {
            warn!(Source = "Tauri"; "Could not identify the content of '{}': {error}", requested_path.to_string_lossy());
            None
        },
    }
}

/// Tries to show the document on the given page, and says whether it did.
#[cfg(any(windows, target_os = "linux"))]
async fn try_open_at_page(path: &Path, page: u32) -> bool {
    let DocumentOpenPlan::WithPage { program, arguments } = resolve_document_open_plan(path, page).await else {
        return false;
    };

    match start_page_aware_viewer(&program, &arguments) {
        Ok(()) => true,

        //
        // Failing to start the viewer ourselves is not something the user has to hear about: the
        // caller opens the document plainly afterwards, only without the page.
        //
        Err(issue) => {
            warn!(Source = "Tauri"; "Could not open '{}' at page {page}, opening it without a page instead: {issue}", path.to_string_lossy());
            false
        },
    }
}

/// Never shows a page on macOS.
///
/// `open` drops the fragment of a URL before the program it starts ever sees it, with and without
/// `-a`, so a page cannot be named from the command line at all. The document opens on its first
/// page, and the source names the page for the reader.
#[cfg(target_os = "macos")]
async fn try_open_at_page(_path: &Path, _page: u32) -> bool {
    false
}

/// How a document viewer wants to be told which page to show.
///
/// They all mean the same thing and every one of them spells it differently. A viewer which is not
/// covered here shows its first page, which is what the system would have done anyway.
///
/// Which spellings exist follows from where a viewer is found: Acrobat is named by the Windows
/// registration and by nothing else, and the three Linux viewers are named by a desktop entry and
/// by nothing else. Only a browser is reached on both, so only its spelling is needed everywhere.
#[cfg(any(windows, target_os = "linux", test))]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum PageArgument {
    /// The page travels in the URL fragment, the way the PDF Open Parameters define it. Browsers
    /// read it, and on Windows a browser is what most people open a PDF with.
    UrlFragment,

    /// Acrobat and Acrobat Reader take an open action: `/A page=12`.
    #[cfg(any(windows, test))]
    AcrobatOpenAction,

    /// The GNOME document viewer and its forks count from zero, so page 12 is index 11.
    #[cfg(any(target_os = "linux", test))]
    ZeroBasedIndex,

    /// Okular takes `-p 12`.
    #[cfg(any(target_os = "linux", test))]
    OkularPage,

    /// Zathura takes `-P 12`.
    #[cfg(any(target_os = "linux", test))]
    ZathuraPage,
}

/// What it takes to show a document on a page.
#[cfg(any(windows, target_os = "linux"))]
#[derive(Debug, PartialEq, Eq)]
enum DocumentOpenPlan {
    /// Hand the file to the system and let it decide. The document opens on its first page.
    Plain,

    /// Start this program ourselves, because it takes the page as an argument.
    WithPage { program: String, arguments: Vec<String> },
}

/// Whether this file is a PDF.
///
/// Only PDFs are sent to a page: the handler is looked up for PDFs, and the arguments below are
/// the ones PDF viewers understand. A Word file has a page too, but the programs which show one
/// cannot be told to go there.
#[cfg(any(windows, target_os = "linux", test))]
fn is_pdf_document(path: &Path) -> bool {
    path.extension().is_some_and(|extension| extension.eq_ignore_ascii_case("pdf"))
}

/// Builds the arguments which name the page, in the spelling this viewer expects.
#[cfg(any(windows, target_os = "linux", test))]
fn page_arguments(argument: PageArgument, path: &Path, page: u32) -> Option<Vec<String>> {
    let path_argument = path.to_string_lossy().to_string();
    Some(match argument {
        PageArgument::UrlFragment => vec![document_url_with_page(path, page)?],

        #[cfg(any(windows, test))]
        PageArgument::AcrobatOpenAction => vec![String::from("/A"), format!("page={page}"), path_argument],

        #[cfg(any(target_os = "linux", test))]
        PageArgument::ZeroBasedIndex => vec![format!("--page-index={}", page.saturating_sub(1)), path_argument],

        #[cfg(any(target_os = "linux", test))]
        PageArgument::OkularPage => vec![String::from("-p"), page.to_string(), path_argument],

        #[cfg(any(target_os = "linux", test))]
        PageArgument::ZathuraPage => vec![String::from("-P"), page.to_string(), path_argument],
    })
}

/// Builds a `file:` URL which names the page, the way the PDF Open Parameters define it.
///
/// The URL is built instead of written by hand because a path may hold spaces, umlauts or a hash
/// of its own, and writing one by hand turns those into a different path or into a second fragment.
#[cfg(any(windows, target_os = "linux", test))]
fn document_url_with_page(path: &Path, page: u32) -> Option<String> {
    let mut url = tauri::Url::from_file_path(path).ok()?;
    url.set_fragment(Some(&format!("page={page}")));
    Some(url.to_string())
}

/// Starts the viewer. Success means the program was started, not that it showed the page.
///
/// Waiting for it to say so is not possible: a viewer runs until the user closes it, so waiting
/// would hold the request open for as long as the document stays on screen.
#[cfg(any(windows, target_os = "linux"))]
fn start_page_aware_viewer(program: &str, arguments: &[String]) -> Result<(), String> {
    let mut command = std::process::Command::new(program);
    command.args(arguments);

    #[cfg(windows)]
    command.creation_flags(CREATE_NO_WINDOW);

    command.spawn()
        .map(|_| ())
        .map_err(|error| format!("Failed to start '{program}': {error}"))
}

#[cfg(any(windows, target_os = "linux"))]
async fn resolve_document_open_plan(path: &Path, page: u32) -> DocumentOpenPlan {
    if !is_pdf_document(path) {
        return DocumentOpenPlan::Plain;
    }

    #[cfg(windows)]
    {
        let Some(prog_id) = windows_default_pdf_prog_id() else {
            return DocumentOpenPlan::Plain;
        };

        let Some(argument) = windows_page_argument(&prog_id) else {
            return DocumentOpenPlan::Plain;
        };

        let Some(program) = windows_handler_executable(&prog_id) else {
            return DocumentOpenPlan::Plain;
        };

        let Some(arguments) = page_arguments(argument, path, page) else {
            return DocumentOpenPlan::Plain;
        };

        DocumentOpenPlan::WithPage { program, arguments }
    }

    #[cfg(target_os = "linux")]
    {
        let Some(desktop_id) = linux_default_pdf_handler().await else {
            return DocumentOpenPlan::Plain;
        };

        let Some((program, argument)) = linux_page_aware_program(&desktop_id) else {
            return DocumentOpenPlan::Plain;
        };

        let Some(arguments) = page_arguments(argument, path, page) else {
            return DocumentOpenPlan::Plain;
        };

        DocumentOpenPlan::WithPage { program, arguments }
    }
}

/// Reads which program the user opens PDFs with.
///
/// The user's own choice comes first; the class registration is what is left when they never made
/// one, for instance right after the system was installed.
#[cfg(windows)]
fn windows_default_pdf_prog_id() -> Option<String> {
    use windows_registry::*;

    const USER_CHOICE_KEY: &str = r"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.pdf\UserChoice";

    if let Ok(key) = CURRENT_USER.open(USER_CHOICE_KEY) && let Ok(prog_id) = key.get_string("ProgId") {
        return Some(prog_id);
    }

    CLASSES_ROOT.open(".pdf").ok()
        .and_then(|key| key.get_string("").ok())
        .filter(|prog_id| !prog_id.is_empty())
}

/// Reads the program behind a registered file type.
#[cfg(windows)]
fn windows_handler_executable(prog_id: &str) -> Option<String> {
    use windows_registry::*;

    let command = CLASSES_ROOT.open(format!(r"{prog_id}\shell\open\command")).ok()?
        .get_string("").ok()?;

    executable_from_command(&command)
}

/// Picks the program out of a registry open command such as
/// `"C:\Program Files\...\msedge.exe" --single-argument %1`.
///
/// The arguments the command carries are dropped on purpose: they are written for a file name, and
/// what follows is a URL naming a page instead.
#[cfg(any(windows, test))]
fn executable_from_command(command: &str) -> Option<String> {
    let command = command.trim();
    let executable = match command.strip_prefix('"') {
        Some(quoted) => quoted.split('"').next()?,
        None => command.split_whitespace().next()?,
    };

    let executable = executable.trim();
    if executable.is_empty() {
        None
    } else {
        Some(String::from(executable))
    }
}

/// Maps the registered file type onto the way its program wants to hear about a page.
#[cfg(any(windows, test))]
fn windows_page_argument(prog_id: &str) -> Option<PageArgument> {
    let prog_id = prog_id.to_ascii_lowercase();

    //
    // Acrobat is asked about first, because its registration says nothing about a browser while
    // the browsers below are recognized by their own name in it.
    //
    if prog_id.contains("acroexch") || prog_id.contains("acrobat") {
        return Some(PageArgument::AcrobatOpenAction);
    }

    const BROWSERS: [&str; 5] = ["msedge", "chrome", "firefox", "opera", "brave"];
    if BROWSERS.iter().any(|browser| prog_id.contains(browser)) {
        return Some(PageArgument::UrlFragment);
    }

    None
}

/// Reads which program the desktop opens PDFs with.
///
/// Inside a Flatpak there is nothing to read: the sandbox has its own list of registered programs
/// rather than the desktop's, and even the right answer would name a program which is not in the
/// sandbox to be started. The document is handed to the desktop portal instead, which opens it on
/// its first page.
#[cfg(target_os = "linux")]
async fn linux_default_pdf_handler() -> Option<String> {
    if crate::environment::is_flatpak() {
        return None;
    }

    let output = tokio::process::Command::new("xdg-mime")
        .args(["query", "default", "application/pdf"])
        .output()
        .await
        .ok()?;

    if !output.status.success() {
        return None;
    }

    //
    // More than one entry can be registered, and the first one is the one the desktop uses.
    //
    let desktop_id = String::from_utf8_lossy(&output.stdout).lines().next()?.trim().to_string();
    if desktop_id.is_empty() {
        None
    } else {
        Some(desktop_id)
    }
}

/// Maps a desktop entry onto the program behind it and the way that program wants to hear about a page.
///
/// A desktop id is not the name of a binary — GNOME's viewer answers `org.gnome.Evince.desktop` —
/// so reading the desktop file would be the thorough way to find the program. Recognizing the few
/// viewers which can be sent to a page at all is the short one, and everything else opens the way
/// it always did, through the desktop's own handler.
#[cfg(any(target_os = "linux", test))]
fn linux_page_aware_program(desktop_id: &str) -> Option<(String, PageArgument)> {
    const KNOWN_VIEWERS: [(&str, &str, PageArgument); 9] = [
        //
        // Atril and Xreader are forks of Evince and count their pages from zero just as it does.
        //
        ("evince", "evince", PageArgument::ZeroBasedIndex),
        ("atril", "atril", PageArgument::ZeroBasedIndex),
        ("xreader", "xreader", PageArgument::ZeroBasedIndex),

        ("okular", "okular", PageArgument::OkularPage),
        ("zathura", "zathura", PageArgument::ZathuraPage),

        //
        // Chrome is asked about before Chromium, so that a desktop entry naming both lands on the
        // program the user actually installed.
        //
        ("google-chrome", "google-chrome", PageArgument::UrlFragment),
        ("chromium", "chromium", PageArgument::UrlFragment),
        ("microsoft-edge", "microsoft-edge", PageArgument::UrlFragment),
        ("firefox", "firefox", PageArgument::UrlFragment),
    ];

    let desktop_id = desktop_id.to_ascii_lowercase();
    KNOWN_VIEWERS.iter()
        .find(|(needle, _, _)| desktop_id.contains(needle))
        .map(|(_, program, argument)| (String::from(*program), *argument))
}

/// Applies an optional file type filter to a FileDialogBuilder.
fn apply_filter<R: tauri::Runtime>(file_dialog: FileDialogBuilder<R>, filter: &Option<FileTypeFilter>) -> FileDialogBuilder<R> {
    match filter {
        Some(f) => file_dialog.add_filter(
            &f.filter_name,
            &f.filter_extensions.iter().map(|s| s.as_str()).collect::<Vec<&str>>(),
        ),

        None => file_dialog,
    }
}

fn split_save_file_path(file_path: &str) -> (Option<PathBuf>, Option<String>) {
    let path = Path::new(file_path);
    let directory = path
        .parent()
        .filter(|parent| !parent.as_os_str().is_empty())
        .map(Path::to_path_buf);
        
    let file_name = path
        .file_name()
        .map(|name| name.to_string_lossy().into_owned())
        .filter(|name| !name.is_empty());

    (directory, file_name)
}

#[derive(Debug, PartialEq, Eq)]
struct FileManagerTarget {
    path: PathBuf,
    reveal_file: bool,
}

#[cfg(any(target_os = "linux", test))]
#[derive(Debug, PartialEq, Eq)]
enum LinuxPortalOperation {
    RevealFile,
    OpenDirectory,
}

fn resolve_file_manager_target(requested_path: &Path) -> Option<FileManagerTarget> {
    if requested_path.is_file() {
        return Some(FileManagerTarget {
            path: requested_path.to_path_buf(),
            reveal_file: true,
        });
    }

    if requested_path.is_dir() {
        return Some(FileManagerTarget {
            path: requested_path.to_path_buf(),
            reveal_file: false,
        });
    }

    requested_path.parent()
        .filter(|parent| parent.is_dir())
        .map(|parent| FileManagerTarget {
            path: parent.to_path_buf(),
            reveal_file: false,
        })
}

#[cfg(any(target_os = "linux", test))]
fn linux_portal_operation(target: &FileManagerTarget) -> LinuxPortalOperation {
    if target.reveal_file {
        LinuxPortalOperation::RevealFile
    } else {
        LinuxPortalOperation::OpenDirectory
    }
}

#[cfg(any(target_os = "linux", test))]
fn xdg_open_fallback_path(target: &FileManagerTarget) -> &Path {
    if target.reveal_file {
        target.path.parent().unwrap_or(&target.path)
    } else {
        &target.path
    }
}

#[cfg(target_os = "linux")]
enum LinuxPortalError {
    Unavailable(String),
    RequestFailed(String),
}

#[cfg(target_os = "linux")]
async fn open_path_with_linux_portal(target: &FileManagerTarget) -> Result<(), LinuxPortalError> {
    let file = std::fs::File::open(&target.path)
        .map_err(|error| LinuxPortalError::Unavailable(format!("Failed to open the path for the desktop portal: {error}")))?;

    let request = match linux_portal_operation(target) {
        LinuxPortalOperation::RevealFile => OpenDirectoryRequest::default().send(&file).await,
        LinuxPortalOperation::OpenDirectory => OpenFileRequest::default().send_file(&file).await,
    }
    .map_err(|error| LinuxPortalError::Unavailable(format!("Desktop portal invocation failed: {error}")))?;

    request.response()
        .map_err(|error| LinuxPortalError::RequestFailed(format!("Desktop portal request failed: {error}")))
}

#[cfg(target_os = "linux")]
async fn open_path_with_xdg_open(target: &FileManagerTarget) -> Result<(), String> {
    let fallback_path = xdg_open_fallback_path(target);
    let status = tokio::process::Command::new("xdg-open")
        .arg(fallback_path)
        .status()
        .await
        .map_err(|error| format!("xdg-open failed to start for '{}': {error}", fallback_path.to_string_lossy()))?;

    if status.success() {
        Ok(())
    } else {
        Err(format!("xdg-open failed for '{}' with exit status {status}", fallback_path.to_string_lossy()))
    }
}

#[cfg(target_os = "linux")]
async fn open_path_in_linux_file_manager(target: &FileManagerTarget) -> Result<(), String> {
    match open_path_with_linux_portal(target).await {
        Ok(()) => Ok(()),
        Err(LinuxPortalError::RequestFailed(error)) => Err(error),
        Err(LinuxPortalError::Unavailable(portal_error)) => {
            match open_path_with_xdg_open(target).await {
                Ok(()) => Ok(()),
                Err(fallback_error) => Err(format!("{portal_error} Fallback failed: {fallback_error}")),
            }
        }
    }
}

#[cfg(target_os = "windows")]
fn create_file_manager_command(target: &FileManagerTarget) -> Command {
    let mut command = Command::new("explorer.exe");
    if target.reveal_file {
        command.arg(format!("/select,{}", target.path.to_string_lossy()));
    } else {
        command.arg(&target.path);
    }

    command
}

#[cfg(target_os = "macos")]
fn create_file_manager_command(target: &FileManagerTarget) -> Command {
    let mut command = Command::new("open");
    if target.reveal_file {
        command.arg("-R");
    }

    command.arg(&target.path);
    command
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::fs;

    #[test]
    fn save_file_options_accept_the_previous_file_contract() {
        let options: SaveFileOptions = serde_json::from_str(
            r#"{"title":"Export visual briefing","previous_file":{"file_path":"Quarterly briefing.html"}}"#,
        )
        .unwrap();

        assert_eq!(options.title, "Export visual briefing");
        assert_eq!(
            options.previous_file.unwrap().file_path,
            "Quarterly briefing.html",
        );
    }

    #[test]
    fn save_file_name_without_directory_is_preserved() {
        let (directory, file_name) = split_save_file_path("Quarterly briefing.html");

        assert_eq!(directory, None);
        assert_eq!(file_name.as_deref(), Some("Quarterly briefing.html"));
    }

    #[test]
    fn save_file_path_is_split_into_directory_and_name() {
        let temp_dir = tempfile::tempdir().unwrap();
        let initial_path = temp_dir.path().join("Quarterly briefing.html");

        let (directory, file_name) = split_save_file_path(initial_path.to_str().unwrap());

        assert_eq!(directory.as_deref(), Some(temp_dir.path()));
        assert_eq!(file_name.as_deref(), Some("Quarterly briefing.html"));
    }

    #[test]
    fn existing_file_is_revealed_and_falls_back_to_its_parent() {
        let temp_dir = tempfile::tempdir().unwrap();
        let file_path = temp_dir.path().join("application.log");
        fs::write(&file_path, "log").unwrap();

        let target = resolve_file_manager_target(&file_path).unwrap();

        assert_eq!(target.path, file_path);
        assert!(target.reveal_file);
        assert_eq!(linux_portal_operation(&target), LinuxPortalOperation::RevealFile);
        assert_eq!(xdg_open_fallback_path(&target), temp_dir.path());
    }

    #[test]
    fn existing_directory_is_opened_directly() {
        let temp_dir = tempfile::tempdir().unwrap();

        let target = resolve_file_manager_target(temp_dir.path()).unwrap();

        assert_eq!(target.path, temp_dir.path());
        assert!(!target.reveal_file);
        assert_eq!(linux_portal_operation(&target), LinuxPortalOperation::OpenDirectory);
        assert_eq!(xdg_open_fallback_path(&target), temp_dir.path());
    }

    #[test]
    fn missing_file_uses_its_existing_parent_directory() {
        let temp_dir = tempfile::tempdir().unwrap();
        let missing_file = temp_dir.path().join("missing.log");

        let target = resolve_file_manager_target(&missing_file).unwrap();

        assert_eq!(target.path, temp_dir.path());
        assert!(!target.reveal_file);
        assert_eq!(linux_portal_operation(&target), LinuxPortalOperation::OpenDirectory);
        assert_eq!(xdg_open_fallback_path(&target), temp_dir.path());
    }

    #[test]
    fn invalid_path_without_existing_parent_is_rejected() {
        let temp_dir = tempfile::tempdir().unwrap();
        let invalid_path = temp_dir.path().join("missing-directory").join("missing.log");

        assert!(resolve_file_manager_target(&invalid_path).is_none());
    }

    /// The bytes an ELF binary starts with. A file which begins like this is a program, whatever
    /// its name promises.
    const ELF_HEADER: &[u8] = b"\x7fELF\x02\x01\x01\x00\x00\x00\x00\x00\x00\x00\x00\x00\x02\x00\x3e\x00";

    #[test]
    fn a_document_may_be_opened() {
        let temp_dir = tempfile::tempdir().unwrap();
        let document_path = temp_dir.path().join("handbook.pdf");
        fs::write(&document_path, b"%PDF-1.7\n% a handbook\n").unwrap();

        assert_eq!(refuse_document(&document_path), None);
    }

    /// A program which carries a harmless extension is the case this guard exists for: nothing
    /// about the name says what it is, so the content has to.
    #[test]
    fn a_program_named_like_a_document_is_refused() {
        let temp_dir = tempfile::tempdir().unwrap();
        let disguised_path = temp_dir.path().join("handbook.pdf");
        fs::write(&disguised_path, ELF_HEADER).unwrap();

        let refusal = refuse_document(&disguised_path).unwrap();

        assert!(refusal.contains("is a program"), "The refusal says why: {refusal}");
    }

    /// The other way round: a launcher is plain text and gives nothing away, so it is refused by
    /// its name.
    #[test]
    fn a_launcher_is_refused_although_it_reads_like_text() {
        let temp_dir = tempfile::tempdir().unwrap();
        let launcher_path = temp_dir.path().join("handbook.desktop");
        fs::write(&launcher_path, "[Desktop Entry]\nExec=rm -rf ~\n").unwrap();

        let refusal = refuse_document(&launcher_path).unwrap();

        assert!(refusal.contains("starts a program"), "The refusal says why: {refusal}");
    }

    #[test]
    fn a_launcher_is_refused_whatever_its_extension_is_spelled_like() {
        let temp_dir = tempfile::tempdir().unwrap();
        let launcher_path = temp_dir.path().join("handbook.CMD");
        fs::write(&launcher_path, "echo nothing to see here\n").unwrap();

        assert!(refuse_document(&launcher_path).is_some());
    }

    #[test]
    fn a_path_which_is_no_file_is_refused() {
        let temp_dir = tempfile::tempdir().unwrap();

        assert!(refuse_document(&temp_dir.path().join("missing.pdf")).is_some(), "A file which is not there cannot be opened.");
        assert!(refuse_document(temp_dir.path()).is_some(), "A folder is not a document.");
        assert!(refuse_document(Path::new("")).is_some(), "An empty path names nothing.");
    }

    #[test]
    fn only_a_pdf_is_sent_to_a_page() {
        assert!(is_pdf_document(Path::new("/docs/handbook.pdf")));
        assert!(is_pdf_document(Path::new("/docs/handbook.PDF")), "How the extension is spelled says nothing about the file.");
        assert!(!is_pdf_document(Path::new("/docs/handbook.docx")), "A Word file has pages, but no program which shows one can be told to go there.");
        assert!(!is_pdf_document(Path::new("/docs/handbook")));
    }

    /// Writing the URL by hand would leave the space in the name as it is, and the browser would
    /// look for a file whose name ends before it.
    #[test]
    fn a_browser_is_told_the_page_in_the_url() {
        let temp_dir = tempfile::tempdir().unwrap();
        let document_path = temp_dir.path().join("Größere Übersicht.pdf");

        let arguments = page_arguments(PageArgument::UrlFragment, &document_path, 12).unwrap();

        assert_eq!(arguments.len(), 1, "A browser takes the document and the page as one URL.");

        let url = tauri::Url::parse(&arguments[0]).unwrap();
        assert_eq!(url.fragment(), Some("page=12"), "The page travels in the fragment, the way the PDF Open Parameters define it.");
        assert_eq!(url.to_file_path().unwrap(), document_path, "A name with spaces and umlauts still names the same file.");
    }

    /// Everybody means page twelve, and everybody says it differently.
    #[test]
    fn every_viewer_spells_the_page_its_own_way() {
        let document = Path::new("/docs/handbook.pdf");

        assert_eq!(
            page_arguments(PageArgument::AcrobatOpenAction, document, 12).unwrap(),
            vec![String::from("/A"), String::from("page=12"), String::from("/docs/handbook.pdf")],
        );

        assert_eq!(
            page_arguments(PageArgument::ZeroBasedIndex, document, 12).unwrap(),
            vec![String::from("--page-index=11"), String::from("/docs/handbook.pdf")],
            "The GNOME viewer counts from zero, so page twelve is index eleven.",
        );

        assert_eq!(
            page_arguments(PageArgument::OkularPage, document, 12).unwrap(),
            vec![String::from("-p"), String::from("12"), String::from("/docs/handbook.pdf")],
        );

        assert_eq!(
            page_arguments(PageArgument::ZathuraPage, document, 12).unwrap(),
            vec![String::from("-P"), String::from("12"), String::from("/docs/handbook.pdf")],
        );
    }

    #[test]
    fn windows_recognizes_the_programs_it_can_send_to_a_page() {
        assert_eq!(windows_page_argument("AcroExch.Document.DC"), Some(PageArgument::AcrobatOpenAction));
        assert_eq!(windows_page_argument("MSEdgePDF"), Some(PageArgument::UrlFragment));
        assert_eq!(windows_page_argument("ChromePDF"), Some(PageArgument::UrlFragment));
        assert_eq!(windows_page_argument("FirefoxPDF"), Some(PageArgument::UrlFragment));
        assert_eq!(windows_page_argument("Applications\\SumatraPDF.exe"), None, "A viewer we know nothing about opens its first page.");
    }

    #[test]
    fn the_program_is_read_out_of_the_registered_command() {
        assert_eq!(
            executable_from_command(r#""C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" --single-argument %1"#).as_deref(),
            Some(r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"),
            "A quoted program keeps the spaces in its path and loses the arguments written for a file name.",
        );

        assert_eq!(
            executable_from_command(r#"C:\Windows\System32\viewer.exe "%1""#).as_deref(),
            Some(r"C:\Windows\System32\viewer.exe"),
        );

        assert_eq!(executable_from_command("   "), None);
    }

    #[test]
    fn linux_recognizes_the_programs_it_can_send_to_a_page() {
        assert_eq!(
            linux_page_aware_program("org.gnome.Evince.desktop"),
            Some((String::from("evince"), PageArgument::ZeroBasedIndex)),
            "A desktop entry is not the name of a binary, and the binary is what we have to start.",
        );

        assert_eq!(linux_page_aware_program("okularApplication_pdf.desktop"), Some((String::from("okular"), PageArgument::OkularPage)));
        assert_eq!(linux_page_aware_program("org.pwmt.zathura.desktop"), Some((String::from("zathura"), PageArgument::ZathuraPage)));
        assert_eq!(linux_page_aware_program("firefox.desktop"), Some((String::from("firefox"), PageArgument::UrlFragment)));
        assert_eq!(linux_page_aware_program("google-chrome.desktop"), Some((String::from("google-chrome"), PageArgument::UrlFragment)));
        assert_eq!(linux_page_aware_program("chromium_chromium.desktop"), Some((String::from("chromium"), PageArgument::UrlFragment)));
        assert_eq!(linux_page_aware_program("com.example.SomeViewer.desktop"), None, "A viewer we know nothing about opens its first page.");
    }
}