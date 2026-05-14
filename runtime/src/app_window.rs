use std::collections::HashMap;
use std::convert::Infallible;
use std::sync::Mutex;
use std::time::Duration;
use async_stream::stream;
use axum::body::Body;
use axum::http::header::CONTENT_TYPE;
use axum::response::{IntoResponse, Response};
use axum::Json;
use bytes::Bytes;
use log::{debug, error, info, trace, warn};
use once_cell::sync::Lazy;
use serde::{Deserialize, Serialize};
use strum_macros::Display;
use tauri::{DragDropEvent,RunEvent, Manager, WindowEvent, generate_context};
use tauri::path::PathResolver;
use tauri::WebviewWindow;
use tauri_plugin_updater::{UpdaterExt, Update};
use tauri_plugin_global_shortcut::GlobalShortcutExt;
use tauri_plugin_opener::OpenerExt;
use tokio::sync::broadcast;
use tokio::time;
use crate::api_token::APIToken;
use crate::dotnet::{cleanup_dotnet_server, start_dotnet_server, stop_dotnet_server};
use crate::environment::{is_prod, is_dev, CONFIG_DIRECTORY, DATA_DIRECTORY};
use crate::log::switch_to_file_logging;
use crate::pdfium::PDFIUM_LIB_PATH;
use crate::qdrant::{cleanup_qdrant, start_qdrant_server, stop_qdrant_server};
#[cfg(debug_assertions)]
use crate::dotnet::create_startup_env_file;

/// The Tauri main window.
pub static MAIN_WINDOW: Lazy<Mutex<Option<WebviewWindow>>> = Lazy::new(|| Mutex::new(None));

/// The update response coming from the Tauri updater.
static CHECK_UPDATE_RESPONSE: Lazy<Mutex<Option<Update>>> = Lazy::new(|| Mutex::new(None));

/// The event broadcast sender for Tauri events.
static EVENT_BROADCAST: Lazy<Mutex<Option<broadcast::Sender<Event>>>> = Lazy::new(|| Mutex::new(None));

/// Stores the currently registered global shortcuts (name -> shortcut string).
static REGISTERED_SHORTCUTS: Lazy<Mutex<HashMap<Shortcut, String>>> = Lazy::new(|| Mutex::new(HashMap::new()));

/// Stores the localhost origin of the Blazor app after the .NET server is ready.
static APPROVED_APP_URL: Lazy<Mutex<Option<tauri::Url>>> = Lazy::new(|| Mutex::new(None));

/// Enum identifying global keyboard shortcuts.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize, Display)]
#[strum(serialize_all = "SCREAMING_SNAKE_CASE")]
pub enum Shortcut {
    None = 0,
    VoiceRecordingToggle,
}

