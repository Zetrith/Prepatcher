Title: 1.7: the in-process reload can't work, the engine binds to the original Assembly-CSharp

I had a go at getting Prepatcher running on 1.7 and wanted to share what I
found, since the last problem looks like a dead end for the current design
rather than something that can be patched around.

Repo with the changes: <your repo url>

## The blocker

RimWorld.dll is the engine host on 1.7 and reads game state directly out of
Assembly-CSharp. RimWorldWaylith.Rendering.MapRenderFeature alone touches:

    Verse.Find.get_CurrentMap          Verse.Current.get_CameraDriver
    Verse.CameraDriver.get_MyCamera    Verse.Find.get_World
    Verse.Map.skyManager               Verse.SkyManager.get_CurSky

RimWorld.dll loads before any mod and binds to the original Assembly-CSharp.
After the reload hands over, the game runs in the patched copy, so the
renderer keeps asking the discarded world what to draw, gets nothing, and
draws nothing. No exception: as far as it's concerned there is no map.

The result is a game that boots, simulates and responds to the UI with a
completely black map. Tooltips read terrain correctly, so the sim is fine.

Nothing can rebind RimWorld.dll, and the state can't be copied across since
the two Map types are unrelated.

## Getting that far

Everything before the handover does work on 1.7. The fixes were:

- UnsafeAssembly.GetRawData: no MonoImage, so read the bytes back off disk.
  Mod assemblies are byte-loaded and report no Location, so find the file via
  ModContentPack.foldersToLoadDescendingOrder.
- AssemblyCollector: 1.7 keeps its assemblies in the game root, not a Managed
  folder, and the folder has non-managed dlls in it.
- Loader.ClearAssemblyResolve: off Mono, AppDomain.AssemblyResolve only
  forwards to a static field on AssemblyLoadContext.
- PrepatcherMod: Thread.Abort is PlatformNotSupported, so the vanilla loading
  thread has to be parked instead.
- 1.7 queues materials until defs load, in a static list in UnityStubs that
  isn't reloaded, so the ones queued by the stopped load have to be dropped.
- The Cecil resolver needs the game's own UnityEngine* -> UnityStubs
  redirects, or writing any modified assembly that has a Unity-typed default
  parameter throws.
- Don't bundle Harmony. 1.7 wants 3.0 and the bundled 2.4.2 loads first and
  downgrades everyone.

Since SetReflectionOnly is a no-op off Mono, the superseded assemblies stay
visible, so I also had to keep a registry of the patched copies, load them
into their own AssemblyLoadContext, share Harmony between the two contexts,
and redirect AccessTools.TypeByName and the GenTypes lookups. All of that
works, which is what makes the renderer the real wall: it's the one consumer
that can't be redirected.

## What does work

Writing the rewritten Assembly-CSharp to disk and letting the game load it
normally. One assembly, already carrying the injected fields at startup,
engine and mods bound to the same thing. The map renders and field injection
works: verified with 458 mods, BigAndSmall.BSCache injected into Verse.Pawn
and MVCF.ManagedVerb into Verse.Verb.

That does mean generating the assembly, installing it and restarting, and
regenerating whenever the game or the mod list changes.

Two of the free patches have to come off in that mode: ReplaceAssemblyLoading
and ReplaceAddComponent both exist to prop up the reload, and baking a call
into PrepatcherImpl makes the game assembly depend on a mod assembly that
isn't loaded yet, which fails every mod's content load.

## Unrelated but worth having

Harmony 3 rejects a patch whose injected parameter types don't match, and the
rejection takes out every other patch on that method plus the static
constructor that triggered it. One mod's bad camera prefix left the game
rendering but unable to move the camera or select anything. A finalizer on
PatchClassProcessor.Patch and PatchProcessor.Patch that logs and swallows
turns that into one skipped patch class instead. Seven mods in my list need
it.
