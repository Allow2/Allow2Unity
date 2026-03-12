#!/usr/bin/env bash
# export-unitypackage.sh — Build a .unitypackage from the Allow2 SDK source tree.
#
# A .unitypackage is a gzipped tar archive where each file is represented by a
# directory named after a GUID. Inside that directory:
#   pathname  — text file containing the Assets/... path
#   asset     — the actual file content
#   asset.meta — the .meta file content (optional but expected by Unity)
#
# This script generates deterministic GUIDs from file paths so the package is
# reproducible across builds. No Unity Editor installation is required.
#
# Usage:
#   ./packaging/asset-store/export-unitypackage.sh [--version X.Y.Z]
#
# Output: Allow2SDK-<version>.unitypackage in the repository root.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
SDK_DIR="$REPO_ROOT/com.allow2.sdk"
PKG_JSON="$SDK_DIR/package.json"

# ---------------------------------------------------------------------------
# Parse arguments
# ---------------------------------------------------------------------------
VERSION=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --version) VERSION="$2"; shift 2 ;;
    *) echo "Unknown option: $1" >&2; exit 1 ;;
  esac
done

if [[ -z "$VERSION" ]]; then
  VERSION=$(python3 -c "import json; print(json.load(open('$PKG_JSON'))['version'])")
fi

OUTPUT="$REPO_ROOT/Allow2SDK-${VERSION}.unitypackage"
WORK_DIR=$(mktemp -d)
trap 'rm -rf "$WORK_DIR"' EXIT

echo "Building Allow2SDK-${VERSION}.unitypackage ..."

# ---------------------------------------------------------------------------
# Helper: deterministic GUID from a path string (MD5, lowercased hex).
# Unity GUIDs are 32-character lowercase hex strings.
# ---------------------------------------------------------------------------
generate_guid() {
  echo -n "$1" | md5sum | cut -c1-32
}

# ---------------------------------------------------------------------------
# Helper: generate a .meta file for a folder
# ---------------------------------------------------------------------------
generate_folder_meta() {
  local guid="$1"
  cat <<META
fileFormatVersion: 2
guid: ${guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
META
}

# ---------------------------------------------------------------------------
# Helper: generate a .meta file for a regular file
# ---------------------------------------------------------------------------
generate_file_meta() {
  local guid="$1"
  local ext="$2"

  local importer="TextScriptImporter"
  case "$ext" in
    cs)    importer="MonoImporter" ;;
    json)  importer="TextScriptImporter" ;;
    md)    importer="TextScriptImporter" ;;
    txt)   importer="TextScriptImporter" ;;
    asmdef) importer="TextScriptImporter" ;;
    *)     importer="DefaultImporter" ;;
  esac

  if [[ "$importer" == "MonoImporter" ]]; then
    cat <<META
fileFormatVersion: 2
guid: ${guid}
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData:
  assetBundleName:
  assetBundleVariant:
META
  else
    cat <<META
fileFormatVersion: 2
guid: ${guid}
TextScriptImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
META
  fi
}

# ---------------------------------------------------------------------------
# Add a directory entry to the package
# ---------------------------------------------------------------------------
add_directory() {
  local asset_path="$1"  # e.g. Assets/Allow2
  local guid
  guid=$(generate_guid "$asset_path")

  local entry_dir="$WORK_DIR/$guid"
  mkdir -p "$entry_dir"
  echo -n "$asset_path" > "$entry_dir/pathname"
  generate_folder_meta "$guid" > "$entry_dir/asset.meta"
}

# ---------------------------------------------------------------------------
# Add a file entry to the package
# ---------------------------------------------------------------------------
add_file() {
  local src_file="$1"     # absolute path on disk
  local asset_path="$2"   # e.g. Assets/Allow2/Runtime/Core/Allow2Api.cs
  local guid
  guid=$(generate_guid "$asset_path")

  local entry_dir="$WORK_DIR/$guid"
  mkdir -p "$entry_dir"
  echo -n "$asset_path" > "$entry_dir/pathname"
  cp "$src_file" "$entry_dir/asset"

  local ext="${asset_path##*.}"
  generate_file_meta "$guid" "$ext" > "$entry_dir/asset.meta"
}

# ---------------------------------------------------------------------------
# Build the package contents
# ---------------------------------------------------------------------------

# Root directories
add_directory "Assets"
add_directory "Assets/Allow2"

# Copy top-level SDK files into Assets/Allow2/
for f in "$SDK_DIR/package.json"; do
  [[ -f "$f" ]] && add_file "$f" "Assets/Allow2/$(basename "$f")"
done

# Copy repo-level files that should ship in the asset store package
for f in "$REPO_ROOT/README.md" "$REPO_ROOT/LICENSE"; do
  [[ -f "$f" ]] && add_file "$f" "Assets/Allow2/$(basename "$f")"
done

# Copy CHANGELOG if it exists
if [[ -f "$REPO_ROOT/CHANGELOG.md" ]]; then
  add_file "$REPO_ROOT/CHANGELOG.md" "Assets/Allow2/CHANGELOG.md"
fi

# Walk the Runtime directory tree
while IFS= read -r -d '' entry; do
  # Relative path from SDK_DIR, e.g. Runtime/Core/Allow2Api.cs
  rel="${entry#$SDK_DIR/}"
  asset_path="Assets/Allow2/$rel"

  if [[ -d "$entry" ]]; then
    add_directory "$asset_path"
  elif [[ -f "$entry" ]]; then
    add_file "$entry" "$asset_path"
  fi
done < <(find "$SDK_DIR/Runtime" -print0 | sort -z)

# ---------------------------------------------------------------------------
# Create the .unitypackage (gzipped tar of GUID directories)
# ---------------------------------------------------------------------------
tar -czf "$OUTPUT" -C "$WORK_DIR" .

echo "Created: $OUTPUT"
echo "Size: $(du -h "$OUTPUT" | cut -f1)"
echo "Entries: $(find "$WORK_DIR" -maxdepth 1 -mindepth 1 -type d | wc -l) assets"
