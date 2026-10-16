using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mono.Cecil;
using Multiplayer.API;
using Multiplayer.Client.Util;
using RimWorld;
using Verse;

namespace MultiplayerLoader;

public static class SyncRituals
{
    private static List<ISyncMethodForPrepatcher> syncMethodsWithPrepatches = new();

    // Harmony fallback for when Prepatcher didn't rewrite the methods (not installed, or RimWorld 1.7 where the game
    // runs on .NET instead of Mono). (target method name, sync method) pairs, filled in the registration pass.
    private static List<(string methodName, ISyncMethodForPrepatcher syncMethod)> harmonyFallbackSyncMethods = new();
    private static List<MethodInfo> harmonyFallbackTargets = new();

    public static bool PrepatcherPrefix(int index, object __instance, object[] __args)
    {
        // This prefix is applied to a generic method and called by all possible instantiations
        // This check makes sure the type is correct
        if (syncMethodsWithPrepatches[index].TargetType == __instance.GetType())
            return !syncMethodsWithPrepatches[index].DoSync(__instance, __args);
        return true;
    }

    private static MethodInfo syncMethod = AccessTools.Method("Multiplayer.Client.Sync:Method");

    // This method is called twice (two passes)
    // The first pass with module != null is called from Prepatcher and applies prepatches
    // The second pass with module == null is called from Sync initialization and registers SyncMethods
    public static void ApplyPrepatches(ModuleDefinition module)
    {
        void Register(Type baseType, string method, Type derivedType, Action<ISyncMethodForPrepatcher> postProcess = null)
        {
            if (module != null)
            {
                Prepatches.AddPrepatcherPrefix(
                    module,
                    syncMethodsWithPrepatches.Count,
                    AccessTools.Method(baseType, method),
                    AccessTools.Method(typeof(SyncRituals), nameof(PrepatcherPrefix))
                );

                // The ids need to be in sync between passes (note the array is recreated after Prepatcher restarts the game)
                syncMethodsWithPrepatches.Add(null);
            }
            else
            {
                // Sync.Method doesn't apply a Harmony patch and doesn't require the DeclaringType to be the target type
                var newSyncMethod = (ISyncMethodForPrepatcher)syncMethod.Invoke(null, [derivedType, method, null]);
                postProcess?.Invoke(newSyncMethod);
                syncMethodsWithPrepatches.Add(newSyncMethod);

                harmonyFallbackSyncMethods.Add((method, newSyncMethod));
                harmonyFallbackTargets.Add(AccessTools.Method(ClosedBaseType(derivedType, baseType), method));
            }
        }

        // ======= PawnRitualRoleSelectionWidget =======

        var ritualRolesSerializer = Serializer.New<IEnumerable<RitualRole>, (PawnRitualRoleSelectionWidget roleSelectionWidget, string Key)>(
            RitualRoleWriter<PawnRitualRoleSelectionWidget, RitualRole>,
            RitualRoleReader<PawnRitualRoleSelectionWidget, RitualRole>
        );
        var psychicRitualRolesSerializer = Serializer.New<IEnumerable<PsychicRitualRoleDef>, (PawnPsychicRitualRoleSelectionWidget roleSelectionWidget, string Key)>(
            RitualRoleWriter<PawnPsychicRitualRoleSelectionWidget, PsychicRitualRoleDef>,
            RitualRoleReader<PawnPsychicRitualRoleSelectionWidget, PsychicRitualRoleDef>
        );

        Register(typeof(PawnRoleSelectionWidgetBase<>), nameof(PawnRoleSelectionWidgetBase<RitualRole>.TryAssignReplace),
            typeof(PawnRitualRoleSelectionWidget),
            m => m.TransformArgument(1, ritualRolesSerializer));
        Register(typeof(PawnRoleSelectionWidgetBase<>), nameof(PawnRoleSelectionWidgetBase<PsychicRitualRoleDef>.TryAssignReplace),
            typeof(PawnPsychicRitualRoleSelectionWidget),
            m => m.TransformArgument(1, psychicRitualRolesSerializer));

        Register(typeof(PawnRoleSelectionWidgetBase<>), nameof(PawnRoleSelectionWidgetBase<RitualRole>.TryAssignAnyRole),
            typeof(PawnRitualRoleSelectionWidget));
        Register(typeof(PawnRoleSelectionWidgetBase<>), nameof(PawnRoleSelectionWidgetBase<PsychicRitualRoleDef>.TryAssignAnyRole),
            typeof(PawnPsychicRitualRoleSelectionWidget));

        Register(typeof(PawnRoleSelectionWidgetBase<>), nameof(PawnRoleSelectionWidgetBase<RitualRole>.TryAssign),
            typeof(PawnRitualRoleSelectionWidget),
            m => m.TransformArgument(1, ritualRolesSerializer));
        Register(typeof(PawnRoleSelectionWidgetBase<>), nameof(PawnRoleSelectionWidgetBase<PsychicRitualRoleDef>.TryAssign),
            typeof(PawnPsychicRitualRoleSelectionWidget),
            m => m.TransformArgument(1, psychicRitualRolesSerializer));

        Register(
            typeof(PawnRoleSelectionWidgetBase<>),
            MpMethodUtil.GetLambda(
                typeof(PawnRoleSelectionWidgetBase<ILordJobRole>),
                nameof(PawnRoleSelectionWidgetBase<RitualRole>.DrawPawnListInternal),
                lambdaOrdinal: 7).Name,
            typeof(PawnRitualRoleSelectionWidget)
        ); // Roles right click delegate (try assign spectate)
        Register(
            typeof(PawnRoleSelectionWidgetBase<>),
            MpMethodUtil.GetLambda(
                typeof(PawnRoleSelectionWidgetBase<ILordJobRole>),
                nameof(PawnRoleSelectionWidgetBase<PsychicRitualRoleDef>.DrawPawnListInternal),
                lambdaOrdinal: 7).Name,
            typeof(PawnPsychicRitualRoleSelectionWidget)
        ); // Roles right click delegate (try assign spectate)

        Register(
            typeof(PawnRoleSelectionWidgetBase<>),
            MpMethodUtil.GetLambda(
                typeof(PawnRoleSelectionWidgetBase<ILordJobRole>),
                nameof(PawnRoleSelectionWidgetBase<RitualRole>.DrawPawnListInternal),
                lambdaOrdinal: 2).Name,
            typeof(PawnRitualRoleSelectionWidget)
        ); // Not participating left click delegate (try assign any role or spectate)
        Register(
            typeof(PawnRoleSelectionWidgetBase<>),
            MpMethodUtil.GetLambda(
                typeof(PawnRoleSelectionWidgetBase<ILordJobRole>),
                nameof(PawnRoleSelectionWidgetBase<PsychicRitualRoleDef>.DrawPawnListInternal),
                lambdaOrdinal: 2).Name,
            typeof(PawnPsychicRitualRoleSelectionWidget)
        ); // Not participating left click delegate (try assign any role or spectate)

        if (module == null)
            ApplyHarmonyFallbackIfNeeded();
    }