/// Starts the Tauri app.
pub fn start_tauri() {
    info!("Starting Tauri app...");

    // Create the event broadcast channel:
    let (event_sender, root_event_receiver) = broadcast::channel(100);

    // Save a copy of the event broadcast sender for later use:
    *EVENT_BROADCAST.lock().unwrap() = Some(event_sender.clone());

    // When the last receiver is dropped, we lose the ability to send events.
    // Therefore, we spawn a task that keeps the root receiver alive:
    tauri::async_runtime::spawn(async move {
        let mut root_receiver = root_event_receiver;
        loop {
            match root_receiver.recv().await {
                Ok(event) => {
                    debug!(Source = "Tauri"; "Tauri event received: location=root receiver       , event={event:?}");
                },

                Err(broadcast::error::RecvError::Lagged(skipped)) => {
                    warn!(Source = "Tauri"; "Root event receiver lagged, skipped {skipped} messages.");
                },

                Err(broadcast::error::RecvError::Closed) => {
                    warn!(Source = "Tauri"; "Root event receiver channel closed.");
                    return;
                },
            }
        }
    });

    let app = tauri::Builder::default()
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_shell::init())
        .plugin(tauri_plugin_opener::init())
        .plugin(
            tauri::plugin::Builder::<tauri::Wry, ()>::new("external-link-handler")
                .on_navigation(|webview, url| {
                    if !should_open_in_system_browser(webview, url) {
                        return true;
                    }

                    match webview.app_handle().opener().open_url(url.as_str(), None::<&str>) {
                        Ok(_) => {
                            info!(Source = "Tauri"; "Opening external URL in system browser: {url}");
                        },
                        Err(error) => {
                            error!(Source = "Tauri"; "Failed to open external URL '{url}' in system browser: {error}");
                        },
                    }
                    false
                })
                .build(),
        )
        .plugin(tauri_plugin_global_shortcut::Builder::new().build())
        .plugin(tauri_plugin_updater::Builder::new().build())
        .setup(move |app| {

            // Get the main window:
            let window = app.get_webview_window("main").expect("Failed to get main window.");

            // Register a callback for window events, such as file drops. We have to use
            // this handler in addition to the app event handler, because file drop events
            // are only available in the window event handler (is a bug, cf. https://github.com/tauri-apps/tauri/issues/14338):
            window.on_window_event(move |event| {
                debug!(Source = "Tauri"; "Tauri event received: location=window event handler, event={event:?}");
                let event_to_send = Event::from_window_event(event);
                let sender = event_sender.clone();
                tauri::async_runtime::spawn(async move {
                    match sender.send(event_to_send) {
                        Ok(_) => {},
                        Err(error) => error!(Source = "Tauri"; "Failed to channel window event: {error}"),
                    }
                });
            });

            // Save the main window for later access:
            *MAIN_WINDOW.lock().unwrap() = Some(window);

            info!(Source = "Bootloader Tauri"; "Setup is running.");
            let data_path = app.path().app_local_data_dir().unwrap();
            let data_path = data_path.join("data");

            // Get and store the data and config directories:
            DATA_DIRECTORY.set(data_path.to_str().unwrap().to_string()).map_err(|_| error!("Was not able to set the data directory.")).unwrap();
            CONFIG_DIRECTORY.set(app.path().app_config_dir().unwrap().to_str().unwrap().to_string()).map_err(|_| error!("Was not able to set the config directory.")).unwrap();

            if is_dev() {
                #[cfg(debug_assertions)]
                create_startup_env_file();
            } else {
                cleanup_dotnet_server();
                start_dotnet_server(app.handle().clone());
            }

            cleanup_qdrant();
            start_qdrant_server(app.handle().clone());

            info!(Source = "Bootloader Tauri"; "Reconfigure the file logger to use the app data directory {data_path:?}");
            switch_to_file_logging(data_path).map_err(|e| error!("Failed to switch logging to file: {e}")).unwrap();
            set_pdfium_path(app.path());

            Ok(())
        })
        .plugin(tauri_plugin_window_state::Builder::default().build())
        .build(generate_context!())
        .expect("Error while running Tauri application");

    // The app event handler:
    app.run(|_app_handle, event| {
        if !matches!(event, RunEvent::MainEventsCleared) {
            debug!(Source = "Tauri"; "Tauri event received: location=app event handler   , event={event:?}");
        }

        match event {
            RunEvent::WindowEvent { event, label, .. } => {
                match event {
                    WindowEvent::CloseRequested { .. } => {
                        warn!(Source = "Tauri"; "Window '{label}': close was requested.");
                    }

                    WindowEvent::Destroyed => {
                        warn!(Source = "Tauri"; "Window '{label}': was destroyed.");
                    }

                    _ => (),
                }
            }

            RunEvent::ExitRequested { .. } => {
                warn!(Source = "Tauri"; "Run event: exit was requested.");
                stop_qdrant_server();
                if is_prod() {
                    warn!("Try to stop the .NET server as well...");
                    stop_dotnet_server();
                }
            }

            RunEvent::Ready => {
                info!(Source = "Tauri"; "Run event: Tauri app is ready.");
            }

            _ => {}
        }
    });

    warn!(Source = "Tauri"; "Tauri app was stopped.");
}

fn is_local_host(host: Option<&str>) -> bool {
    matches!(host, Some("localhost") | Some("127.0.0.1") | Some("::1") | Some("[::1]"))
}

fn is_tauri_asset_host(host: Option<&str>) -> bool {
    matches!(host, Some("tauri.localhost"))
}

fn is_tauri_asset_url(url: &tauri::Url) -> bool {
    matches!(url.scheme(), "http" | "https") && is_tauri_asset_host(url.host_str())
}

fn is_local_http_url(url: &tauri::Url) -> bool {
    matches!(url.scheme(), "http" | "https") && is_local_host(url.host_str())
}

fn same_origin(left: &tauri::Url, right: &tauri::Url) -> bool {
    left.scheme() == right.scheme()
        && left.host_str() == right.host_str()
        && left.port_or_known_default() == right.port_or_known_default()
}

