# AGENTS.md

These instructions cover the Rust runtime in `runtime/`. They add to the `AGENTS.md` in the repository
root, which applies here as well.

## Rust Runtime (`runtime/`)
**Entry point:** `runtime/src/main.rs`

Key modules:
- `app_window.rs` - Tauri window management, updater integration
- `app_access.rs` - Session cookie which lets the app window into the .NET server
- `dotnet.rs` - Launches and manages the .NET sidecar process
- `runtime_api.rs` - Axum-based HTTPS API for .NET ↔ Rust communication
- `certificate.rs` - Generates self-signed TLS certificates for secure IPC
- `secret.rs` - Secure secret storage using OS keyring (Keychain/Credential Manager)
- `clipboard.rs` - Cross-platform clipboard operations
- `file_data.rs` - File processing for RAG (extracts text from PDF, DOCX, XLSX, PPTX, etc.)
- `encryption.rs` - AES-256-CBC encryption for sensitive data
- `pandoc.rs` - Integration with Pandoc for document conversion
- `log.rs` - Logging infrastructure using `flexi_logger`

**Every runtime API route requires the API token.** `require_api_token` in `runtime_api.rs` checks it
for all routes at once, so handlers take no `APIToken` argument. Register a new route in
`create_router`, before that call: `route_layer` protects only the routes registered before it, and
a route added afterward would be open to every process on the machine. The test
`every_route_requires_the_api_token` reads the routes from `create_router` and fails for any route
which answers without the token.

**Only the app window gets into the .NET server.** Every start makes a new UI token in `app_access.rs`.
Rust passes it to .NET as `AI_STUDIO_UI_TOKEN` through `ipc_environment` in `dotnet.rs`, and hands it to
the window as an HttpOnly session cookie before the window opens the app (`change_location_to` in
`app_window.rs`); .NET refuses every request without it. Therefore:

- Never put the token into a URL, a log line, or `startup.env`. Only a development build writes a boot
  link, to `dev-boot-link.txt` in the repository root, so that another browser can get the cookie.
- Keep the cookie `Lax`. A release shows `tauri://localhost` before it opens the app, and WKWebView on
  macOS withholds a `Strict` cookie on that navigation. A development build starts from the dev server
  of the Tauri CLI on `127.0.0.1`, the same site as the app, which hides the problem; start it with
  `cargo tauri dev --no-watch --no-dev-server` to see what a release sees.
- The .NET server listens on `127.0.0.1` only, and the cookie belongs to that host. Change the host in
  `APP_HOST` and in `Program.cs` together, since no test compares them. `AppAccessRuntimeAgreementTests`
  only checks that both sides use the same cookie name and environment variable.

**Runtime API handlers never block.** All calls of the .NET app share one HTTP/2 connection, and the
task driving it may wait on exactly the Tokio worker which a blocking handler occupies, so a single
blocking handler can hold up the whole app. Therefore:

- File and disk access, the OS keyring, waiting for a `std::sync::Mutex`, and CPU-bound work such as
  loading a tokenizer or scanning text run in `tokio::task::spawn_blocking`. Follow
  `run_qdrant_edge_request` in `qdrant_edge_database.rs`, `prepare_image` and `prepare_image_sync` in
  `image.rs`, or `sanitize_batch` in `prompt_injection/api.rs`.
- When an API offers a callback, as the file dialogs do, await the callback instead of calling the
  blocking variant; see `await_dialog` in `file_actions.rs`.
- Short, bounded work, such as a single metadata lookup or writing a log line, may stay on the worker.
- When the blocking task fails, answer with an error, never with an empty value the app could take
  for a valid answer.
- Never keep a `std::sync::MutexGuard` alive across an `.await`, not even with an explicit `drop`
  before it: the future of the handler is then no longer `Send`, and Axum refuses it. Scope the guard
  in a block instead.
- `runtime_api::test_support::assert_runtime_stays_free` tests a handler which waits for a lock. Run
  such a test with `#[tokio::test(flavor = "multi_thread", worker_threads = 1)]`.

## IPC Communication Flow
1. Rust runtime starts and generates TLS certificate
2. Rust starts internal HTTPS API on random port
3. Rust launches .NET sidecar, passing: API port, certificate fingerprint, API token, secret key, UI token
4. .NET reads environment variables and establishes secure HTTPS connection to Rust
5. .NET requests an app port from Rust, starts Blazor Server on 127.0.0.1 at that port
6. Rust hands the session cookie to the Tauri webview and points it to 127.0.0.1:app_port
7. Bi-directional communication: .NET ↔ Rust via HTTPS API
