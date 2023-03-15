using RimWorld.Planet;
using Verse;

namespace Prepatcher.Process;

internal static class GameInjections
{
    internal static void RegisterInjections(FieldAdder fieldAdder)
    {
        Lg.Verbose("Registering injections");

        fieldAdder.RegisterInjection(
            typeof(ThingWithComps),
            typeof(ThingComp),
            nameof(ThingWithComps.InitializeComps),
            nameof(ThingWithComps.comps)
        );

        fieldAdder.RegisterInjection(
            typeof(Map),
            typeof(MapComponent),
            nameof(Map.FillComponents),
            nameof(Map.components)
        );

        fieldAdder.RegisterInjection(
            typeof(World),
            typeof(WorldComponent),
            nameof(World.FillComponents),
            nameof(World.components)
        );

        fieldAdder.RegisterInjection(
            typeof(Game),
            typeof(GameComponent),
            nameof(Game.FillComponents),
            nameof(Game.components)
        );
    }
}