fn should_open_in_system_browser<R: tauri::Runtime>(webview: &tauri::Webview<R>, url: &tauri::Url) -> bool {
    match url.scheme() {
        "mailto" | "tel" => return true,
        "http" | "https" => {},
        _ => return false,
    }

    if is_tauri_asset_url(url) {
        return false;
    }

    if let Some(approved_app_url) = APPROVED_APP_URL.lock().unwrap().as_ref() {
        if same_origin(approved_app_url, url) {
            return false;
        }

        if is_local_http_url(url) {
            return true;
        }
    }

    if let Ok(current_url) = webview.url() {
        if same_origin(&current_url, url) {
            return false;
        }
    }

    !is_local_host(url.host_str())
}

/// Our event API endpoint for Tauri events. We try to send an endless stream of events to the client.
/// If no events are available for a certain time, we send a ping event to keep the connection alive.
/// When the client disconnects, the stream is closed. But we try to not lose events in between.
/// The client is expected to reconnect automatically when the connection is closed and continue
/// listening for events.
pub async fn get_event_stream(_token: APIToken) -> Response {
    // Get the lock to the event broadcast sender:
    let event_broadcast_lock = EVENT_BROADCAST.lock().unwrap();

    // Get and subscribe to the event receiver:
    let mut event_receiver = event_broadcast_lock.as_ref()
        .expect("Event sender not initialized.")
        .subscribe();

    // Drop the lock to allow other access to the sender:
    drop(event_broadcast_lock);

    let stream = stream! {
        loop {
            // Wait at most 3 seconds for an event:
            match time::timeout(Duration::from_secs(3), event_receiver.recv()).await {

                // Case: we received an event
                Ok(Ok(event)) => {
                    // Serialize the event to JSON. Important is that the entire event
                    // is serialized as a single line so that the client can parse it
                    // correctly:
                    let event_json = serde_json::to_string(&event).unwrap();
                    yield Ok::<Bytes, Infallible>(Bytes::from(event_json));

                    // The client expects a newline after each event because we are using
                    // a method to read the stream line-by-line:
                    yield Ok::<Bytes, Infallible>(Bytes::from("\n"));
                },

                // Case: we lagged behind and missed some events
                Ok(Err(broadcast::error::RecvError::Lagged(skipped))) => {
                    warn!(Source = "Tauri"; "Event receiver lagged, skipped {skipped} messages.");
                },

                // Case: the event channel was closed
                Ok(Err(broadcast::error::RecvError::Closed)) => {
                    warn!(Source = "Tauri"; "Event receiver channel closed.");
                    return;
                },

                // Case: timeout. We will send a ping event to keep the connection alive.
                Err(_) => {
                    let ping_event = Event::new(TauriEventType::Ping, Vec::new());

                    // Again, we have to serialize the event as a single line:
                    let event_json = serde_json::to_string(&ping_event).unwrap();
                    yield Ok::<Bytes, Infallible>(Bytes::from(event_json));

                    // The client expects a newline after each event because we are using
                    // a method to read the stream line-by-line:
                    yield Ok::<Bytes, Infallible>(Bytes::from("\n"));
                },
            }
        }
    };

    ([(CONTENT_TYPE, "application/jsonl")], Body::from_stream(stream)).into_response()
}

/// Data structure representing a Tauri event for our event API.
#[derive(Debug, Clone, Serialize)]
pub struct Event {
    pub event_type: TauriEventType,
    pub payload: Vec<String>,
}

/// Implementation of the Event struct.
impl Event {

    /// Creates a new Event instance.
    pub fn new(event_type: TauriEventType, payload: Vec<String>) -> Self {
        Event {
            payload,
            event_type,
        }
    }

    /// Creates an Event instance from a Tauri WindowEvent.
    pub fn from_window_event(window_event: &WindowEvent) -> Self {
        match window_event {
            WindowEvent::DragDrop(drop_event) => {
                match drop_event {
                    DragDropEvent::Enter { paths, .. } => Event::new(
                        TauriEventType::FileDropHovered,
                        paths.iter().map(|p| p.display().to_string()).collect(),
                    ),

                    DragDropEvent::Drop { paths, .. } => Event::new(
                        TauriEventType::FileDropDropped,
                        paths.iter().map(|p| p.display().to_string()).collect(),
                    ),

                    DragDropEvent::Leave => Event::new(TauriEventType::FileDropCanceled, Vec::new()),

                    _ => Event::new(TauriEventType::Unknown, Vec::new()),
                }
            },

            WindowEvent::Focused(state) => if *state {
                Event::new(TauriEventType::WindowFocused,
                           Vec::new(),
                )
            } else {
                Event::new(TauriEventType::WindowNotFocused,
                           Vec::new(),
                )
            },

            _ => Event::new(TauriEventType::Unknown,
                            Vec::new(),
            ),
        }
    }
}

