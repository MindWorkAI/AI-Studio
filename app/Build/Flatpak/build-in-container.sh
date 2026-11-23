#!/usr/bin/env bash
#
# Builds the Flatpak of AI Studio and its Pandoc plugin. The build command "flatpak" of app/Build, see
# Commands/FlatpakCommand.cs, runs this script inside the CI image of the Flatpak repository and copies the
# bundles out of the output folder afterward.
#
# The sync below simulates the job "sync_flatpak_repo" of .github/workflows/build-and-release.yml, which
# prepares the Flatpak repository for a release. Whoever changes one of them changes the other one as well.
# On purpose, the script differs from the job in three points:
#
# - It removes the tag of AI Studio. flatpak-builder checks a tag against the commit, and the commit of a
#   test build has none.
# - It skips update-metainfo.py --check. The metainfo gets the entry of a release from the release command
#   only, so a test build lacks it.
# - It sets the URL of AI Studio as well. For the source "local", it points to a repository inside the
#   container, which holds the copied files of the local checkout in a single commit.
#
set -euo pipefail

: "${ARCH:?}" "${SOURCE:?}" "${FLATPAK_URL:?}" "${FLATPAK_COMMIT:?}"
: "${APP_BUNDLE:?}" "${PANDOC_BUNDLE:?}" "${BUILD_DIRECTORY:?}" "${OUT_DIRECTORY:?}"
if [ "$SOURCE" = "local" ]; then
    : "${LOCAL_CHECKOUT:?}"
else
    : "${AI_STUDIO_URL:?}" "${AI_STUDIO_COMMIT:?}"
fi

# The same version of yq as the pipeline uses, see FLATPAK_YQ_VERSION and FLATPAK_YQ_SHA256 there:
YQ_VERSION="4.44.6"
YQ_SHA256_X86_64="0c2b24e645b57d8e7c0566d18643a6d4f5580feeea3878127354a46f2a1e4598"
YQ_SHA256_AARCH64="9477ac3cc447b6c083986129e35af8122eb2b938fe55c9c3e40436fb966e5813"

FLATHUB_REPO_URL="https://flathub.org/repo/flathub.flatpakrepo"
APP_MANIFEST="org.mindworkai.AIStudio.yml"
PANDOC_MANIFEST="plugins/pandoc/org.mindworkai.AIStudio.plugin.pandoc.yml"

stage() {
    printf '\n===== %s: %s =====\n' "$(date -u '+%H:%M:%S')" "$*"
}

fail() {
    echo >&2 "error: $*"
    exit 1
}

# Fetches a single commit of a repository into a new folder, without its history.
fetch_commit() {
    local url="$1" commit="$2" directory="$3"
    shift 3
    git init --quiet "$directory"
    git -C "$directory" remote add origin "$url"
    git -C "$directory" fetch --quiet --depth 1 "$@" origin "$commit"
}

# Installs the given refs from Flathub, unless they are installed already. The image brings some runtimes
# in a lower layer of its file system, where updating them fails with "Invalid cross-device link". Hence,
# the script never updates them, and flatpak-builder runs without --install-deps-from.
install_missing() {
    local missing=()
    local ref
    for ref in "$@"; do
        if flatpak info --system "$ref" > /dev/null 2>&1; then
            echo "Already installed: $ref"
        else
            missing+=("$ref")
        fi
    done

    if [ "${#missing[@]}" -gt 0 ]; then
        flatpak install --system --noninteractive -y flathub "${missing[@]}"
    fi
}

