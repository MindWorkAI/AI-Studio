#![cfg_attr(not(any(target_os = "linux", test)), allow(dead_code))]

use std::collections::HashMap;
use std::sync::atomic::{AtomicBool, Ordering};

#[cfg(target_os = "linux")]
use std::sync::atomic::AtomicU64;

use log::{error, info, warn};
use once_cell::sync::Lazy;
use serde::{Deserialize, Serialize};
use strum_macros::Display;
use tauri_plugin_global_shortcut::GlobalShortcutExt;
use tokio::sync::{Mutex, broadcast};

use crate::app_window::{Event, TauriEventType};

#[cfg(target_os = "linux")]
use ashpd::desktop::{CreateSessionOptions, ResponseError};
#[cfg(target_os = "linux")]
use ashpd::desktop::global_shortcuts::{
    BindShortcutsOptions, GlobalShortcuts, ListShortcutsOptions, NewShortcut,
};
#[cfg(target_os = "linux")]
#[cfg(target_os = "linux")]
use futures::StreamExt;

static SHORTCUT_MANAGER: Lazy<Mutex<ShortcutManager>> = Lazy::new(|| Mutex::new(ShortcutManager::default()));

static PROCESSING_SUSPENDED: AtomicBool = AtomicBool::new(false);

#[cfg(target_os = "linux")]
static NEXT_PORTAL_GENERATION: AtomicU64 = AtomicU64::new(1);

#[cfg(target_os = "linux")]
static ACTIVE_PORTAL_GENERATIONS: Lazy<std::sync::Mutex<HashMap<Shortcut, u64>>> = Lazy::new(|| std::sync::Mutex::new(HashMap::new()));

/// Enum identifying global keyboard shortcuts.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize, Display)]
#[strum(serialize_all = "SCREAMING_SNAKE_CASE")]
pub enum Shortcut {
    None = 0,
    VoiceRecordingToggle,
}

impl Shortcut {
    #[cfg(target_os = "linux")]
    fn from_portal_id(id: &str) -> Option<Self> {
        match id {
            "VOICE_RECORDING_TOGGLE" => Some(Self::VoiceRecordingToggle),
            _ => None,
        }
    }
}

/// Request payload for registering or disabling a global shortcut.
#[derive(Clone, Deserialize)]
pub struct RegisterShortcutRequest {
    pub id: Shortcut,
    pub shortcut: String,
    pub description: String,
    pub reconfigure: bool,
}

/// Backend used for a shortcut registration.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum ShortcutBackend {
    None,
    Portal,
    Tauri,
}

/// Response for shortcut registration and processing state changes.
#[derive(Serialize)]
pub struct ShortcutResponse {
    pub success: bool,
    pub error_message: String,
    pub backend: ShortcutBackend,
    pub cancelled: bool,
    pub effective_display_name: String,
}

impl ShortcutResponse {
    fn success(backend: ShortcutBackend, effective_display_name: String) -> Self {
        Self {
            success: true,
            error_message: String::new(),
            backend,
            cancelled: false,
            effective_display_name,
        }
    }

    fn error(error_message: impl Into<String>, backend: ShortcutBackend, cancelled: bool) -> Self {
        Self {
            success: false,
            error_message: error_message.into(),
            backend,
            cancelled,
            effective_display_name: String::new(),
        }
    }
}

#[derive(Default)]
struct ShortcutManager {
    bindings: HashMap<Shortcut, ActiveBinding>,
}

enum ActiveBinding {
    Tauri { shortcut: String },
    #[cfg(target_os = "linux")]
    Portal {
        shortcut: String,
        effective_display_name: String,
        generation: u64,
        session: ashpd::desktop::Session<GlobalShortcuts>,
    },
}

impl ActiveBinding {
    fn shortcut(&self) -> &str {
        match self {
            Self::Tauri { shortcut } => shortcut,
            #[cfg(target_os = "linux")]
            Self::Portal { shortcut, .. } => shortcut,
        }
    }