/// The types of Tauri events we can send through our event API.
#[derive(Debug, Serialize, Clone)]
pub enum TauriEventType {
    None,
    Ping,
    Unknown,

    WindowFocused,
    WindowNotFocused,

    FileDropHovered,
    FileDropDropped,
    FileDropCanceled,

    GlobalShortcutPressed,
}

/// Changes the location of the main window to the given URL.
pub async fn change_location_to(url: &str) {
    // Try to get the main window. If it is not available yet, wait for it:
    let mut main_window_ready = false;
    let mut main_window_status_reported = false;
    let main_window_spawn_clone = &MAIN_WINDOW;
    while !main_window_ready
    {
        main_window_ready = {
            let main_window = main_window_spawn_clone.lock().unwrap();
            main_window.is_some()
        };

        if !main_window_ready {
            if !main_window_status_reported {
                info!("Waiting for main window to be ready, because .NET was faster than Tauri.");
                main_window_status_reported = true;
            }

            time::sleep(Duration::from_millis(100)).await;
        }
    }

    if let Ok(parsed_url) = tauri::Url::parse(url) {
        if is_local_http_url(&parsed_url) {
            *APPROVED_APP_URL.lock().unwrap() = Some(parsed_url);
        }
    }

    let js_location_change = format!("window.location = '{url}';");
    let main_window = main_window_spawn_clone.lock().unwrap();
    let location_change_result = main_window.as_ref().unwrap().eval(js_location_change.as_str());
    match location_change_result {
        Ok(_) => info!("The app location was changed to {url}."),
        Err(e) => error!("Failed to change the app location to {url}: {e}."),
    }
}

/// Checks for updates.
pub async fn check_for_update(_token: APIToken) -> Json<CheckUpdateResponse> {
    if is_dev() {
        warn!(Source = "Updater"; "The app is running in development mode; skipping update check.");
        return Json(CheckUpdateResponse {
            update_is_available: false,
            error: false,
            new_version: String::from(""),
            changelog: String::from(""),
        });
    }

    let app_handle = {
        let main_window = MAIN_WINDOW.lock().unwrap();
        match main_window.as_ref() {
            Some(window) => window.app_handle().clone(),
            None => {
                error!(Source = "Updater"; "Cannot check updates: main window not available.");
                return Json(CheckUpdateResponse {
                    update_is_available: false,
                    error: true,
                    new_version: String::from(""),
                    changelog: String::from(""),
                });
            }
        }
    };
    let response = match app_handle.updater() {
        Ok(updater) => updater.check().await,
        Err(e) => {
            warn!(Source = "Updater"; "Failed to get updater instance: {e}");
            return Json(CheckUpdateResponse {
                update_is_available: false,
                error: true,
                new_version: String::from(""),
                changelog: String::from(""),
            });
        }
    };

    match response {
        Ok(Some(update)) => {
            let body_len = update.body.as_ref().map_or(0, |body| body.len());
            let date = update.date;
            let new_version = update.version.clone();
            info!(Source = "Tauri"; "Updater: update available: body size={body_len} time={date:?} version={new_version}");
            let changelog = update.body.clone().unwrap_or_default();
            *CHECK_UPDATE_RESPONSE.lock().unwrap() = Some(update);
            Json(CheckUpdateResponse {
                update_is_available: true,
                error: false,
                new_version,
                changelog,
            })
        }
        Ok(None) => {
            info!(Source = "Tauri"; "Updater: app is already up to date");
            Json(CheckUpdateResponse {
                update_is_available: false,
                error: false,
                new_version: String::from(""),
                changelog: String::from(""),
            })
        }
        Err(e) => {
            warn!(Source = "Tauri"; "Updater: failed to update: {e}");
            Json(CheckUpdateResponse {
                update_is_available: false,
                error: true,
                new_version: String::from(""),
                changelog: String::from(""),
            })
        }
    }
}