# Prints the branch of an SDK extension: the version of the extension point which the SDK declares, e.g.,
# 25.08 for org.freedesktop.Sdk.Extension.dotnet10 in org.gnome.Sdk//50.
extension_branch() {
    local sdk_ref="$1" extension="$2"
    flatpak info --system --show-metadata "$sdk_ref" | awk -v section="[Extension ${extension%.*}]" '
        $0 == section { inside = 1; next }
        /^\[/ { inside = 0 }
        inside && $1 == "version" { sub(/^[^=]*=[[:space:]]*/, ""); print; exit }
    '
}

stage "Checking the container"
test "$(uname -m)" = "$ARCH" || fail "the container runs on $(uname -m) instead of $ARCH"
for tool in curl flatpak flatpak-builder git jq python3 sha256sum uv; do
    hash "$tool" 2> /dev/null || fail "the image lacks $tool"
done
echo "Building the source '$SOURCE' for $ARCH"

stage "Preparing yq ${YQ_VERSION}"
case "$ARCH" in
    aarch64)
        yq_asset="yq_linux_arm64"
        yq_sha256="$YQ_SHA256_AARCH64"
        ;;
    x86_64)
        yq_asset="yq_linux_amd64"
        yq_sha256="$YQ_SHA256_X86_64"
        ;;
    *)
        fail "there is no Flatpak for $ARCH"
        ;;
esac

mkdir -p "$BUILD_DIRECTORY/tools" "$OUT_DIRECTORY"
curl -fsSL -o "$BUILD_DIRECTORY/tools/yq" "https://github.com/mikefarah/yq/releases/download/v${YQ_VERSION}/${yq_asset}"
echo "${yq_sha256}  $BUILD_DIRECTORY/tools/yq" | sha256sum --check --strict
chmod 755 "$BUILD_DIRECTORY/tools/yq"
export PATH="$BUILD_DIRECTORY/tools:$PATH"

if [ "$SOURCE" = "local" ]; then
    stage "Committing the local checkout in a repository of its own"

    # The files arrive with the owner of the checkout, and git refuses to work in a repository of another user:
    chown -R root:root "$LOCAL_CHECKOUT"
    git init --quiet "$LOCAL_CHECKOUT"

    # A checkout on Windows may hold CR LF line endings, which a commit there would turn into LF. The fetches by
    # commit, from update-dependencies and flatpak-builder alike, need the two uploadpack settings:
    git -C "$LOCAL_CHECKOUT" config core.autocrlf input
    git -C "$LOCAL_CHECKOUT" config core.safecrlf false
    git -C "$LOCAL_CHECKOUT" config uploadpack.allowAnySHA1InWant true
    git -C "$LOCAL_CHECKOUT" config uploadpack.allowFilter true
    git -C "$LOCAL_CHECKOUT" add --all
    git -C "$LOCAL_CHECKOUT" -c user.name="AI Studio build" -c user.email="build@localhost" commit --quiet --no-verify -m "Local checkout"

    AI_STUDIO_URL="file://$LOCAL_CHECKOUT"
    AI_STUDIO_COMMIT=$(git -C "$LOCAL_CHECKOUT" rev-parse HEAD)
    echo "Committed $(git -C "$LOCAL_CHECKOUT" ls-files | wc -l) files as ${AI_STUDIO_COMMIT}"
fi

stage "Fetching the Flatpak repository at ${FLATPAK_COMMIT}"
fetch_commit "$FLATPAK_URL" "$FLATPAK_COMMIT" "$BUILD_DIRECTORY/flatpak"
cd "$BUILD_DIRECTORY/flatpak"
git checkout --quiet FETCH_HEAD

stage "Installing the runtimes and SDKs"
runtime_version=$(yq -r '."runtime-version"' "$APP_MANIFEST")
sdk_ref="$(yq -r '.sdk' "$APP_MANIFEST")//${runtime_version}"
install_missing "$(yq -r '.runtime' "$APP_MANIFEST")//${runtime_version}" "$sdk_ref"

refs=()
while IFS= read -r extension; do
    branch=$(extension_branch "$sdk_ref" "$extension")
    [ -n "$branch" ] || fail "$sdk_ref declares no extension point for $extension"
    refs+=("${extension}//${branch}")
done < <(yq -r '."sdk-extensions"[]' "$APP_MANIFEST")

