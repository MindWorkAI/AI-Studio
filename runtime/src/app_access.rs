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

    match window.cookies_for_url(app_url.clone()) {
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
    fn the_ui_token_differs_from_the_api_token() {
        assert!(!UI_ACCESS_TOKEN.validate(&APIToken::from_hex_text(crate::runtime_api_token::API_TOKEN.to_hex_text())));
    }
}