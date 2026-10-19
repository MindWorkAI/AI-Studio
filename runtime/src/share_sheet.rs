use axum::Json;
use log::{error, info};
use serde::{Deserialize, Serialize};
use std::path::{Path, PathBuf};

/// The directory the app creates its shareable plugin archives in. Keep in sync with
/// PluginShareService.TEMPORARY_ARCHIVE_DIRECTORY on the .NET side.
const SHARE_DIRECTORY_NAME: &str = "mindwork-ai-studio-plugin-shares";

/// The file extension of plugin archives, without the leading dot. Keep in sync with
/// PluginShareService.PLUGIN_FILE_EXTENSION on the .NET side.
const SHARE_FILE_EXTENSION: &str = "mwplugin";

#[derive(Deserialize)]
pub struct ShareFileRequest {
    file_path: String,
}

/// What became of a share request. Keep in sync with ShareSheetOutcome on the .NET side.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum ShareOutcome {
    /// The operating system opened its share UI for the file.
    Opened,

    /// The share UI could not be opened, or the request was invalid.
    Failed,

    /// Windows accepted the share request, but its share UI never asked for the file. Windows
    /// reports no error then, and the share UI simply does not appear.
    #[cfg(windows)]
    Unresponsive,
}

#[derive(Serialize)]
pub struct ShareFileResponse {
    outcome: ShareOutcome,
    issue: String,
}

/// Why a share request did not open the share UI.
struct ShareFailure {
    outcome: ShareOutcome,
    issue: String,
}

/// Most failures are plain errors of the platform APIs, hence a text alone counts as a failure.
impl From<String> for ShareFailure {
    fn from(issue: String) -> Self {
        Self {
            outcome: ShareOutcome::Failed,
            issue,
        }
    }
}

pub async fn share_file(Json(request): Json<ShareFileRequest>) -> Json<ShareFileResponse> {
    let path = PathBuf::from(request.file_path.trim());
    if path.as_os_str().is_empty() {
        return failure("The file path is empty.");
    }

    if !path.is_file() {
        return failure(format!("The requested path is not an existing file: {}", path.to_string_lossy()));
    }

    // Resolve the path before validating it, so a symlink with a matching name cannot point at an
    // arbitrary file. We share the original path afterwards, though: on Windows, canonicalize
    // returns a \\?\ path, which the WinRT storage APIs do not accept.
    let resolved_path = match std::fs::canonicalize(&path) {
        Ok(resolved_path) => resolved_path,
        Err(error) => return failure(format!("The requested path could not be resolved: {error}")),
    };

    if !is_shareable_archive(&resolved_path) {
        return failure(format!("The requested path is not a plugin archive created by AI Studio: {}", path.to_string_lossy()));
    }

    let result = share_file_on_platform(path).await;
    match result {
        Ok(()) => {
            info!(Source = "Share sheet"; "Opened the native share UI.");
            Json(ShareFileResponse {
                outcome: ShareOutcome::Opened,
                issue: String::new(),
            })
        }

        Err(share_failure) => {
            error!(Source = "Share sheet"; "{}", share_failure.issue);
            Json(ShareFileResponse {
                outcome: share_failure.outcome,
                issue: share_failure.issue,
            })
        }
    }
}

/// Checks that a path points to a plugin archive the app itself created for sharing. This keeps the
/// endpoint from handing arbitrary readable files to the operating system's share UI.
///
/// We match the directory by name instead of comparing it against the temporary directory: Rust and
/// .NET do not have to agree on where that is, and a mismatch would break sharing entirely.
fn is_shareable_archive(path: &Path) -> bool {
    let has_archive_extension = path.extension().is_some_and(|extension| extension.eq_ignore_ascii_case(SHARE_FILE_EXTENSION));
    if !has_archive_extension {
        return false;
    }

    path.parent()
        .and_then(|parent| parent.file_name())
        .is_some_and(|directory_name| directory_name == SHARE_DIRECTORY_NAME)
}

fn failure(issue: impl Into<String>) -> Json<ShareFileResponse> {
    Json(ShareFileResponse {
        outcome: ShareOutcome::Failed,
        issue: issue.into(),
    })
}