/// The response to the check for update request.
#[derive(Serialize)]
pub struct CheckUpdateResponse {
    update_is_available: bool,
    error: bool,
    new_version: String,
    changelog: String,
}

/// Installs the update.
pub async fn install_update(_token: APIToken) {
    if is_dev() {
        warn!(Source = "Updater"; "The app is running in development mode; skipping update installation.");
        return;
    }

    let cloned_response_option = CHECK_UPDATE_RESPONSE.lock().unwrap().clone();
    let app_handle = MAIN_WINDOW
        .lock()
        .unwrap()
        .as_ref()
        .map(|window| window.app_handle().clone());

    match cloned_response_option {
        Some(update_response) => {
            info!(Source = "Tauri"; "Updater: update is pending!");
            let result = update_response.download_and_install(
                |chunk_length, _content_length| {
                    trace!(Source = "Tauri"; "Updater: downloading chunk of {chunk_length} bytes");
                },
                || {
                    info!(Source = "Tauri"; "Updater: update has been downloaded!");
                    warn!(Source = "Tauri"; "Try to stop the .NET server now...");

                    if is_prod() {
                        stop_dotnet_server();
                        stop_qdrant_server();
                    } else {
                        warn!(Source = "Tauri"; "Development environment detected; do not stop the .NET server.");
                    }
                },
            ).await;

            match result {
                Ok(_) => {
                    info!(Source = "Tauri"; "Updater: app has been updated");
                    warn!(Source = "Tauri"; "Try to restart the app now...");

                    if is_prod() {
                        if let Some(handle) = app_handle {
                            handle.restart();
                        } else {
                            warn!(Source = "Tauri"; "Cannot restart after update: main window not available.");
                        }
                    } else {
                        warn!(Source = "Tauri"; "Development environment detected; do not restart the app.");
                    }
                }
                Err(e) => {
                    warn!(Source = "Tauri"; "Updater: failed to update: {e}");
                }
            }
        },

        None => {
            error!(Source = "Updater"; "No update available to install. Did you check for updates first?");
        },
    }
}

/// Request payload for registering a global shortcut.
#[derive(Clone, Deserialize)]
pub struct RegisterShortcutRequest {
    /// The shortcut ID to use.
    id: Shortcut,

    /// The shortcut string in Tauri format (e.g., "CmdOrControl+1").
    /// Use empty string to unregister the shortcut.
    shortcut: String,
}

/// Response for shortcut registration.
#[derive(Serialize)]
pub struct ShortcutResponse {
    success: bool,
    error_message: String,
}

/// Response for application exit requests.
#[derive(Serialize)]
pub struct AppExitResponse {
    success: bool,
    error_message: String,
}

/// Internal helper function to register a shortcut with its callback.
/// This is used by both `register_shortcut` and `resume_shortcuts` to
/// avoid code duplication.
fn register_shortcut_with_callback<R: tauri::Runtime>(
    app_handle: &tauri::AppHandle<R>,
    shortcut: &str,
    shortcut_id: Shortcut,
    event_sender: broadcast::Sender<Event>,
) -> Result<(), tauri_plugin_global_shortcut::Error> {
    let shortcut_manager = app_handle.global_shortcut();
    shortcut_manager.on_shortcut(shortcut, move |_app, _shortcut, _event| {
        info!(Source = "Tauri"; "Global shortcut triggered for '{}'.", shortcut_id);
        let event = Event::new(TauriEventType::GlobalShortcutPressed, vec![shortcut_id.to_string()]);
        let sender = event_sender.clone();
        tauri::async_runtime::spawn(async move {
            if let Err(error) = sender.send(event) {
                error!(Source = "Tauri"; "Failed to send global shortcut event: {error}");
            }
        });
    })
}

/// Requests a controlled shutdown of the entire desktop application.
pub async fn exit_app(_token: APIToken) -> Json<AppExitResponse> {
    let app_handle = {
        let main_window_lock = MAIN_WINDOW.lock().unwrap();
        match main_window_lock.as_ref() {
            Some(window) => window.app_handle().clone(),
            None => {
                error!(Source = "Tauri"; "Cannot exit app: main window not available.");
                return Json(AppExitResponse {
                    success: false,
                    error_message: "Main window not available".to_string(),
                });
            }
        }
    };

    info!(Source = "Tauri"; "Controlled app exit was requested by the UI.");
    tauri::async_runtime::spawn(async move {
        time::sleep(Duration::from_millis(50)).await;
        app_handle.exit(0);
    });

    Json(AppExitResponse {
        success: true,
        error_message: String::new(),
    })
}

