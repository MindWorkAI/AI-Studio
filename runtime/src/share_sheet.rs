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

#[cfg(target_os = "linux")]
async fn share_file_on_platform(path: PathBuf) -> Result<(), String> {
    crate::file_actions::open_existing_file_in_file_manager(path).await
}

#[cfg(windows)]
async fn share_file_on_platform(path: PathBuf) -> Result<(), String> {
    use windows::ApplicationModel::DataTransfer::{DataRequestedEventArgs, DataTransferManager};
    use windows::Foundation::TypedEventHandler;
    use windows::Storage::{IStorageItem, StorageFile};
    use windows::Win32::UI::Shell::IDataTransferManagerInterop;
    use windows::core::{factory, HSTRING, Interface};
    use windows_collections::IIterable;

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
            manager.DataRequested(&handler)
                .map_err(|error| format!("Failed to provide the shared file: {error}"))?;
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
    use objc2::MainThreadMarker;
    use objc2_app_kit::{NSRectEdge, NSSharingServicePicker, NSView};
    use objc2_foundation::{NSArray, NSRect, NSString, NSURL};

    let window = crate::app_window::MAIN_WINDOW.lock().unwrap().clone()
        .ok_or_else(|| String::from("The main window is not available."))?;
    let ui_window = window.clone();
    let path = path.to_string_lossy().to_string();
    let (sender, receiver) = tokio::sync::oneshot::channel();

    window.run_on_main_thread(move || {
        let result = (|| -> Result<(), String> {
            let mtm = MainThreadMarker::new().ok_or_else(|| String::from("The macOS share sheet must run on the main thread."))?;
            let path = NSString::from_str(&path);
            let url = NSURL::fileURLWithPath(&path);
            let items = NSArray::from_retained_slice(&[url]);
            let picker = NSSharingServicePicker::alloc(mtm).initWithItems(&items);
            let view = unsafe { &*ui_window.ns_view().map_err(|error| format!("Failed to get the native view: {error}"))?.cast::<NSView>() };
            picker.showRelativeToRect_ofView_preferredEdge(NSRect::ZERO, view, NSRectEdge::MinY);
            Ok(())
        })();
        let _ = sender.send(result);
    }).map_err(|error| format!("Failed to schedule the macOS share sheet: {error}"))?;

    receiver.await.map_err(|_| String::from("The macOS share sheet did not return a result."))?
}