// Linux has no native share sheet: the XDG desktop portals do not provide a share interface. The
// app exports the file through the save dialog instead, hence this endpoint is not used on Linux:
#[cfg(target_os = "linux")]
async fn share_file_on_platform(_path: PathBuf) -> Result<(), ShareFailure> {
    Err(String::from("The native share sheet is not available on Linux.").into())
}

#[cfg(windows)]
async fn share_file_on_platform(path: PathBuf) -> Result<(), ShareFailure> {
    use std::cell::RefCell;
    use std::time::{Duration, Instant};
    use log::{debug, warn};
    use windows::ApplicationModel::DataTransfer::{DataPackage, DataRequestedEventArgs, DataTransferManager, ShareCompletedEventArgs};
    use windows::Foundation::TypedEventHandler;
    use windows::Storage::{IStorageItem, StorageFile};
    use windows::Win32::UI::Shell::IDataTransferManagerInterop;
    use windows::core::{factory, IInspectable, HSTRING, Interface};
    use windows_collections::IIterable;

    /// How long Windows waits for the shared data. The documentation of DataRequest.GetDeferral
    /// names 200 ms, after which the share operation times out.
    const DATA_PACKAGE_DEADLINE: Duration = Duration::from_millis(200);

    /// How long the share UI may take to ask for the shared file after Windows accepted the share
    /// request. A working share UI asks right away, before it lists any target. The limit is
    /// generous on purpose: we adjust it once the log shows how long slow computers take.
    const DATA_REQUEST_TIMEOUT: Duration = Duration::from_secs(10);

    /// Hands the shared file to Windows, once its share UI asks for it.
    fn provide_shared_file(arguments: &DataRequestedEventArgs, path: &str) -> windows::core::Result<()> {
        let request = arguments.Request()?;
        let lookup_started = Instant::now();
        let file = StorageFile::GetFileFromPathAsync(&HSTRING::from(path))?.get()?;
        let lookup_duration = lookup_started.elapsed();
        if lookup_duration > DATA_PACKAGE_DEADLINE {
            warn!(Source = "Share sheet"; "Looking up the shared file took {} ms, but Windows allows only {} ms to provide the data.", lookup_duration.as_millis(), DATA_PACKAGE_DEADLINE.as_millis());
        }

        let file: IStorageItem = file.cast()?;
        let items = IIterable::<IStorageItem>::from(vec![Some(file)]);
        let data = request.Data()?;
        data.Properties()?.SetTitle(&HSTRING::from("MindWork AI Studio"))?;
        data.SetStorageItemsReadOnly(&items)?;
        observe_share_outcome(&data);
        Ok(())
    }

    /// Logs how the share ends. Windows reports this for each data package, and each share gets a
    /// package of its own, so these handlers never stack up. Nothing depends on these events: they
    /// only tell us in the log what the user saw.
    fn observe_share_outcome(data: &DataPackage) {
        let completed = TypedEventHandler::<DataPackage, ShareCompletedEventArgs>::new(|_, arguments| {
            let target_app = arguments.as_ref()
                .and_then(|arguments| arguments.ShareTarget().ok())
                .and_then(|target| target.AppUserModelId().ok())
                .map(|app_id| app_id.to_string())
                .unwrap_or_default();

            info!(Source = "Share sheet"; "The Windows share sheet shared the file with '{target_app}'.");
            Ok(())
        });

        // Windows offers this event since version 1703:
        if let Err(error) = data.ShareCompleted(&completed) {
            debug!(Source = "Share sheet"; "Cannot observe whether the share completes: {error}");
        }

        let canceled = TypedEventHandler::<DataPackage, IInspectable>::new(|_, _| {
            info!(Source = "Share sheet"; "The Windows share sheet closed without sharing the file.");
            Ok(())
        });

        // Windows offers this event since version 2004:
        if let Err(error) = data.ShareCanceled(&canceled) {
            debug!(Source = "Share sheet"; "Cannot observe whether the share gets canceled: {error}");
        }
    }

    // The DataTransferManager belongs to the window, not to a single share. Registering a handler
    // for every share would stack them up, and each stale handler keeps pointing at the archive of
    // its own share, which gets cleaned up after a while. We therefore remember the registration
    // and remove the previous handler before adding a new one. We only ever register on the main
    // thread, hence a thread-local reference is sufficient:
    thread_local! {
        static DATA_REQUESTED_TOKEN: RefCell<Option<i64>> = const { RefCell::new(None) };
    }

    let window = crate::app_window::MAIN_WINDOW.lock().unwrap().clone()
        .ok_or_else(|| String::from("The main window is not available."))?;
    let ui_window = window.clone();
    let path = path.to_string_lossy().to_string();
    let (sender, receiver) = tokio::sync::oneshot::channel();
    let (data_requested_sender, data_requested_receiver) = tokio::sync::oneshot::channel::<Result<(), String>>();

    window.run_on_main_thread(move || {
        let result = (|| -> Result<(), String> {
            let hwnd = ui_window.hwnd().map_err(|error| format!("Failed to get the native window handle: {error}"))?;
            let interop: IDataTransferManagerInterop = factory::<DataTransferManager, IDataTransferManagerInterop>()
                .map_err(|error| format!("Failed to access the Windows share service: {error}"))?;
            let manager: DataTransferManager = unsafe { interop.GetForWindow(hwnd) }
                .map_err(|error| format!("Failed to create the Windows share request: {error}"))?;

            //
            // When its share UI really opens, Windows first asks for the data and lists the targets
            // only afterward. How long that takes shows in the log whether the share UI responded at
            // all. Registering the handler below takes no measurable time, so we start measuring here.
            //
            // The handler runs on the main thread. Like the window events in start_tauri, it must
            // therefore never lock MAIN_WINDOW: it would deadlock as soon as anybody else holds
            // that lock.
            //
            let share_started = Instant::now();
            let mut data_requested = Some(data_requested_sender);
            let handler = TypedEventHandler::<DataTransferManager, DataRequestedEventArgs>::new(move |_, arguments| {
                let Some(arguments) = arguments.as_ref() else {
                    return Ok(());
                };

                info!(Source = "Share sheet"; "Windows requested the shared file {} ms after AI Studio opened its share UI.", share_started.elapsed().as_millis());
                let result = provide_shared_file(arguments, &path);
                match data_requested.take() {
                    // The first request answers the share call, which waits for it:
                    Some(sender) => {
                        let _ = sender.send(result.clone().map_err(|error| format!("Failed to provide the shared file to Windows: {error}")));
                    },

                    // Windows asked again, or only after the share call stopped waiting. Windows
                    // shows a failure in its share UI, but would not log it anywhere:
                    None => if let Err(error) = &result {
                        error!(Source = "Share sheet"; "Failed to provide the shared file to Windows: {error}");
                    },
                }

                result
            });
            if let Some(previous_token) = DATA_REQUESTED_TOKEN.with(|token| token.borrow_mut().take()) {
                let _ = manager.RemoveDataRequested(previous_token);
            }

            let token = manager.DataRequested(&handler)
                .map_err(|error| format!("Failed to provide the shared file: {error}"))?;
            DATA_REQUESTED_TOKEN.with(|current| current.replace(Some(token)));
            unsafe { interop.ShowShareUIForWindow(hwnd) }
                .map_err(|error| format!("Failed to open the Windows share sheet: {error}"))?;
            Ok(())
        })();
        let _ = sender.send(result);
    }).map_err(|error| format!("Failed to schedule the Windows share sheet: {error}"))?;

    receiver.await.map_err(|_| String::from("The Windows share sheet did not return a result."))??;

    //
    // Windows accepts a share request even when its share UI hangs and never appears. Only when
    // the share UI asks for the file do we know that it opened. We wait for that without blocking
    // the worker, so that the other calls of the app go on meanwhile:
    //
    match tokio::time::timeout(DATA_REQUEST_TIMEOUT, data_requested_receiver).await {
        Ok(Ok(result)) => result.map_err(ShareFailure::from),
        Ok(Err(_)) => Err(String::from("The Windows share sheet discarded the share request before it asked for the file.").into()),
        Err(_) => Err(ShareFailure {
            outcome: ShareOutcome::Unresponsive,
            issue: format!("Windows accepted the share request, but its share UI did not ask for the file within {} seconds.", DATA_REQUEST_TIMEOUT.as_secs()),
        }),
    }
}