    fn backend(&self) -> ShortcutBackend {
        match self {
            Self::Tauri { .. } => ShortcutBackend::Tauri,
            #[cfg(target_os = "linux")]
            Self::Portal { .. } => ShortcutBackend::Portal,
        }
    }

    fn effective_display_name(&self) -> String {
        match self {
            Self::Tauri { shortcut } => shortcut.clone(),
            #[cfg(target_os = "linux")]
            Self::Portal { effective_display_name, .. } => effective_display_name.clone(),
        }
    }
}

pub async fn registered_shortcuts() -> Vec<(Shortcut, String)> {
    SHORTCUT_MANAGER
        .lock()
        .await
        .bindings
        .iter()
        .map(|(id, binding)| (*id, binding.shortcut().to_string()))
        .collect()
}

pub async fn register(
    app_handle: Option<tauri::AppHandle>,
    event_sender: Option<broadcast::Sender<Event>>,
    request: RegisterShortcutRequest,
) -> ShortcutResponse {
    if request.id == Shortcut::None {
        return ShortcutResponse::error("Cannot register NONE shortcut", ShortcutBackend::None, false);
    }

    let Some(app_handle) = app_handle else {
        return ShortcutResponse::error("Main window not available", ShortcutBackend::None, false);
    };
    let Some(event_sender) = event_sender else {
        return ShortcutResponse::error("Event broadcast not initialized", ShortcutBackend::None, false);
    };

    let mut manager = SHORTCUT_MANAGER.lock().await;
    if request.shortcut.is_empty() {
        return disable_binding(&app_handle, &mut manager, request.id).await;
    }

    if registration_is_unchanged(manager.bindings.get(&request.id).map(ActiveBinding::shortcut), &request.shortcut, request.reconfigure) {
        info!(Source = "Global shortcuts"; "Ignoring unchanged registration for '{}'.", request.id);
        let binding = manager.bindings.get(&request.id).unwrap();
        return ShortcutResponse::success(binding.backend(), binding.effective_display_name());
    }

    #[cfg(target_os = "linux")]
    {
        match prepare_portal_binding(&request, event_sender.clone()).await {
            Ok(new_binding) => {
                let effective_display_name = new_binding.effective_display_name();
                replace_portal_binding(&app_handle, &mut manager, request.id, new_binding).await;
                info!(Source = "XDG portal"; "Global shortcut '{}' is active through the desktop portal.", request.id);
                return ShortcutResponse::success(ShortcutBackend::Portal, effective_display_name);
            },

            Err(error) if may_fallback_to_tauri(
                error.kind,
                manager.bindings.get(&request.id).map(ActiveBinding::backend),
            ) => {
                warn!(Source = "XDG portal"; "Global shortcuts portal is unavailable; using the Tauri X11 backend: {}", error.message);
            },

            Err(error) => {
                let cancelled = error.kind == PortalFailureKind::Cancelled;
                if cancelled {
                    warn!(Source = "XDG portal"; "Global shortcut configuration was cancelled by the user.");
                } else if error.kind == PortalFailureKind::Denied {
                    warn!(Source = "XDG portal"; "Global shortcut permission was denied: {}", error.message);
                } else {
                    error!(Source = "XDG portal"; "Global shortcut registration failed: {}", error.message);
                }

                return ShortcutResponse::error(error.message, ShortcutBackend::Portal, cancelled);
            },
        }
    }

    match register_tauri_binding(&app_handle, &request.shortcut, request.id, event_sender) {
        Ok(()) => {
            if let Some(old_binding) = manager.bindings.remove(&request.id) {
                close_binding(&app_handle, request.id, old_binding).await;
            }

            manager.bindings.insert(request.id, ActiveBinding::Tauri { shortcut: request.shortcut.clone() });
            ShortcutResponse::success(ShortcutBackend::Tauri, request.shortcut)
        },

        Err(error) => ShortcutResponse::error(
            format!("Failed to register shortcut: {error}"),
            ShortcutBackend::Tauri,
            false,
        ),
    }
}

