# Runtime Patches

This directory documents temporary patches for third-party Rust dependencies.

## Qdrant Edge

AI Studio temporarily uses a pinned commit from `SommerEngineering/qdrant` for `qdrant-edge`.
The fork commit exposes Qdrant's internal `lib/edge` crate as `qdrant-edge` and applies the
trait-solver fix from Qdrant PR #9312.

When updating to a newer Qdrant Edge version, replace the placeholder values first:

```bash
export QDRANT_EDGE_VERSION="0.7.2"
export QDRANT_BRANCH="ai-studio-qdrant-edge-${QDRANT_EDGE_VERSION}"
export AISTUDIO_REPO="xxx/mindwork-ai-studio"
export QDRANT_REPO="xxx/qdrant"
```

1. Sync the Qdrant fork with upstream:

```bash
cd "$QDRANT_REPO"
git remote add upstream https://github.com/qdrant/qdrant.git 2>/dev/null || true
git fetch upstream
git fetch origin
git switch master
git merge --ff-only upstream/master
git push origin master
```

2. Create a fresh AI Studio branch in the Qdrant fork:

```bash
cd "$QDRANT_REPO"
git switch -c "$QDRANT_BRANCH" master
```

3. Apply the AI Studio patch if upstream has not released the fix yet:

```bash
cd "$QDRANT_REPO"
git apply "$AISTUDIO_REPO/runtime/patches/qdrant-edge-ai-studio.patch"
```

4. Update the exposed `qdrant-edge` version in the fork:

```bash
cd "$QDRANT_REPO"
perl -0pi -e "s/name = \"qdrant-edge\"\\nversion = \"[^\"]+\"/name = \"qdrant-edge\"\\nversion = \"$ENV{QDRANT_EDGE_VERSION}\"/" lib/edge/Cargo.toml
```

5. Commit and push the fork branch:

```bash
cd "$QDRANT_REPO"
git diff
git add lib/edge/Cargo.toml lib/segment/src/common/anonymize.rs
git commit -m "Expose qdrant-edge ${QDRANT_EDGE_VERSION} package for AI Studio"
git push origin "$QDRANT_BRANCH"
export QDRANT_EDGE_COMMIT="$(git rev-parse HEAD)"
echo "$QDRANT_EDGE_COMMIT"
```

6. Update AI Studio to use the new Qdrant Edge version and fork commit:

```bash
cd "$AISTUDIO_REPO"
perl -0pi -e "s/qdrant-edge = \"[^\"]+\"/qdrant-edge = \"$ENV{QDRANT_EDGE_VERSION}\"/" runtime/Cargo.toml
perl -0pi -e "s/rev = \"[0-9a-f]+\"/rev = \"$ENV{QDRANT_EDGE_COMMIT}\"/" runtime/Cargo.toml
```

7. Refresh the AI Studio lock file and verify the Rust runtime:

```bash
cd "$AISTUDIO_REPO/runtime"
cargo update -p qdrant-edge
cargo check
```

Remove the patch and the `[patch.crates-io]` override once Qdrant publishes a fixed `qdrant-edge`
release on crates.io.
