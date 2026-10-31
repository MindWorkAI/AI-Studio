use log::{error, info};
use once_cell::sync::Lazy;
use tauri::webview::Cookie;
use tauri::{Url, WebviewWindow};
use crate::api_token::{generate_api_token, APIToken};

/// The token which the app window presents to the .NET server as the value of its session cookie.
/// Every start makes a new one.
pub static UI_ACCESS_TOKEN: Lazy<APIToken> = Lazy::new(generate_api_token);

/// The name of the session cookie. The .NET server expects the same name.
pub const SESSION_COOKIE_NAME: &str = "ai_studio_session";

/// The environment variable which carries the token to the .NET server.
pub const DOTNET_ENV_UI_TOKEN: &str = "AI_STUDIO_UI_TOKEN";

/// The host of the app. The .NET server listens on this address only, and the session cookie
/// belongs to it.
pub const APP_HOST: &str = "127.0.0.1";

/// The file in the root of the repository which holds the boot link of a development build.
#[cfg(debug_assertions)]
const DEV_BOOT_LINK_FILE: &str = "dev-boot-link.txt";

/// Builds the session cookie of the app window.
///
/// The cookie is for the app host alone and lives as long as the window: it has no expiry date,
/// scripts cannot read it, and the window never sends it with a request which another site
/// started. We parse it from text because the `cookie` crate, whose builder would need its
/// `SameSite` type, is no direct dependency of ours.
pub fn session_cookie() -> Cookie<'static> {
    let token = UI_ACCESS_TOKEN.to_hex_text();
    Cookie::parse(format!("{SESSION_COOKIE_NAME}={token}; Domain={APP_HOST}; Path=/; HttpOnly; SameSite=Strict"))
        .expect("The session cookie consists of fixed attributes and a hexadecimal token, which always parse.")
}

/// Puts the session cookie into the cookie store of the given window, before the window opens
/// the app at the given URL.
///
/// Setting the cookie does not wait for the webview, and WebView2 even adds it in the background.
/// Reading the cookies back waits until the webview has handled everything sent before, so the
/// cookie is in place once this function returns. That reading is also the only way to learn
/// whether setting the cookie worked.
///
/// Blocks until the main thread has answered, so never call it on a worker of the async runtime.
/// The log never shows the value of the cookie.
pub fn hand_over_session_cookie(window: &WebviewWindow, app_url: &Url) {
    if let Err(error) = window.set_cookie(session_cookie()) {
        error!(Source = "App access"; "Failed to hand the session cookie to the app window: {error}");
        return;
    }

    match app_host_cookies(window, app_url) {
        Ok(cookies) if cookies.iter().any(|cookie| cookie.name() == SESSION_COOKIE_NAME) => {
            info!(Source = "App access"; "The app window got its session cookie.");
        },

        Ok(_) => {
            error!(Source = "App access"; "The app window lacks its session cookie after it was set.");
        },

        Err(error) => {
            error!(Source = "App access"; "Failed to check the session cookie of the app window: {error}");
        },
    }
}

/// Reads the cookies of the app host from the given window.
///
/// On macOS, wry picks the cookies for a URL by comparing their domain with the domain of the
/// URL. An IP address such as the app host has no domain in that sense, so wry would never find
/// a cookie of the app there. We read all cookies instead and pick those of the app host
/// ourselves.
#[cfg(target_os = "macos")]
fn app_host_cookies(window: &WebviewWindow, _app_url: &Url) -> tauri::Result<Vec<Cookie<'static>>> {
    Ok(window.cookies()?.into_iter().filter(|cookie| cookie.domain() == Some(APP_HOST)).collect())
}

/// Reads the cookies of the app host from the given window.
///
/// WebView2 and WebKitGTK pick the cookies for a URL by themselves. Reading all cookies instead
/// would need WebKitGTK 2.42 or newer.
#[cfg(not(target_os = "macos"))]
fn app_host_cookies(window: &WebviewWindow, app_url: &Url) -> tauri::Result<Vec<Cookie<'static>>> {
    window.cookies_for_url(app_url.clone())
}

/// Returns the link with which another browser gets the session cookie of a development build,
/// such as the browser of an agent which checks a change in the running app.
#[cfg(debug_assertions)]
fn dev_boot_link(port: u16) -> String {
    format!("http://{APP_HOST}:{port}/dev/boot?token={}", UI_ACCESS_TOKEN.to_hex_text())
}

/// Writes the boot link of a development build into a file of its own in the root of the
/// repository. It never goes into the startup env file: that file holds the secrets of the IPC,
/// which a browser needs none of.
///
/// Only development builds contain this function. A release has no boot link, and its .NET
/// server knows no route which would take one. The log names the file, never the link.
#[cfg(debug_assertions)]
pub fn write_dev_boot_link(port: u16) {
    let path = std::path::PathBuf::from("..").join(DEV_BOOT_LINK_FILE);
    match std::fs::write(&path, dev_boot_link(port)) {
        Ok(_) => info!(Source = "App access"; "Wrote the boot link of this development build to '{}'.", path.display()),
        Err(error) => error!(Source = "App access"; "Failed to write the boot link of this development build to '{}': {error}", path.display()),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn the_session_cookie_carries_the_token() {
        let cookie = session_cookie();

        assert_eq!(cookie.name(), SESSION_COOKIE_NAME);
        assert_eq!(cookie.value(), UI_ACCESS_TOKEN.to_hex_text());
    }

    #[test]
    fn the_session_cookie_belongs_to_the_app_host_alone() {
        let cookie = session_cookie();

        assert_eq!(cookie.domain(), Some(APP_HOST));
        assert_eq!(cookie.path(), Some("/"));
    }

    #[test]
    fn the_session_cookie_is_hidden_from_scripts_and_other_sites() {
        let cookie = session_cookie();

        assert_eq!(cookie.http_only(), Some(true));
        assert!(cookie.same_site().is_some_and(|same_site| same_site.is_strict()));
    }

    #[test]
    fn the_session_cookie_lives_only_as_long_as_the_window() {
        let cookie = session_cookie();

        assert!(cookie.expires().is_none());
        assert!(cookie.max_age().is_none());
    }

    #[test]
    #[cfg(debug_assertions)]
    fn the_dev_boot_link_leads_to_the_boot_route_of_the_app_host() {
        let link = Url::parse(dev_boot_link(61153).as_str()).unwrap();

        assert_eq!(link.host_str(), Some(APP_HOST));
        assert_eq!(link.port(), Some(61153));
        assert_eq!(link.path(), "/dev/boot");

        let token = link.query_pairs().find(|(key, _)| key == "token").map(|(_, value)| value.into_owned());
        assert_eq!(token.as_deref(), Some(UI_ACCESS_TOKEN.to_hex_text()));
    }

    #[test]
    fn the_ui_token_differs_from_the_api_token() {
        assert!(!UI_ACCESS_TOKEN.validate(&APIToken::from_hex_text(crate::runtime_api_token::API_TOKEN.to_hex_text())));
    }
}