/// Registers or updates a global shortcut. If the shortcut string is empty,
/// the existing shortcut for that name will be unregistered.
pub async fn register_shortcut(_token: APIToken, payload: Json<RegisterShortcutRequest>) -> Json<ShortcutResponse> {
    let id = payload.id;
    let new_shortcut = payload.shortcut.clone();

    if id == Shortcut::None {
        error!(Source = "Tauri"; "Cannot register NONE shortcut.");
        return Json(ShortcutResponse {
            success: false,
            error_message: "Cannot register NONE shortcut".to_string(),
        });
    }

    info!(Source = "Tauri"; "Registering global shortcut '{}' with key '{new_shortcut}'.", id);

    // Get the main window to access the global shortcut manager:
    let main_window_lock = MAIN_WINDOW.lock().unwrap();
    let main_window = match main_window_lock.as_ref() {
        Some(window) => window,
        None => {
            error!(Source = "Tauri"; "Cannot register shortcut: main window not available.");
            return Json(ShortcutResponse {
                success: false,
                error_message: "Main window not available".to_string(),
            });
        }
    };

    let app_handle = main_window.app_handle();
    let shortcut_manager = app_handle.global_shortcut();
    let mut registered_shortcuts = REGISTERED_SHORTCUTS.lock().unwrap();

    // Unregister the old shortcut if one exists for this name:
    if let Some(old_shortcut) = registered_shortcuts.get(&id) {
        if !old_shortcut.is_empty() {
            match shortcut_manager.unregister(old_shortcut.as_str()) {
                Ok(_) => info!(Source = "Tauri"; "Unregistered old shortcut '{old_shortcut}' for '{}'.", id),
                Err(error) => warn!(Source = "Tauri"; "Failed to unregister old shortcut '{old_shortcut}': {error}"),
            }
        }
    }

    // When the new shortcut is empty, we're done (just unregistering):
    if new_shortcut.is_empty() {
        registered_shortcuts.remove(&id);
        info!(Source = "Tauri"; "Shortcut '{}' has been disabled.", id);
        return Json(ShortcutResponse {
            success: true,
            error_message: String::new(),
        });
    }

    // Get the event broadcast sender for the shortcut callback:
    let event_broadcast_lock = EVENT_BROADCAST.lock().unwrap();
    let event_sender = match event_broadcast_lock.as_ref() {
        Some(sender) => sender.clone(),
        None => {
            error!(Source = "Tauri"; "Cannot register shortcut: event broadcast not initialized.");
            return Json(ShortcutResponse {
                success: false,
                error_message: "Event broadcast not initialized".to_string(),
            });
        }
    };

    drop(event_broadcast_lock);

    // Register the new shortcut:
    match register_shortcut_with_callback(app_handle, &new_shortcut, id, event_sender) {
        Ok(_) => {
            info!(Source = "Tauri"; "Global shortcut '{new_shortcut}' registered successfully for '{}'.", id);
            registered_shortcuts.insert(id, new_shortcut);
            Json(ShortcutResponse {
                success: true,
                error_message: String::new(),
            })
        },

        Err(error) => {
            let error_msg = format!("Failed to register shortcut: {error}");
            error!(Source = "Tauri"; "{error_msg}");
            Json(ShortcutResponse {
                success: false,
                error_message: error_msg,
            })
        }
    }
}

/// Request payload for validating a shortcut.
#[derive(Clone, Deserialize)]
pub struct ValidateShortcutRequest {
    /// The shortcut string to validate (e.g., "CmdOrControl+1").
    shortcut: String,
}

/// Response for shortcut validation.
#[derive(Serialize)]
pub struct ShortcutValidationResponse {
    is_valid: bool,
    error_message: String,
    has_conflict: bool,
    conflict_description: String,
}