fn registration_is_unchanged(current: Option<&str>, requested: &str, reconfigure: bool) -> bool {
    current.is_some_and(|current| current.eq_ignore_ascii_case(requested)) && !reconfigure
}

async fn disable_binding(
    app_handle: &tauri::AppHandle,
    manager: &mut ShortcutManager,
    id: Shortcut,
) -> ShortcutResponse {
    if let Some(binding) = manager.bindings.remove(&id) {
        close_binding(app_handle, id, binding).await;
    }

    info!(Source = "Global shortcuts"; "Shortcut '{}' has been disabled.", id);
    ShortcutResponse::success(ShortcutBackend::None, String::new())
}

#[cfg(target_os = "linux")]
async fn replace_portal_binding(
    app_handle: &tauri::AppHandle,
    manager: &mut ShortcutManager,
    id: Shortcut,
    new_binding: ActiveBinding,
) {
    #[cfg(target_os = "linux")]
    if let ActiveBinding::Portal { generation, .. } = &new_binding {
        ACTIVE_PORTAL_GENERATIONS.lock().unwrap().insert(id, *generation);
    }

    let old_binding = manager.bindings.insert(id, new_binding);
    if let Some(old_binding) = old_binding {
        close_binding(app_handle, id, old_binding).await;
    }
}

async fn close_binding(app_handle: &tauri::AppHandle, id: Shortcut, binding: ActiveBinding) {
    match binding {
        ActiveBinding::Tauri { shortcut } => {
            if let Err(error) = app_handle.global_shortcut().unregister(shortcut.as_str()) {
                warn!(Source = "Tauri"; "Failed to unregister shortcut '{shortcut}' for '{}': {error}", id);
            }
        },

        #[cfg(target_os = "linux")]
        ActiveBinding::Portal { generation, session, .. } => {
            let is_still_active = ACTIVE_PORTAL_GENERATIONS.lock().unwrap().get(&id) == Some(&generation);
            if is_still_active {
                ACTIVE_PORTAL_GENERATIONS.lock().unwrap().remove(&id);
            }
            if let Err(error) = session.close().await {
                warn!(Source = "XDG portal"; "Failed to close portal session for '{}': {error}", id);
            }
        },
    }
}

fn register_tauri_binding(
    app_handle: &tauri::AppHandle,
    shortcut: &str,
    shortcut_id: Shortcut,
    event_sender: broadcast::Sender<Event>,
) -> Result<(), tauri_plugin_global_shortcut::Error> {
    app_handle.global_shortcut().on_shortcut(shortcut, move |_app, _shortcut, _event| {
        if PROCESSING_SUSPENDED.load(Ordering::Relaxed) {
            return;
        }

        send_shortcut_pressed(&event_sender, shortcut_id, "Tauri");
    })
}

fn send_shortcut_pressed(event_sender: &broadcast::Sender<Event>, shortcut_id: Shortcut, source: &str) {
    info!(Source = "Global shortcuts"; "Global shortcut triggered through {source} for '{}'.", shortcut_id);
    if let Err(error) = event_sender.send(Event::new(
        TauriEventType::GlobalShortcutPressed,
        vec![shortcut_id.to_string()],
    )) {
        error!(Source = "Global shortcuts"; "Failed to send global shortcut event: {error}");
    }
}

pub async fn suspend(app_handle: Option<tauri::AppHandle>) -> ShortcutResponse {
    PROCESSING_SUSPENDED.store(true, Ordering::Relaxed);
    let Some(app_handle) = app_handle else {
        PROCESSING_SUSPENDED.store(false, Ordering::Relaxed);
        return ShortcutResponse::error("Main window not available", ShortcutBackend::None, false);
    };

    let manager = SHORTCUT_MANAGER.lock().await;
    for (id, binding) in &manager.bindings {
        if unregister_backend_during_suspend(binding.backend())
            && let ActiveBinding::Tauri { shortcut } = binding
            && let Err(error) = app_handle.global_shortcut().unregister(shortcut.as_str())
        {
            warn!(Source = "Tauri"; "Failed to suspend shortcut '{shortcut}' for '{}': {error}", id);
        }
    }

    ShortcutResponse::success(ShortcutBackend::None, String::new())
}

