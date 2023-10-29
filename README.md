# Prepatcher
[![Latest API version](https://img.shields.io/nuget/v/Zetrith.Prepatcher?label=Zetrith.Prepatcher)](https://www.nuget.org/packages/Zetrith.Prepatcher)

Structured assembly rewriting library/mod for RimWorld

Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=2934420800

The project has three main logical components:
- **Assembly rewriter** - in principle platform-agnostic
- **Assembly reloader** - specific to the Mono runtime used by RimWorld's Unity version
- **Mod manager** (named Prestarter) - specific to RimWorld

## Installation

### Users
- Download the mod by clicking *Code* > *Download ZIP* on this repo's main page
- Unzip in RimWorld's Mods folder
- Open the game and put Prepatcher first in the mod list. It has no dependencies on other mods.

The mod is currently no longer distributed through GitHub's Releases tab.

Prepatcher is a provider of the Harmony library for RimWorld mods and can be used instead of [HarmonyRimWorld](https://github.com/pardeike/HarmonyRimWorld). It patches the mod loading system so that:

- `zetrith.prepatcher` (this mod) satisfies dependencies on `brrainz.harmony`
- Mods needing to load after `brrainz.harmony` also need to load after `zetrith.prepatcher`

Having both Prepatcher and the Harmony mod active won't cause any problems.


### Modders

Add the [`Zetrith.Prepatcher`](https://www.nuget.org/packages/Zetrith.Prepatcher) nuget package to your mod's project:

`<PackageReference Include="Zetrith.Prepatcher" Version="<version>" />`

Similar to Harmony, the package distributes an API to be used for compiling only and the actual runtime library is installed by the user once using the mod downloaded from here.

To make a RimWorld mod correctly depend on Prepatcher, put this in `About.xml`:
```xml
<modDependencies>
    <li>
        <packageId>zetrith.prepatcher</packageId>
        <displayName>Prepatcher</displayName>
        <steamWorkshopUrl>steam://url/CommunityFilePage/2934420800</steamWorkshopUrl>
        <downloadUrl>https://github.com/Zetrith/Prepatcher/releases/latest</downloadUrl>
    </li>
</modDependencies>
```

Library example (declaring field addition):
```cs
[PrepatcherField]
public static extern ref int MyInt(this TargetClass target);
```
For more details and features, see https://github.com/Zetrith/Prepatcher/wiki


## Compiling
Clone anywhere and go to the Source folder. Run `dotnet build` and/or `dotnet test`.
If you want to run it ingame clone to the Mods folder.