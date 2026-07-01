# Qdrant Edge Migration

- Status: Active
- Introduced: 2026-06-02
- Remove after: 2026-12-02
- Code references:
  - `runtime/src/qdrant_edge_database.rs`

## User Impact

Older installations may still contain Qdrant server sidecar binaries or directories after upgrading to a release that uses Qdrant Edge.

Without this shim, obsolete Qdrant server files could remain in application data or bundled resource locations even though AI Studio no longer starts or uses the separate Qdrant server process.

## Compatibility Behavior

When Qdrant Edge starts, AI Studio checks known previous Qdrant server sidecar locations and attempts to remove obsolete `qdrant` and `qdrant_test` files or directories.

On Windows and macOS production installations, AI Studio also checks the executable directory for old `qdrant.exe` or `qdrant` sidecar binaries. Missing paths are ignored, and failed cleanup attempts are logged without blocking Qdrant Edge startup.

## Removal Checklist

- Remove `remove_obsolete_qdrant_sidecar_files`.
- Remove `remove_obsolete_qdrant_path` if it has no other callers.
- Remove the startup cleanup call from `start_qdrant_edge_database`.
- Update this document's status to `Removed`.