pub async fn resume(
    app_handle: Option<tauri::AppHandle>,
    event_sender: Option<broadcast::Sender<Event>>,
) -> ShortcutResponse {
    let Some(app_handle) = app_handle else {
        return ShortcutResponse::error("Main window not available", ShortcutBackend::None, false);
    };

    let Some(event_sender) = event_sender else {
        return ShortcutResponse::error("Event broadcast not initialized", ShortcutBackend::None, false);
    };

    let manager = SHORTCUT_MANAGER.lock().await;
    for (id, binding) in &manager.bindings {
        if let ActiveBinding::Tauri { shortcut } = binding
            && let Err(error) = register_tauri_binding(&app_handle, shortcut, *id, event_sender.clone())
        {
            PROCESSING_SUSPENDED.store(false, Ordering::Relaxed);
            return ShortcutResponse::error(
                format!("Failed to resume shortcut: {error}"),
                ShortcutBackend::Tauri,
                false,
            );
        }
    }

    PROCESSING_SUSPENDED.store(false, Ordering::Relaxed);
    ShortcutResponse::success(ShortcutBackend::None, String::new())
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
#[cfg_attr(not(target_os = "linux"), allow(dead_code))]
enum PortalFailureKind {
    Unavailable,
    Cancelled,
    Denied,
    Technical,
}

fn may_fallback_to_tauri(failure: PortalFailureKind, current_backend: Option<ShortcutBackend>) -> bool {
    failure == PortalFailureKind::Unavailable && current_backend.is_none_or(|backend| backend == ShortcutBackend::Tauri)
}

fn unregister_backend_during_suspend(backend: ShortcutBackend) -> bool {
    backend == ShortcutBackend::Tauri
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum PortalBindingAction {
    Restore,
    Bind,
}

fn portal_binding_action(was_restored: bool, reconfigure: bool) -> PortalBindingAction {
    if was_restored && !reconfigure {
        PortalBindingAction::Restore
    } else {
        PortalBindingAction::Bind
    }
}

#[derive(Debug, Clone)]
#[cfg_attr(not(target_os = "linux"), allow(dead_code))]
struct PortalFailure {
    kind: PortalFailureKind,
    message: String,
}

impl PortalFailure {
    #[cfg(target_os = "linux")]
    fn from_error(error: ashpd::Error) -> Self {
        let kind = match &error {
            ashpd::Error::Response(ResponseError::Cancelled) => PortalFailureKind::Cancelled,
            ashpd::Error::Portal(ashpd::PortalError::Cancelled(_)) => PortalFailureKind::Cancelled,
            ashpd::Error::Portal(ashpd::PortalError::NotAllowed(_)) => PortalFailureKind::Denied,
            ashpd::Error::PortalNotFound(_) | ashpd::Error::RequiresVersion(_, _) => PortalFailureKind::Unavailable,
            _ if portal_error_is_unavailable(&error.to_string()) => PortalFailureKind::Unavailable,
            _ => PortalFailureKind::Technical,
        };

        Self { kind, message: error.to_string() }
    }

    fn denied(message: impl Into<String>) -> Self {
        Self { kind: PortalFailureKind::Denied, message: message.into() }
    }
}

#[derive(Debug, Clone)]
struct PortalShortcutInfo {
    id: String,
    effective_display_name: String,
}

trait PortalAdapter {
    async fn list_shortcuts(&mut self) -> Result<Vec<PortalShortcutInfo>, PortalFailure>;

    async fn bind_shortcut(
        &mut self,
        id: &str,
        description: &str,
        preferred_trigger: &str,
    ) -> Result<Vec<PortalShortcutInfo>, PortalFailure>;
}

async fn resolve_portal_shortcut<A: PortalAdapter>(
    adapter: &mut A,
    request: &RegisterShortcutRequest,
) -> Result<String, PortalFailure> {
    let listed = adapter.list_shortcuts().await?;
    let restored = listed.iter().find(|shortcut| shortcut.id == request.id.to_string());
    if portal_binding_action(restored.is_some(), request.reconfigure) == PortalBindingAction::Restore {
        return Ok(restored.unwrap().effective_display_name.clone());
    }

    let preferred_trigger = tauri_shortcut_to_xdg(&request.shortcut).map_err(|message| PortalFailure {
        kind: PortalFailureKind::Technical,
        message,
    })?;

    let bound = adapter
        .bind_shortcut(
            &request.id.to_string(),
            &request.description,
            &preferred_trigger,
        )
        .await?;

    bound
        .into_iter()
        .find(|shortcut| shortcut.id == request.id.to_string())
        .map(|shortcut| shortcut.effective_display_name)
        .ok_or_else(|| PortalFailure::denied("The desktop portal did not approve the requested shortcut."))
}

#[cfg(target_os = "linux")]
struct AshpdPortalAdapter<'a> {
    portal: &'a GlobalShortcuts,
    session: &'a ashpd::desktop::Session<GlobalShortcuts>,
}

#[cfg(target_os = "linux")]
impl PortalAdapter for AshpdPortalAdapter<'_> {
    async fn list_shortcuts(&mut self) -> Result<Vec<PortalShortcutInfo>, PortalFailure> {
        let response = self
            .portal
            .list_shortcuts(self.session, ListShortcutsOptions::default())
            .await
            .and_then(|request| request.response())
            .map_err(PortalFailure::from_error)?;

        Ok(response
            .shortcuts()
            .iter()
            .map(|shortcut| PortalShortcutInfo {
                id: shortcut.id().to_string(),
                effective_display_name: shortcut.trigger_description().to_string(),
            })
            .collect())
    }

    async fn bind_shortcut(
        &mut self,
        id: &str,
        description: &str,
        preferred_trigger: &str,
    ) -> Result<Vec<PortalShortcutInfo>, PortalFailure> {
        let shortcut = NewShortcut::new(id, description).preferred_trigger(preferred_trigger);
        let response = self
            .portal
            .bind_shortcuts(self.session, &[shortcut], None, BindShortcutsOptions::default())
            .await
            .and_then(|request| request.response())
            .map_err(PortalFailure::from_error)?;

        Ok(response
            .shortcuts()
            .iter()
            .map(|shortcut| PortalShortcutInfo {
                id: shortcut.id().to_string(),
                effective_display_name: shortcut.trigger_description().to_string(),
            })
            .collect())
    }
}

