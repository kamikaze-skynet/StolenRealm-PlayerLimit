#!/usr/bin/env bash
# Builds the mod and writes the Thunderstore zip to dist/.
# Set STOLEN_REALM_DIR and BEPINEX_CORE first (see README).
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
version="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["version_number"])' "$root/package/manifest.json")"

dotnet build "$root/PlayerLimitMod" -c Release -o "$root/PlayerLimitMod/bin/Release"

stage="$root/dist/stage"
rm -rf "$stage"; mkdir -p "$stage"
cp "$root"/package/* "$stage/"
cp "$root/PlayerLimitMod/bin/Release/PlayerLimitMod.dll" "$stage/"

zip="$root/dist/girthy_mods-Player_Limit_Mod-$version.zip"
rm -f "$zip"
(cd "$stage" && python3 -c 'import os,sys,zipfile; z=zipfile.ZipFile(sys.argv[1],"w",zipfile.ZIP_DEFLATED); [z.write(f) for f in sorted(os.listdir("."))]; z.close()' "$zip")
rm -rf "$stage"
echo "Created $zip"
