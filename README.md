# Prepatcher
Structured assembly rewriting library/mod for RimWorld

## Installation

### Users
Download the mod zip from [Releases](https://github.com/Zetrith/Prepatcher/releases) and unzip in RimWorld's Mods folder.

Clicking *Code* > *Download ZIP* on GitHub's main page won't work. Please use Releases.

Prepatcher depends on [Harmony](https://github.com/pardeike/HarmonyRimWorld). Install it first in the mod list and put Prepatcher right below.

### Modders

Add the `PrepatcherAPI` nuget package to your mod's project:

`<PackageReference Include="PrepatcherAPI" Version="1.0.0" ExcludeAssets="runtime" />`

Similar to Harmony, the package distributes an API (currently just attributes) and the actual runtime library is installed by the user once using the mod downloaded from here.

Library example (declaring field addition):
```cs
[PrepatcherField]
public static extern ref int MyInt(this TargetClass target);
```
For more details and features, see https://github.com/Zetrith/Prepatcher/wiki


## Compiling
Clone anywhere and go to the Source folder. Run `dotnet build` and/or `dotnet test`.
If you want to run it ingame clone to the Mods folder.