#[cfg(target_os = "linux")]
fn portal_error_is_unavailable(message: &str) -> bool {
    let normalized = message.to_ascii_lowercase();
    normalized.contains("unknownmethod")
        || normalized.contains("unknown method")
        || normalized.contains("serviceunknown")
        || normalized.contains("globalshortcuts portal was not found")
}

#[cfg(target_os = "linux")]
async fn prepare_portal_binding(
    request: &RegisterShortcutRequest,
    event_sender: broadcast::Sender<Event>,
) -> Result<ActiveBinding, PortalFailure> {
    let portal = GlobalShortcuts::new().await.map_err(PortalFailure::from_error)?;
    if portal.version() < 1 {
        return Err(PortalFailure {
            kind: PortalFailureKind::Unavailable,
            message: "The GlobalShortcuts portal is not supported by this desktop.".to_string(),
        });
    }

    let mut activated = portal.receive_activated().await.map_err(PortalFailure::from_error)?;
    let mut changed = portal.receive_shortcuts_changed().await.map_err(PortalFailure::from_error)?;

    let session = portal
        .create_session(CreateSessionOptions::default())
        .await
        .map_err(PortalFailure::from_error)?;

    let mut adapter = AshpdPortalAdapter { portal: &portal, session: &session };
    let effective_display_name = match resolve_portal_shortcut(&mut adapter, request).await {
        Ok(effective_display_name) => effective_display_name,
        Err(error) => {
            let _ = session.close().await;
            return Err(error);
        },
    };

    let generation = NEXT_PORTAL_GENERATION.fetch_add(1, Ordering::Relaxed);
    let activation_sender = event_sender.clone();

    tauri::async_runtime::spawn(async move {
        while let Some(signal) = activated.next().await {
            let Some(id) = Shortcut::from_portal_id(signal.shortcut_id()) else {
                continue;
            };

            let is_active = ACTIVE_PORTAL_GENERATIONS.lock().unwrap().get(&id) == Some(&generation);
            if is_active && !PROCESSING_SUSPENDED.load(Ordering::Relaxed) {
                send_shortcut_pressed(&activation_sender, id, "XDG portal");
            }
        }
    });

    tauri::async_runtime::spawn(async move {
        while let Some(signal) = changed.next().await {
            for shortcut in signal.shortcuts() {
                let Some(id) = Shortcut::from_portal_id(shortcut.id()) else {
                    continue;
                };

                let is_active = ACTIVE_PORTAL_GENERATIONS.lock().unwrap().get(&id) == Some(&generation);
                if is_active {
                    let _ = event_sender.send(Event::new(
                        TauriEventType::GlobalShortcutChanged,
                        vec![id.to_string(), shortcut.trigger_description().to_string()],
                    ));
                }
            }
        }
    });

    Ok(ActiveBinding::Portal {
        shortcut: request.shortcut.clone(),
        effective_display_name,
        generation,
        session,
    })
}