/// Validates a shortcut string without registering it.
/// Checks if the shortcut syntax is valid and if it
/// conflicts with existing shortcuts.
pub async fn validate_shortcut(_token: APIToken, payload: Json<ValidateShortcutRequest>) -> Json<ShortcutValidationResponse> {
    let shortcut = payload.shortcut.clone();

    // Empty shortcuts are always valid (means "disabled"):
    if shortcut.is_empty() {
        return Json(ShortcutValidationResponse {
            is_valid: true,
            error_message: String::new(),
            has_conflict: false,
            conflict_description: String::new(),
        });
    }

    // Check if the shortcut is already registered:
    let registered_shortcuts = REGISTERED_SHORTCUTS.lock().unwrap();
    for (name, registered_shortcut) in registered_shortcuts.iter() {
        if registered_shortcut.eq_ignore_ascii_case(&shortcut) {
            return Json(ShortcutValidationResponse {
                is_valid: true,
                error_message: String::new(),
                has_conflict: true,
                conflict_description: format!("Already used by: {}", name),
            });
        }
    }

    drop(registered_shortcuts);

    // Try to parse the shortcut to validate syntax.
    // We can't easily validate without registering in Tauri 1.x,
    // so we do basic syntax validation here:
    let is_valid = validate_shortcut_syntax(&shortcut);

    if is_valid {
        Json(ShortcutValidationResponse {
            is_valid: true,
            error_message: String::new(),
            has_conflict: false,
            conflict_description: String::new(),
        })
    } else {
        Json(ShortcutValidationResponse {
            is_valid: false,
            error_message: format!("Invalid shortcut syntax: {}", shortcut),
            has_conflict: false,
            conflict_description: String::new(),
        })
    }
}

/// Suspends shortcut processing by unregistering all shortcuts from the OS.
/// The shortcuts remain in our internal map, so they can be re-registered on resume.
/// This is useful when opening a dialog to configure shortcuts, so the user can
/// press the current shortcut to re-enter it without triggering the action.
pub async fn suspend_shortcuts(_token: APIToken) -> Json<ShortcutResponse> {
    // Get the main window to access the global shortcut manager:
    let main_window_lock = MAIN_WINDOW.lock().unwrap();
    let main_window = match main_window_lock.as_ref() {
        Some(window) => window,
        None => {
            error!(Source = "Tauri"; "Cannot suspend shortcuts: main window not available.");
            return Json(ShortcutResponse {
                success: false,
                error_message: "Main window not available".to_string(),
            });
        }
    };

    let app_handle = main_window.app_handle();
    let shortcut_manager = app_handle.global_shortcut();
    let registered_shortcuts = REGISTERED_SHORTCUTS.lock().unwrap();

    // Unregister all shortcuts from the OS (but keep them in our map):
    for (name, shortcut) in registered_shortcuts.iter() {
        if !shortcut.is_empty() {
            match shortcut_manager.unregister(shortcut.as_str()) {
                Ok(_) => info!(Source = "Tauri"; "Temporarily unregistered shortcut '{shortcut}' for '{}'.", name),
                Err(error) => warn!(Source = "Tauri"; "Failed to unregister shortcut '{shortcut}' for '{}': {error}", name),
            }
        }
    }

    info!(Source = "Tauri"; "Shortcut processing has been suspended ({} shortcuts unregistered).", registered_shortcuts.len());
    Json(ShortcutResponse {
        success: true,
        error_message: String::new(),
    })
}

/// Resumes shortcut processing by re-registering all shortcuts with the OS.
pub async fn resume_shortcuts(_token: APIToken) -> Json<ShortcutResponse> {
    // Get the main window to access the global shortcut manager:
    let main_window_lock = MAIN_WINDOW.lock().unwrap();
    let main_window = match main_window_lock.as_ref() {
        Some(window) => window,
        None => {
            error!(Source = "Tauri"; "Cannot resume shortcuts: main window not available.");
            return Json(ShortcutResponse {
                success: false,
                error_message: "Main window not available".to_string(),
            });
        }
    };

    let app_handle = main_window.app_handle();
    let registered_shortcuts = REGISTERED_SHORTCUTS.lock().unwrap();

    // Get the event broadcast sender for the shortcut callbacks:
    let event_broadcast_lock = EVENT_BROADCAST.lock().unwrap();
    let event_sender = match event_broadcast_lock.as_ref() {
        Some(sender) => sender.clone(),
        None => {
            error!(Source = "Tauri"; "Cannot resume shortcuts: event broadcast not initialized.");
            return Json(ShortcutResponse {
                success: false,
                error_message: "Event broadcast not initialized".to_string(),
            });
        }
    };

    drop(event_broadcast_lock);

    // Re-register all shortcuts with the OS:
    let mut success_count = 0;
    for (shortcut_id, shortcut) in registered_shortcuts.iter() {
        if shortcut.is_empty() {
            continue;
        }

        match register_shortcut_with_callback(app_handle, shortcut, *shortcut_id, event_sender.clone()) {
            Ok(_) => {
                info!(Source = "Tauri"; "Re-registered shortcut '{shortcut}' for '{}'.", shortcut_id);
                success_count += 1;
            },

            Err(error) => warn!(Source = "Tauri"; "Failed to re-register shortcut '{shortcut}' for '{}': {error}", shortcut_id),
        }
    }

    info!(Source = "Tauri"; "Shortcut processing has been resumed ({success_count} shortcuts re-registered).");
    Json(ShortcutResponse {
        success: true,
        error_message: String::new(),
    })
}

