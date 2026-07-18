use std::fmt::Display;
use std::sync::Mutex;
use arboard::Clipboard;
use log::{debug, error, warn};
use once_cell::sync::Lazy;
use axum::Json;
use serde::Serialize;
use crate::api_token::APIToken;
use crate::encryption::{EncryptedText, ENCRYPTION};

/// The process-wide clipboard instance. On Linux, retaining this instance keeps the app's
/// ownership of clipboard contents alive until the next write or application shutdown.
static CLIPBOARD: Lazy<Mutex<Option<Clipboard>>> = Lazy::new(|| Mutex::new(None));

trait ClipboardBackend {
    type Error: Display;

    fn set_text(&mut self, text: String) -> Result<(), Self::Error>;
}

impl ClipboardBackend for Clipboard {
    type Error = arboard::Error;

    fn set_text(&mut self, text: String) -> Result<(), Self::Error> {
        Clipboard::set_text(self, text)
    }
}

fn set_text_with_retry<B, F>(
    clipboard: &mut Option<B>,
    text: String,
    mut create_clipboard: F,
) -> Result<(), B::Error>
where
    B: ClipboardBackend,
    F: FnMut() -> Result<B, B::Error>,
{
    if clipboard.is_none() {
        *clipboard = Some(create_clipboard()?);
    }

    let first_result = clipboard.as_mut().unwrap().set_text(text.clone());
    if let Err(first_error) = first_result {
        warn!(Source = "Clipboard"; "Failed to set text using the current clipboard backend; reinitializing it once: {first_error}.");
        *clipboard = None;

        let mut retry_clipboard = create_clipboard()?;
        if let Err(retry_error) = retry_clipboard.set_text(text) {
            error!(Source = "Clipboard"; "Failed to set text after reinitializing the clipboard backend: {retry_error}.");
            return Err(retry_error);
        }

        *clipboard = Some(retry_clipboard);
    }

    Ok(())
}

fn release_clipboard<B>(clipboard: &mut Option<B>) -> bool {
    clipboard.take().is_some()
}

/// Sets the clipboard text to the provided encrypted text.
pub async fn set_clipboard(_token: APIToken, encrypted_text: String) -> Json<SetClipboardResponse> {
    let encrypted_text = EncryptedText::new(encrypted_text);

    // Decrypt this text first:
    let decrypted_text = match ENCRYPTION.decrypt(&encrypted_text) {
        Ok(text) => text,
        Err(e) => {
            error!(Source = "Clipboard"; "Failed to decrypt the text: {e}.");
            return Json(SetClipboardResponse {
                success: false,
                issue: e,
            })
        },
    };

    let mut clipboard = CLIPBOARD.lock().unwrap();
    match set_text_with_retry(&mut clipboard, decrypted_text, Clipboard::new) {
        Ok(_) => {
            debug!(Source = "Clipboard"; "Text was set to the clipboard successfully.");
            Json(SetClipboardResponse {
                success: true,
                issue: String::from(""),
            })
        },

        Err(e) => {
            error!(Source = "Clipboard"; "Failed to set text to the clipboard: {e}.");
            Json(SetClipboardResponse {
                success: false,
                issue: e.to_string(),
            })
        },
    }
}

/// Releases the process-wide clipboard instance during application shutdown.
pub fn shutdown_clipboard() {
    let mut clipboard = CLIPBOARD.lock().unwrap();
    if release_clipboard(&mut clipboard) {
        debug!(Source = "Clipboard"; "Clipboard instance was released.");
    }
}

/// The response for setting the clipboard text.
#[derive(Serialize)]
pub struct SetClipboardResponse {
    success: bool,
    issue: String,
}

#[cfg(test)]
mod tests {
    use std::collections::VecDeque;
    use std::sync::atomic::{AtomicUsize, Ordering};
    use std::sync::{Arc, Mutex};
    use super::{release_clipboard, set_text_with_retry, ClipboardBackend};