    // Prepatcher inserts a call to PrepatcherPrefix at the start of each target method before the game loads.
    // If that call isn't there, Prepatcher didn't run, so patch the same methods with Harmony instead.
    private static void ApplyHarmonyFallbackIfNeeded()
    {
        var prepatcherPrefix = AccessTools.Method(typeof(SyncRituals), nameof(PrepatcherPrefix));
        var firstTarget = harmonyFallbackTargets.First();
        if (PatchProcessor.GetOriginalInstructions(firstTarget).Any(inst => inst.Calls(prepatcherPrefix)))
            return;

        Log.Message("Multiplayer: Prepatcher not active, syncing ritual role selection with Harmony patches");

        var harmony = new Harmony("rwmt.multiplayer.syncrituals");
        var prefix = new HarmonyMethod(AccessTools.Method(typeof(SyncRituals), nameof(HarmonyFallbackPrefix)));
        foreach (var target in harmonyFallbackTargets.Distinct())
            harmony.Patch(target, prefix: prefix);
    }

    // The two widget types share the base class's methods (for reference type arguments .NET runs one shared copy
    // of the generic code), so this can be called for either widget type and has to look up the matching sync method.
    // It may also be applied twice to the same shared code; __runOriginal makes the second application a no-op.
    private static bool HarmonyFallbackPrefix(object __instance, object[] __args, MethodBase __originalMethod, bool __runOriginal)
    {
        if (!__runOriginal) return false;

        var instanceType = __instance.GetType();
        foreach (var (methodName, sync) in harmonyFallbackSyncMethods)
            if (methodName == __originalMethod.Name && sync.TargetType == instanceType)
                return !sync.DoSync(__instance, __args);

        return true;
    }

    // PawnRoleSelectionWidgetBase<> -> PawnRoleSelectionWidgetBase<RitualRole> for PawnRitualRoleSelectionWidget
    private static Type ClosedBaseType(Type derivedType, Type openBaseType)
    {
        for (var t = derivedType; t != null; t = t.BaseType)
            if (t.IsGenericType && t.GetGenericTypeDefinition() == openBaseType)
                return t;
        throw new Exception($"{derivedType} doesn't derive from {openBaseType}");
    }

    private static (WidgetType roleSelectionWidget, string Key) RitualRoleWriter<WidgetType, RoleType>(IEnumerable<RoleType> roles, object target, object[] _)
        where WidgetType : PawnRoleSelectionWidgetBase<RoleType>
        where RoleType : class, ILordJobRole
    {
        var roleSelectionWidget = (WidgetType)target;
        return (roleSelectionWidget, roleSelectionWidget.assignments.RoleGroups().FirstOrDefault(g => g.SequenceEqual(roles))?.Key);
    }

    private static IEnumerable<RoleType> RitualRoleReader<WidgetType, RoleType>((WidgetType roleSelectionWidget, string Key) data)
        where WidgetType : PawnRoleSelectionWidgetBase<RoleType>
        where RoleType : class, ILordJobRole
    {
        return data.roleSelectionWidget.assignments.RoleGroups().FirstOrDefault(g => g.Key == data.Key);
    }
}