fn tauri_shortcut_to_xdg(shortcut: &str) -> Result<String, String> {
    let mut converted = Vec::new();
    let parts: Vec<&str> = shortcut.split('+').collect();
    if parts.len() < 2 {
        return Err(format!("Invalid global shortcut '{shortcut}'."));
    }

    for (index, part) in parts.iter().enumerate() {
        let normalized = part.to_ascii_lowercase();
        let is_last = index == parts.len() - 1;
        let value = if !is_last {
            match normalized.as_str() {
                "cmdorcontrol" | "commandorcontrol" | "ctrl" | "control" => "CTRL",
                "shift" => "SHIFT",
                "alt" | "option" => "ALT",
                "cmd" | "command" | "meta" | "super" => "LOGO",

                _ => return Err(format!("Unsupported shortcut modifier '{part}'.")),
            }.to_string()

        } else {

            match normalized.as_str() {
                "enter" => "Return".to_string(),
                "backspace" => "BackSpace".to_string(),
                "pageup" => "Prior".to_string(),
                "pagedown" => "Next".to_string(),
                "arrowup" => "Up".to_string(),
                "arrowdown" => "Down".to_string(),
                "arrowleft" => "Left".to_string(),
                "arrowright" => "Right".to_string(),
                "escape" => "Escape".to_string(),
                "delete" => "Delete".to_string(),
                "insert" => "Insert".to_string(),
                "home" => "Home".to_string(),
                "end" => "End".to_string(),
                "space" => "space".to_string(),
                "tab" => "Tab".to_string(),
                "up" => "Up".to_string(),
                "down" => "Down".to_string(),
                "left" => "Left".to_string(),
                "right" => "Right".to_string(),
                "minus" => "minus".to_string(),
                "equal" => "equal".to_string(),
                "bracketleft" => "bracketleft".to_string(),
                "bracketright" => "bracketright".to_string(),
                "backslash" => "backslash".to_string(),
                "semicolon" => "semicolon".to_string(),
                "quote" => "apostrophe".to_string(),
                "backquote" => "grave".to_string(),
                "comma" => "comma".to_string(),
                "period" => "period".to_string(),
                "slash" => "slash".to_string(),

                _ if normalized.starts_with("num") => numpad_key_to_xdg(&normalized).ok_or_else(|| format!("Unsupported shortcut key '{part}'."))?,
                _ if part.len() == 1 && part.as_bytes()[0].is_ascii_alphabetic() => normalized,
                _ if part.chars().all(|character| character.is_ascii_alphanumeric() || character == '_') => part.to_string(),

                _ => return Err(format!("Unsupported shortcut key '{part}'.")),
            }
        };

        converted.push(value);
    }

    Ok(converted.join("+"))
}