    struct MockClipboard {
        id: usize,
        fail_write: bool,
        writes: Arc<Mutex<Vec<(usize, String)>>>,
        drops: Arc<AtomicUsize>,
    }

    impl ClipboardBackend for MockClipboard {
        type Error = String;

        fn set_text(&mut self, text: String) -> Result<(), Self::Error> {
            self.writes.lock().unwrap().push((self.id, text));
            if self.fail_write {
                Err(format!("backend {} failed", self.id))
            } else {
                Ok(())
            }
        }
    }

    impl Drop for MockClipboard {
        fn drop(&mut self) {
            self.drops.fetch_add(1, Ordering::SeqCst);
        }
    }

    struct MockFactory {
        outcomes: VecDeque<bool>,
        created: usize,
        writes: Arc<Mutex<Vec<(usize, String)>>>,
        drops: Arc<AtomicUsize>,
    }

    impl MockFactory {
        fn new(outcomes: impl IntoIterator<Item = bool>) -> Self {
            Self {
                outcomes: outcomes.into_iter().collect(),
                created: 0,
                writes: Arc::new(Mutex::new(Vec::new())),
                drops: Arc::new(AtomicUsize::new(0)),
            }
        }

        fn create(&mut self) -> Result<MockClipboard, String> {
            let fail_write = self.outcomes.pop_front().expect("missing mock outcome");
            let id = self.created;
            self.created += 1;
            Ok(MockClipboard {
                id,
                fail_write,
                writes: Arc::clone(&self.writes),
                drops: Arc::clone(&self.drops),
            })
        }
    }

    #[test]
    fn initializes_lazily() {
        let mut clipboard = None;
        let mut factory = MockFactory::new([false]);

        assert_eq!(factory.created, 0);
        set_text_with_retry(&mut clipboard, "first".to_string(), || factory.create()).unwrap();

        assert_eq!(factory.created, 1);
        assert!(clipboard.is_some());
    }

    #[test]
    fn reuses_the_same_instance_for_multiple_writes() {
        let mut clipboard = None;
        let mut factory = MockFactory::new([false]);

        set_text_with_retry(&mut clipboard, "first".to_string(), || factory.create()).unwrap();
        set_text_with_retry(&mut clipboard, "second".to_string(), || factory.create()).unwrap();

        assert_eq!(factory.created, 1);
        assert_eq!(*factory.writes.lock().unwrap(), vec![(0, "first".to_string()), (0, "second".to_string())]);
    }

    #[test]
    fn retries_once_with_a_new_instance_after_a_write_failure() {
        let mut clipboard = None;
        let mut factory = MockFactory::new([true, false]);

        set_text_with_retry(&mut clipboard, "text".to_string(), || factory.create()).unwrap();

        assert_eq!(factory.created, 2);
        assert_eq!(clipboard.as_ref().unwrap().id, 1);
        assert_eq!(*factory.writes.lock().unwrap(), vec![(0, "text".to_string()), (1, "text".to_string())]);
    }

    #[test]
    fn returns_the_retry_error_and_discards_the_failed_instance() {
        let mut clipboard = None;
        let mut factory = MockFactory::new([true, true]);

        let error = set_text_with_retry(&mut clipboard, "text".to_string(), || factory.create()).unwrap_err();

        assert_eq!(error, "backend 1 failed");
        assert_eq!(factory.created, 2);
        assert!(clipboard.is_none());
    }

    #[test]
    fn releases_the_instance_on_shutdown() {
        let mut clipboard = None;
        let mut factory = MockFactory::new([false]);
        let drops = Arc::clone(&factory.drops);
        set_text_with_retry(&mut clipboard, "text".to_string(), || factory.create()).unwrap();

        assert!(release_clipboard(&mut clipboard));

        assert!(clipboard.is_none());
        assert_eq!(drops.load(Ordering::SeqCst), 1);
    }
}