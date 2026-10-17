using System.Collections.Generic;
using System.Reflection;
using RimWorld.BaseGen;
using Verse;

namespace Multiplayer.Client
{
    // Game-wide static state that has to be identical for all players when a multiplayer game starts.
    // This used to re-run the classes' static initializers (TypeInitializer.Invoke). That works on Mono, but on
    // RimWorld 1.7's .NET runtime it silently does nothing, which left every DebugSettings bool false. With
    // anomalyDarkeningFX false, effecters spawn without sub-effecters and every progress bar throws, so pawns
    // couldn't wear apparel, eat or harvest. Instead, snapshot the defaults at startup and restore them.
    public static class StaticDefaults
    {
        private static Dictionary<FieldInfo, object> debugSettings;
        private static Rot4[] singleThingRotations;

        // Call before any game is started, so the values are still the static initializers' defaults
        public static void Capture()
        {
            debugSettings = new Dictionary<FieldInfo, object>();
            foreach (var field in typeof(DebugSettings).GetFields(BindingFlags.Public | BindingFlags.Static))
                if (!field.IsLiteral && !field.IsInitOnly)
                    debugSettings[field] = field.GetValue(null);

            singleThingRotations = (Rot4[])SymbolResolver_SingleThing.tmpRotations.Clone();
        }

        public static void Restore()
        {
            foreach (var (field, value) in debugSettings)
                field.SetValue(null, value);

            // Copy in place, the array is shuffled in place during base generation
            singleThingRotations.CopyTo(SymbolResolver_SingleThing.tmpRotations, 0);
        }
    }
}
