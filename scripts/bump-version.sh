#!/usr/bin/env bash
# bump-version.sh — Version bumping for the Allow2 Unity SDK.
#
# This is the SINGLE entry point for releasing. Bumping a version here
# updates package.json, commits the change, and creates a git tag.
# Pushing the tag triggers all CI/CD pipelines (release.yml, store-publish.yml).
#
# Usage:
#   ./scripts/bump-version.sh <major|minor|patch|prerelease> [--preid alpha|beta|rc]
#
# Examples:
#   ./scripts/bump-version.sh prerelease --preid alpha   # 2.0.0-alpha.1 -> 2.0.0-alpha.2
#   ./scripts/bump-version.sh prerelease --preid beta    # 2.0.0-alpha.2 -> 2.0.0-beta.0
#   ./scripts/bump-version.sh patch                       # 2.0.0-beta.0  -> 2.0.1
#   ./scripts/bump-version.sh minor                       # 2.0.1         -> 2.1.0
#   ./scripts/bump-version.sh major                       # 2.1.0         -> 3.0.0
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PKG_JSON="$REPO_ROOT/com.allow2.sdk/package.json"

if [[ ! -f "$PKG_JSON" ]]; then
  echo "ERROR: package.json not found at $PKG_JSON" >&2
  exit 1
fi

# ---------------------------------------------------------------------------
# Parse arguments
# ---------------------------------------------------------------------------
BUMP="${1:-}"
PREID=""

if [[ -z "$BUMP" ]]; then
  echo "Usage: $0 <major|minor|patch|prerelease> [--preid alpha|beta|rc]" >&2
  exit 1
fi

shift
while [[ $# -gt 0 ]]; do
  case "$1" in
    --preid) PREID="${2:-alpha}"; shift 2 ;;
    *) echo "Unknown option: $1" >&2; exit 1 ;;
  esac
done

# ---------------------------------------------------------------------------
# Read current version
# ---------------------------------------------------------------------------
OLD=$(python3 -c "import json; print(json.load(open('$PKG_JSON'))['version'])")
echo "Current version: $OLD"

# ---------------------------------------------------------------------------
# Compute new version (pure bash/python, no npm required)
# ---------------------------------------------------------------------------
compute_new_version() {
  python3 - "$OLD" "$BUMP" "$PREID" <<'PYEOF'
import sys, re

old = sys.argv[1]
bump = sys.argv[2]
preid = sys.argv[3] if len(sys.argv) > 3 else ""

# Parse semver: MAJOR.MINOR.PATCH[-PRERELEASE]
m = re.match(r'^(\d+)\.(\d+)\.(\d+)(?:-(.+))?$', old)
if not m:
    print(f"ERROR: Cannot parse version: {old}", file=sys.stderr)
    sys.exit(1)

major, minor, patch = int(m.group(1)), int(m.group(2)), int(m.group(3))
pre = m.group(4) or ""

if bump == "major":
    print(f"{major + 1}.0.0")
elif bump == "minor":
    print(f"{major}.{minor + 1}.0")
elif bump == "patch":
    # If currently pre-release, drop the pre-release tag (e.g. 2.0.0-alpha.1 -> 2.0.0)
    if pre:
        print(f"{major}.{minor}.{patch}")
    else:
        print(f"{major}.{minor}.{patch + 1}")
elif bump == "prerelease":
    if not preid:
        # No preid: increment existing pre-release number or start at 0
        if pre:
            pm = re.match(r'^([a-zA-Z]+)\.(\d+)$', pre)
            if pm:
                print(f"{major}.{minor}.{patch}-{pm.group(1)}.{int(pm.group(2)) + 1}")
            else:
                print(f"{major}.{minor}.{patch}-{pre}.1")
        else:
            print(f"{major}.{minor}.{patch + 1}-0")
    else:
        # With preid: if same preid, increment; if different, start at 0
        if pre:
            pm = re.match(r'^([a-zA-Z]+)\.(\d+)$', pre)
            if pm and pm.group(1) == preid:
                print(f"{major}.{minor}.{patch}-{preid}.{int(pm.group(2)) + 1}")
            else:
                print(f"{major}.{minor}.{patch}-{preid}.0")
        else:
            print(f"{major}.{minor}.{patch + 1}-{preid}.0")
else:
    print(f"ERROR: Unknown bump type: {bump}", file=sys.stderr)
    sys.exit(1)
PYEOF
}

NEW=$(compute_new_version)

if [[ -z "$NEW" || "$NEW" == "$OLD" ]]; then
  echo "ERROR: Version computation failed or produced no change." >&2
  exit 1
fi

echo "New version:     $NEW"

# ---------------------------------------------------------------------------
# Update package.json
# ---------------------------------------------------------------------------
python3 -c "
import json
with open('$PKG_JSON', 'r') as f:
    pkg = json.load(f)
pkg['version'] = '$NEW'
with open('$PKG_JSON', 'w') as f:
    json.dump(pkg, f, indent=2)
    f.write('\n')
"

echo "Updated $PKG_JSON"

# ---------------------------------------------------------------------------
# Git commit and tag
# ---------------------------------------------------------------------------
cd "$REPO_ROOT"
git add com.allow2.sdk/package.json
git commit -m "v$NEW"
git tag "v$NEW"

echo ""
echo "$OLD -> $NEW"
echo "Tagged v$NEW"
echo ""
echo "To release, push the tag:"
echo "  git push origin master --tags"