#[cfg(target_os = "macos")]
async fn share_file_on_platform(path: PathBuf) -> Result<(), ShareFailure> {
    use std::cell::RefCell;
    use objc2::rc::Retained;
    use objc2::runtime::AnyObject;
    use objc2::{AnyThread, MainThreadMarker};
    use objc2_app_kit::{NSSharingServicePicker, NSView};
    use objc2_foundation::{NSArray, NSRect, NSRectEdge, NSString, NSURL};

    // AppKit does not retain the picker while its UI is shown. Without a strong reference of our
    // own, the picker would be deallocated right after showRelativeToRect and the share sheet
    // would close immediately. We create and replace the picker on the main thread only, hence a
    // thread-local reference is sufficient:
    thread_local! {
        static CURRENT_PICKER: RefCell<Option<Retained<NSSharingServicePicker>>> = const { RefCell::new(None) };
    }

    let window = crate::app_window::MAIN_WINDOW.lock().unwrap().clone()
        .ok_or_else(|| String::from("The main window is not available."))?;
    let ui_window = window.clone();
    let path = path.to_string_lossy().to_string();
    let (sender, receiver) = tokio::sync::oneshot::channel();

    window.run_on_main_thread(move || {
        let result = (|| -> Result<(), String> {
            // We create the NSView reference from a raw pointer below, which bypasses the
            // main-thread guarantee of objc2. Thus, we assert the main thread ourselves:
            let _mtm = MainThreadMarker::new().ok_or_else(|| String::from("The macOS share sheet must run on the main thread."))?;
            let path = NSString::from_str(&path);
            let url = NSURL::fileURLWithPath(&path);
            let item: Retained<AnyObject> = Retained::into_super(Retained::into_super(url));
            let items = NSArray::from_retained_slice(&[item]);

            // Safety: the items are NSURL instances, which conform to NSPasteboardWriting.
            let picker = unsafe { NSSharingServicePicker::initWithItems(NSSharingServicePicker::alloc(), &items) };
            let view = unsafe { &*ui_window.ns_view().map_err(|error| format!("Failed to get the native view: {error}"))?.cast::<NSView>() };
            picker.showRelativeToRect_ofView_preferredEdge(NSRect::ZERO, view, NSRectEdge::MinY);
            CURRENT_PICKER.with(|current| current.replace(Some(picker)));
            Ok(())
        })();
        let _ = sender.send(result);
    }).map_err(|error| format!("Failed to schedule the macOS share sheet: {error}"))?;

    receiver.await.map_err(|_| String::from("The macOS share sheet did not return a result."))?.map_err(ShareFailure::from)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn a_plain_error_text_counts_as_a_failure() {
        let share_failure = ShareFailure::from(String::from("The main window is not available."));

        assert_eq!(share_failure.outcome, ShareOutcome::Failed);
        assert_eq!(share_failure.issue, "The main window is not available.");
    }

    /// The app reads these values as ShareSheetOutcome. A renamed value would turn every answer into
    /// one the app does not know.
    #[test]
    fn the_response_names_the_outcome_as_the_app_reads_it() {
        let opened = serde_json::to_value(ShareFileResponse { outcome: ShareOutcome::Opened, issue: String::new() }).unwrap();
        let failed = serde_json::to_value(failure("The file path is empty.").0).unwrap();

        assert_eq!(opened, serde_json::json!({ "outcome": "opened", "issue": "" }));
        assert_eq!(failed, serde_json::json!({ "outcome": "failed", "issue": "The file path is empty." }));
    }

    #[cfg(windows)]
    #[test]
    fn an_unresponsive_share_ui_is_named_as_the_app_reads_it() {
        let unresponsive = serde_json::to_value(ShareFileResponse { outcome: ShareOutcome::Unresponsive, issue: String::new() }).unwrap();

        assert_eq!(unresponsive["outcome"], "unresponsive");
    }
}