# update-dependencies restores the .NET projects inside the Freedesktop SDK and its .NET extension, in the
# versions it passes to flatpak-dotnet-generator.py:
generator_call=$(grep -E 'flatpak-dotnet-generator\.py' update-dependencies)
freedesktop_version=$(grep -oE -e '-f [0-9]+\.[0-9]+' <<< "$generator_call" | cut -d' ' -f2)
dotnet_major=$(grep -oE -e '-d [0-9]+' <<< "$generator_call" | cut -d' ' -f2)
[ -n "$freedesktop_version" ] && [ -n "$dotnet_major" ] || fail "update-dependencies calls flatpak-dotnet-generator.py without -f and -d"
refs+=("org.freedesktop.Sdk//${freedesktop_version}" "org.freedesktop.Sdk.Extension.dotnet${dotnet_major}//${freedesktop_version}")

pandoc_sdk=$(yq -r '.sdk' "$PANDOC_MANIFEST")
[[ "$pandoc_sdk" == *//* ]] || fail "the Pandoc plugin names its SDK without a branch: $pandoc_sdk"
refs+=("$pandoc_sdk")
install_missing "${refs[@]}"

stage "Reading metadata.txt of AI Studio ${AI_STUDIO_COMMIT}"
fetch_commit "$AI_STUDIO_URL" "$AI_STUDIO_COMMIT" "$BUILD_DIRECTORY/ai-studio-metadata" --filter=blob:none
metadata=$(git -C "$BUILD_DIRECTORY/ai-studio-metadata" show FETCH_HEAD:metadata.txt | tr -d '\r')
app_version=$(sed -n '1p' <<< "$metadata")
pdfium_full_version=$(sed -n '10p' <<< "$metadata")
pdfium_chromium_revision=$(cut -d'.' -f3 <<< "$pdfium_full_version")
[[ "$pdfium_chromium_revision" =~ ^[0-9]+$ ]] || fail "line 10 of metadata.txt names no PDFium version: $pdfium_full_version"
echo "AI Studio ${app_version} with PDFium ${pdfium_full_version}"

stage "Syncing the Flatpak repository like the release pipeline"
pdfium_base_url="https://github.com/bblanchon/pdfium-binaries/releases/download/chromium%2F${pdfium_chromium_revision}"
export AI_STUDIO_URL AI_STUDIO_COMMIT
export PDFIUM_X64_URL="${pdfium_base_url}/pdfium-linux-x64.tgz"
export PDFIUM_ARM64_URL="${pdfium_base_url}/pdfium-linux-arm64.tgz"
curl -fsSL -o "$BUILD_DIRECTORY/pdfium-linux-x64.tgz" "$PDFIUM_X64_URL"
curl -fsSL -o "$BUILD_DIRECTORY/pdfium-linux-arm64.tgz" "$PDFIUM_ARM64_URL"

export PDFIUM_X64_SHA256
export PDFIUM_ARM64_SHA256
PDFIUM_X64_SHA256=$(sha256sum "$BUILD_DIRECTORY/pdfium-linux-x64.tgz" | awk '{print $1}')
PDFIUM_ARM64_SHA256=$(sha256sum "$BUILD_DIRECTORY/pdfium-linux-arm64.tgz" | awk '{print $1}')

yq -i '
  (.modules[] | select(.name == "mind-work-ai-studio").sources[0].url) = strenv(AI_STUDIO_URL) |
  (.modules[] | select(.name == "mind-work-ai-studio").sources[0].commit) = strenv(AI_STUDIO_COMMIT) |
  del(.modules[] | select(.name == "mind-work-ai-studio").sources[0].tag) |
  (.modules[] | select(.name == "mind-work-ai-studio").sources[] | select(.type == "archive" and .only-arches[0] == "x86_64").url) = strenv(PDFIUM_X64_URL) |
  (.modules[] | select(.name == "mind-work-ai-studio").sources[] | select(.type == "archive" and .only-arches[0] == "x86_64").sha256) = strenv(PDFIUM_X64_SHA256) |
  (.modules[] | select(.name == "mind-work-ai-studio").sources[] | select(.type == "archive" and .only-arches[0] == "aarch64").url) = strenv(PDFIUM_ARM64_URL) |
  (.modules[] | select(.name == "mind-work-ai-studio").sources[] | select(.type == "archive" and .only-arches[0] == "aarch64").sha256) = strenv(PDFIUM_ARM64_SHA256)
' "$APP_MANIFEST"

./update-dependencies

test "$(yq -r '(.modules[] | select(.name == "mind-work-ai-studio")).sources[0].url' "$APP_MANIFEST")" = "$AI_STUDIO_URL"
test "$(yq -r '(.modules[] | select(.name == "mind-work-ai-studio")).sources[0].commit' "$APP_MANIFEST")" = "$AI_STUDIO_COMMIT"
test "$(yq -r '(.modules[] | select(.name == "mind-work-ai-studio")).sources[0].tag' "$APP_MANIFEST")" = "null"
test "$(yq -r '(.modules[] | select(.name == "mind-work-ai-studio")).sources[] | select(.type == "archive" and .only-arches[0] == "x86_64").url' "$APP_MANIFEST")" = "$PDFIUM_X64_URL"
test "$(yq -r '(.modules[] | select(.name == "mind-work-ai-studio")).sources[] | select(.type == "archive" and .only-arches[0] == "x86_64").sha256' "$APP_MANIFEST")" = "$PDFIUM_X64_SHA256"
test "$(yq -r '(.modules[] | select(.name == "mind-work-ai-studio")).sources[] | select(.type == "archive" and .only-arches[0] == "aarch64").url' "$APP_MANIFEST")" = "$PDFIUM_ARM64_URL"
test "$(yq -r '(.modules[] | select(.name == "mind-work-ai-studio")).sources[] | select(.type == "archive" and .only-arches[0] == "aarch64").sha256' "$APP_MANIFEST")" = "$PDFIUM_ARM64_SHA256"

for generated_source in cargo-sources.json dotnet-sources.json tauri-cli-sources.json; do
    test -s "$generated_source"
    jq -e 'type == "array" and length > 0' "$generated_source" > /dev/null
done

git diff --check
git diff --stat

stage "Building the Flatpak of AI Studio"
app_id=$(yq -r '.id' "$APP_MANIFEST")
flatpak-builder \
    --arch="$ARCH" \
    --repo="$BUILD_DIRECTORY/repo" \
    --state-dir="$BUILD_DIRECTORY/state" \
    --disable-rofiles-fuse \
    --force-clean \
    --default-branch=stable \
    "$BUILD_DIRECTORY/build-app" \
    "$APP_MANIFEST"

flatpak build-bundle \
    --arch="$ARCH" \
    --runtime-repo="$FLATHUB_REPO_URL" \
    "$BUILD_DIRECTORY/repo" \
    "$OUT_DIRECTORY/$APP_BUNDLE" \
    "$app_id" \
    stable

stage "Building the Pandoc plugin"

# Like the CI, the plugin builds against the app it extends, so the bundle of the app gets installed first:
flatpak install --system --noninteractive -y --bundle "$OUT_DIRECTORY/$APP_BUNDLE"
flatpak-builder \
    --arch="$ARCH" \
    --repo="$BUILD_DIRECTORY/repo" \
    --state-dir="$BUILD_DIRECTORY/state" \
    --disable-rofiles-fuse \
    --force-clean \
    "$BUILD_DIRECTORY/build-pandoc" \
    "$PANDOC_MANIFEST"

flatpak build-bundle \
    --arch="$ARCH" \
    --runtime \
    "$BUILD_DIRECTORY/repo" \
    "$OUT_DIRECTORY/$PANDOC_BUNDLE" \
    "$(yq -r '.id' "$PANDOC_MANIFEST")" \
    "$(yq -r '.branch' "$PANDOC_MANIFEST")"

stage "Done"
ls -l "$OUT_DIRECTORY"