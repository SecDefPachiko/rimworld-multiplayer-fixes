using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Multiplayer.Client
{
    // Shows other players what they're about to do: a ghost of the building they're placing (in their color) and
    // the cells of a designation they're dragging (walls, zones, mining...). The data comes with the cursor packet.
    [HarmonyPatch(typeof(MapInterface), nameof(MapInterface.MapInterfaceUpdate))]
    static class PlacementPreviews
    {
        private static readonly List<IntVec3> tmpCells = new();

        static void Postfix()
        {
            if (Multiplayer.Client == null || !Multiplayer.settings.showCursors || TickPatch.Simulating) return;

            var map = Find.CurrentMap;
            if (map == null || WorldRendererUtility.WorldSelected) return;

            foreach (var player in Multiplayer.session.players)
            {
                if (player.username == Multiplayer.username) continue;
                if (player.map != map.Index) continue;
                if (player.factionId != Multiplayer.RealPlayerFaction.loadID) continue;

                var cursorCell = player.cursor.ToIntVec3();
                if (!cursorCell.InBounds(map)) continue;

                var color = player.color;

                if (player.designationDragStart.IsValid && player.designationDragStart.InBounds(map) &&
                    player.designationDragStyle?.DrawStyleWorker is { } drawStyle)
                {
                    // The same draw style (rectangle, line, circle...) the player is dragging with gives the same cells
                    tmpCells.Clear();
                    drawStyle.Update(player.designationDragStart, cursorCell, tmpCells);
                    tmpCells.RemoveAll(c => !c.InBounds(map));
                    if (tmpCells.Count > 0)
                        GenDraw.DrawFieldEdges(tmpCells, color);
                }

                if (player.placingDef is ThingDef thingDef)
                {
                    GhostDrawer.DrawGhostThing(cursorCell, player.placingRot, thingDef, null,
                        new Color(color.r, color.g, color.b, 0.35f), AltitudeLayer.Blueprint);
                }
                else if (player.placingDef is TerrainDef)
                {
                    tmpCells.Clear();
                    tmpCells.Add(cursorCell);
                    GenDraw.DrawFieldEdges(tmpCells, color);
                }
            }
        }

    }
}
