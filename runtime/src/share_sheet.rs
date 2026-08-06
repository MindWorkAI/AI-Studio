use axum::Json;
use log::{error, info};
use serde::{Deserialize, Serialize};
use std::path::PathBuf;
use crate::api_token::APIToken;

#[derive(Deserialize)]
pub struct ShareFileRequest {
    file_path: String,
}

#[derive(Serialize)]
pub struct ShareFileResponse {
    success: bool,
    issue: String,
}

pub async fn share_file(_token: APIToken, Json(request): Json<ShareFileRequest>) -> Json<ShareFileResponse> {
    let path = PathBuf::from(request.file_path.trim());
    if path.as_os_str().is_empty() {
        return failure("The file path is empty.");
    }

    if !path.is_file() {
        return failure(format!("The requested path is not an existing file: {}", path.to_string_lossy()));
    }

    let result = share_file_on_platform(path).await;
    match result {
        Ok(()) => {
            info!(Source = "Share sheet"; "Opened the native share UI.");
            Json(ShareFileResponse {
                success: true,
                issue: String::new(),
            })
        }

        Err(issue) => {
            error!(Source = "Share sheet"; "{issue}");
            failure(issue)
        }
    }
}

fn failure(issue: impl Into<String>) -> Json<ShareFileResponse> {
    Json(ShareFileResponse {
        success: false,
        issue: issue.into(),
    })
}

// Linux has no native share sheet: the XDG desktop portals do not provide a share interface. The
// app exports the file through the save dialog instead, hence this endpoint is not used on Linux:
#[cfg(target_os = "linux")]
async fn share_file_on_platform(_path: PathBuf) -> Result<(), String> {
    Err(String::from("The native share sheet is not available on Linux."))
}

#[cfg(windows)]
async fn share_file_on_platform(path: PathBuf) -> Result<(), String> {
    use std::cell::RefCell;
    use windows::ApplicationModel::DataTransfer::{DataRequestedEventArgs, DataTransferManager};
    use windows::Foundation::{EventRegistrationToken, TypedEventHandler};
    use windows::Storage::{IStorageItem, StorageFile};
    use windows::Win32::UI::Shell::IDataTransferManagerInterop;
    use windows::core::{factory, HSTRING, Interface};
    use windows_collections::IIterable;

    // The DataTransferManager belongs to the window, not to a single share. Registering a handler
    // for every share would stack them up, and each stale handler keeps pointing at the archive of
    // its own share, which gets cleaned up after a while. We therefore remember the registration
    // and remove the previous handler before adding a new one. We only ever register on the main
    // thread, hence a thread-local reference is sufficient:
    thread_local! {
        static DATA_REQUESTED_TOKEN: RefCell<Option<EventRegistrationToken>> = const { RefCell::new(None) };
    }

    let window = crate::app_window::MAIN_WINDOW.lock().unwrap().clone()
        .ok_or_else(|| String::from("The main window is not available."))?;
    let ui_window = window.clone();
    let path = path.to_string_lossy().to_string();
    let (sender, receiver) = tokio::sync::oneshot::channel();

    window.run_on_main_thread(move || {
        let result = (|| -> Result<(), String> {
            let hwnd = ui_window.hwnd().map_err(|error| format!("Failed to get the native window handle: {error}"))?;
            let interop: IDataTransferManagerInterop = factory::<DataTransferManager, IDataTransferManagerInterop>()
                .map_err(|error| format!("Failed to access the Windows share service: {error}"))?;
            let manager: DataTransferManager = unsafe { interop.GetForWindow(hwnd) }
                .map_err(|error| format!("Failed to create the Windows share request: {error}"))?;
            let handler = TypedEventHandler::<DataTransferManager, DataRequestedEventArgs>::new(move |_, arguments| {
                let Some(arguments) = arguments.as_ref() else {
                    return Ok(());
                };

                let request = arguments.Request()?;
                let file = StorageFile::GetFileFromPathAsync(&HSTRING::from(&path))?.get()?;
                let file: IStorageItem = file.cast()?;
                let items = IIterable::<IStorageItem>::from(vec![Some(file)]);
                request.Data()?.Properties()?.SetTitle(&HSTRING::from("MindWork AI Studio"))?;
                request.Data()?.SetStorageItemsReadOnly(&items)?;
                Ok(())
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

    receiver.await.map_err(|_| String::from("The Windows share sheet did not return a result."))?
}

#[cfg(target_os = "macos")]
async fn share_file_on_platform(path: PathBuf) -> Result<(), String> {
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

    receiver.await.map_err(|_| String::from("The macOS share sheet did not return a result."))?
}
