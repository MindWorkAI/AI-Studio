# Runtime Patches

This directory documents temporary patches for third-party Rust dependencies.

## Qdrant Edge

AI Studio temporarily uses a pinned commit from `SommerEngineering/qdrant` for `qdrant-edge`.
The fork commit exposes Qdrant's internal `lib/edge` crate as `qdrant-edge` and applies the
trait-solver fix from Qdrant PR #9312.

When updating to a newer Qdrant Edge version:

1. Sync the Qdrant fork with upstream.
2. Apply `qdrant-edge-ai-studio.patch` to the Qdrant fork if upstream has not released the fix yet.
3. Update the version in `lib/edge/Cargo.toml` in the fork.
4. Update `runtime/Cargo.toml` in AI Studio to use the new `qdrant-edge` version and fork commit.
5. Run `cargo update -p qdrant-edge` from `runtime/`.

Remove the patch and the `[patch.crates-io]` override once Qdrant publishes a fixed `qdrant-edge`
release on crates.io.