/// Validates the syntax of a shortcut string.
fn validate_shortcut_syntax(shortcut: &str) -> bool {
    let parts: Vec<&str> = shortcut.split('+').collect();
    if parts.is_empty() {
        return false;
    }

    let mut has_key = false;
    for part in parts {
        let part_lower = part.to_lowercase();
        match part_lower.as_str() {
            // Modifiers
            "cmdorcontrol" | "commandorcontrol" | "ctrl" | "control" | "cmd" | "command" |
            "shift" | "alt" | "meta" | "super" | "option" => continue,

            // Keys - letters
            "a" | "b" | "c" | "d" | "e" | "f" | "g" | "h" | "i" | "j" | "k" | "l" | "m" |
            "n" | "o" | "p" | "q" | "r" | "s" | "t" | "u" | "v" | "w" | "x" | "y" | "z" => has_key = true,

            // Keys - numbers
            "0" | "1" | "2" | "3" | "4" | "5" | "6" | "7" | "8" | "9" => has_key = true,

            // Keys - function keys
            _ if part_lower.starts_with('f') && part_lower[1..].parse::<u32>().is_ok() => has_key = true,

            // Keys - special
            "space" | "enter" | "tab" | "escape" | "backspace" | "delete" | "insert" |
            "home" | "end" | "pageup" | "pagedown" |
            "up" | "down" | "left" | "right" |
            "arrowup" | "arrowdown" | "arrowleft" | "arrowright" |
            "minus" | "equal" | "bracketleft" | "bracketright" | "backslash" |
            "semicolon" | "quote" | "backquote" | "comma" | "period" | "slash" => has_key = true,

            // Keys - numpad
            _ if part_lower.starts_with("num") => has_key = true,

            // Unknown
            _ => return false,
        }
    }

    has_key
}

fn set_pdfium_path<R: tauri::Runtime>(path_resolver: &PathResolver<R>) {
    let resource_dir = match path_resolver.resource_dir() {
        Ok(path) => path,
        Err(error) => {
            error!(Source = "Bootloader Tauri"; "Failed to resolve resource dir: {error}");
            return;
        }
    };

    let candidate_paths = [
        resource_dir.join("resources").join("libraries"),
        resource_dir.join("libraries"),
    ];

    let pdfium_source_path = candidate_paths
        .iter()
        .find(|path| path.exists())
        .map(|path| path.to_string_lossy().to_string());

    match pdfium_source_path {
        Some(path) => {
            *PDFIUM_LIB_PATH.lock().unwrap() = Some(path);
        }
        None => {
            error!(Source = "Bootloader Tauri"; "Failed to set the PDFium library path.");
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn tauri_localhost_is_tauri_asset_url() {
        let https_url = tauri::Url::parse("https://tauri.localhost/index.html").unwrap();
        let http_url = tauri::Url::parse("http://tauri.localhost/index.html").unwrap();

        assert!(is_tauri_asset_url(&https_url));
        assert!(is_tauri_asset_url(&http_url));
    }

    #[test]
    fn localhost_app_url_is_not_tauri_asset_url() {
        let url = tauri::Url::parse("http://localhost:12345/").unwrap();

        assert!(!is_tauri_asset_url(&url));
        assert!(is_local_http_url(&url));
    }

    #[test]
    fn external_url_is_not_internal_url() {
        let url = tauri::Url::parse("https://example.com/").unwrap();

        assert!(!is_tauri_asset_url(&url));
        assert!(!is_local_http_url(&url));
    }
}