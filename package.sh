#!/usr/bin/env bash
#
# Build the Thunderstore package for the version declared in QuantumObliterator.csproj.
#
# The build deliberately happens here rather than in CI: the game assemblies are referenced
# from a local Valheim install and are never redistributed, so a runner has nothing to compile
# against. What this produces is what gets published, byte for byte.
#
#   ./package.sh   ->  dist/wheezl-QuantumObliterator-<version>.zip
#
set -euo pipefail
cd "$(dirname "$0")"

CSPROJ=QuantumObliterator.csproj
TOML=thunderstore.toml

# ---------------------------------------------------------------- version
VERSION=$(sed -n 's|.*<PluginVersion>\(.*\)</PluginVersion>.*|\1|p' "$CSPROJ")
if [[ ! $VERSION =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "error: could not read a valid <PluginVersion> from $CSPROJ (got '$VERSION')" >&2
  exit 1
fi

# thunderstore.toml carries the same number; keep it in lockstep rather than trusting a human.
CURRENT=$(sed -n 's|^versionNumber = "\(.*\)"|\1|p' "$TOML")
if [[ $CURRENT != "$VERSION" ]]; then
  sed -i "s|^versionNumber = \".*\"|versionNumber = \"$VERSION\"|" "$TOML"
  echo "note: updated $TOML versionNumber $CURRENT -> $VERSION (commit this)"
fi

# ---------------------------------------------------------------- tools
if ! command -v tcli >/dev/null 2>&1; then
  echo "error: tcli not found. Install it with:  dotnet tool install -g tcli" >&2
  echo "       (and make sure ~/.dotnet/tools is on your PATH)" >&2
  exit 1
fi

# ---------------------------------------------------------------- build
echo "==> building QuantumObliterator $VERSION"
dotnet build -c Release

echo "==> packaging"
rm -rf dist
tcli build --package-version "$VERSION"

ZIP="dist/wheezl-QuantumObliterator-$VERSION.zip"
if [[ ! -f $ZIP ]]; then
  echo "error: expected $ZIP but tcli did not produce it" >&2
  ls -la dist || true
  exit 1
fi

echo
echo "Package ready: $ZIP"
unzip -l "$ZIP"
cat <<MSG

Next, to publish:

  git tag v$VERSION && git push origin v$VERSION
  gh release create v$VERSION "$ZIP" --title "v$VERSION" --notes-file CHANGELOG.md

Publishing the release fires .github/workflows/publish.yml, which uploads this exact
zip to Thunderstore using the TCLI_AUTH_TOKEN repository secret.
MSG