fn numpad_key_to_xdg(key: &str) -> Option<String> {
    let suffix = match key {
        "num0" => "0",
        "num1" => "1",
        "num2" => "2",
        "num3" => "3",
        "num4" => "4",
        "num5" => "5",
        "num6" => "6",
        "num7" => "7",
        "num8" => "8",
        "num9" => "9",

        "numadd" => "Add",
        "numsubtract" => "Subtract",
        "nummultiply" => "Multiply",
        "numdivide" => "Divide",
        "numdecimal" => "Decimal",
        "numenter" => "Enter",

        _ => return None,
    };

    Some(format!("KP_{suffix}"))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[derive(Default)]
    struct FakePortalAdapter {
        listed: Vec<PortalShortcutInfo>,
        bound: Vec<PortalShortcutInfo>,
        bind_failure: Option<PortalFailure>,
        bind_calls: usize,
        last_preferred_trigger: String,
    }

    impl PortalAdapter for FakePortalAdapter {
        async fn list_shortcuts(&mut self) -> Result<Vec<PortalShortcutInfo>, PortalFailure> {
            Ok(self.listed.clone())
        }

        async fn bind_shortcut(
            &mut self,
            _id: &str,
            _description: &str,
            preferred_trigger: &str,
        ) -> Result<Vec<PortalShortcutInfo>, PortalFailure> {
            self.bind_calls += 1;
            self.last_preferred_trigger = preferred_trigger.to_string();
            if let Some(error) = &self.bind_failure {
                return Err(error.clone());
            }
            Ok(self.bound.clone())
        }
    }

    fn portal_request(shortcut: &str, reconfigure: bool) -> RegisterShortcutRequest {
        RegisterShortcutRequest {
            id: Shortcut::VoiceRecordingToggle,
            shortcut: shortcut.to_string(),
            description: "Toggle voice recording".to_string(),
            reconfigure,
        }
    }

    fn portal_shortcut(display_name: &str) -> PortalShortcutInfo {
        PortalShortcutInfo {
            id: Shortcut::VoiceRecordingToggle.to_string(),
            effective_display_name: display_name.to_string(),
        }
    }

    #[test]
    fn converts_tauri_shortcut_to_xdg_trigger() {
        assert_eq!(tauri_shortcut_to_xdg("CmdOrControl+Shift+1").unwrap(), "CTRL+SHIFT+1");
        assert_eq!(tauri_shortcut_to_xdg("Control+Alt+Enter").unwrap(), "CTRL+ALT+Return");
        assert_eq!(tauri_shortcut_to_xdg("Super+Space").unwrap(), "LOGO+space");
        assert_eq!(tauri_shortcut_to_xdg("Ctrl+A").unwrap(), "CTRL+a");
        assert_eq!(tauri_shortcut_to_xdg("Ctrl+Num1").unwrap(), "CTRL+KP_1");
        assert_eq!(tauri_shortcut_to_xdg("Ctrl+Quote").unwrap(), "CTRL+apostrophe");
    }

    #[test]
    fn rejects_unsupported_xdg_trigger_parts() {
        assert!(tauri_shortcut_to_xdg("Hyper+1").is_err());
        assert!(tauri_shortcut_to_xdg("Ctrl++").is_err());
    }

    #[test]
    fn identical_configuration_is_not_registered_twice() {
        assert!(registration_is_unchanged(Some("CmdOrControl+Shift+1"), "cmdorcontrol+shift+1", false));
        assert!(!registration_is_unchanged(Some("CmdOrControl+Shift+1"), "CmdOrControl+Shift+2", false));
        assert!(!registration_is_unchanged(Some("CmdOrControl+Shift+1"), "CmdOrControl+Shift+1", true));
    }

    #[tokio::test]
    async fn portal_adapter_restores_without_binding() {
        let mut adapter = FakePortalAdapter {
            listed: vec![portal_shortcut("Ctrl+Shift+1")],
            ..Default::default()
        };

        let display_name = resolve_portal_shortcut(&mut adapter, &portal_request("CmdOrControl+Shift+1", false)).await.unwrap();

        assert_eq!(display_name, "Ctrl+Shift+1");
        assert_eq!(adapter.bind_calls, 0);
    }

    #[tokio::test]
    async fn portal_adapter_binds_missing_shortcut_with_preferred_trigger() {
        let mut adapter = FakePortalAdapter {
            bound: vec![portal_shortcut("Ctrl+Shift+1")],
            ..Default::default()
        };

        let display_name = resolve_portal_shortcut(&mut adapter, &portal_request("CmdOrControl+Shift+1", false)).await.unwrap();

        assert_eq!(display_name, "Ctrl+Shift+1");
        assert_eq!(adapter.bind_calls, 1);
        assert_eq!(adapter.last_preferred_trigger, "CTRL+SHIFT+1");
    }

    #[tokio::test]
    async fn portal_adapter_rebinds_after_deliberate_change() {
        let mut adapter = FakePortalAdapter {
            listed: vec![portal_shortcut("Ctrl+Shift+1")],
            bound: vec![portal_shortcut("Ctrl+Shift+2")],
            ..Default::default()
        };

        let display_name = resolve_portal_shortcut(&mut adapter, &portal_request("CmdOrControl+Shift+2", true)).await.unwrap();

        assert_eq!(display_name, "Ctrl+Shift+2");
        assert_eq!(adapter.bind_calls, 1);
        assert_eq!(adapter.last_preferred_trigger, "CTRL+SHIFT+2");
    }

    #[tokio::test]
    async fn portal_adapter_preserves_cancellation() {
        let mut adapter = FakePortalAdapter {
            bind_failure: Some(PortalFailure {
                kind: PortalFailureKind::Cancelled,
                message: "cancelled".to_string(),
            }),
            ..Default::default()
        };

        let error = resolve_portal_shortcut(&mut adapter, &portal_request("CmdOrControl+Shift+1", false)).await.unwrap_err();

        assert_eq!(error.kind, PortalFailureKind::Cancelled);
        assert_eq!(adapter.bind_calls, 1);
    }

    #[test]
    fn fallback_is_limited_to_an_unavailable_portal() {
        assert!(may_fallback_to_tauri(PortalFailureKind::Unavailable, None));
        assert!(may_fallback_to_tauri(PortalFailureKind::Unavailable, Some(ShortcutBackend::Tauri)));
        assert!(!may_fallback_to_tauri(PortalFailureKind::Unavailable, Some(ShortcutBackend::Portal)));
        assert!(!may_fallback_to_tauri(PortalFailureKind::Cancelled, None));
        assert!(!may_fallback_to_tauri(PortalFailureKind::Denied, None));
        assert!(!may_fallback_to_tauri(PortalFailureKind::Technical, None));
    }

    #[test]
    fn suspend_keeps_portal_session_registered() {
        assert!(!unregister_backend_during_suspend(ShortcutBackend::Portal));
        assert!(unregister_backend_during_suspend(ShortcutBackend::Tauri));
    }

    #[cfg(target_os = "linux")]
    #[test]
    fn only_unavailable_portal_errors_allow_fallback() {
        assert!(portal_error_is_unavailable("org.freedesktop.DBus.Error.UnknownMethod"));
        assert!(portal_error_is_unavailable("ServiceUnknown"));
        assert!(!portal_error_is_unavailable("Portal request was cancelled"));
        assert!(!portal_error_is_unavailable("NotAllowed"));
    }
}