#!/usr/bin/env bash
# Build and install into the game's Mods folder.
set -e
cd "$(dirname "$0")"
M="$HOME/.local/share/Steam/steamapps/common/RimWorld/Mods/Prepatcher17"
dotnet build Source/Implementation/Implementation.csproj -c Release -v q --nologo
rm -f Assemblies/0Harmony.dll Assemblies/*.deps.json   # Harmony comes from the Harmony mod
mkdir -p "$M/Assemblies" "$M/About"
cp Assemblies/*.dll "$M/Assemblies/"
cp About/About.xml "$M/About/"
cp LICENSE "$M/LICENSE"
echo "installed to $M"
