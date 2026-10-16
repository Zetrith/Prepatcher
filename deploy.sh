#!/usr/bin/env bash
# Build the 1.7 port and install it as a local mod.
set -e
cd "$(dirname "$0")"
M="$HOME/.local/share/Steam/steamapps/common/RimWorld/Mods/Prepatcher17"
dotnet build Source/Implementation/Implementation.csproj -c Release -v q --nologo
rm -f Assemblies/0Harmony.dll Assemblies/*.deps.json   # Harmony mod supplies 3.0
mkdir -p "$M/Assemblies"
cp Assemblies/*.dll "$M/Assemblies/"
echo "deployed -> $M"
ls -la "$M/Assemblies/"
