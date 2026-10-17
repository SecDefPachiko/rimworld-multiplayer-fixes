using System.Collections.Generic;
using System.Linq;
using Multiplayer.Common.Networking.Packet;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Multiplayer.Client;

public class PlayerCursors
{
    private HashSet<int> lastSelected = new();
    private float lastSelectedSend;
    private int lastMap;

    private byte cursorSeq;
    private float lastCursorSend;

    public void SendVisuals()
    {
        if (Time.realtimeSinceStartup - lastCursorSend > 0.05f)
        {
            lastCursorSend = Time.realtimeSinceStartup;
            SendCursor();
        }

        if (Time.realtimeSinceStartup - lastSelectedSend > 0.2f)
        {
            lastSelectedSend = Time.realtimeSinceStartup;
            SendSelected();
        }
    }

    private void SendCursor()
    {
        var packet = new ClientCursorPacket(cursorSeq++);

        if (Find.CurrentMap != null && !WorldRendererUtility.WorldSelected)
        {
            packet.map = (byte)Find.CurrentMap.Index;

            var icon = Find.MapUI?.designatorManager?.SelectedDesignator?.icon;
            int iconId = icon == null ? 0 :
                !MultiplayerData.icons.Contains(icon) ? 0 : MultiplayerData.icons.IndexOf(icon);
            packet.icon = (byte)iconId;

            var mapPosition = UI.MouseMapPosition();
            packet.x = mapPosition.x;
            packet.z = mapPosition.z;

            if (Find.Selector.dragBox.IsValidAndActive)
            {
                var dragVec = Find.Selector.dragBox.start;
                packet.flags |= ClientCursorPacket.FlagDrag;
                packet.dragX = dragVec.x;
                packet.dragZ = dragVec.z;
            }

            var designator = Find.MapUI?.designatorManager?.SelectedDesignator;

            if (designator is Designator_Place { PlacingDef: { } placingDef } place)
            {
                packet.flags |= ClientCursorPacket.FlagPlacing;
                packet.placingDefHash = placingDef.shortHash;
                packet.placingTerrain = placingDef is TerrainDef;
                packet.placingRot = (byte)place.placingRot.AsInt;
            }

            var dragger = Find.MapUI?.designatorManager?.Dragger;
            if (designator != null && dragger is { Dragging: true } && Find.DesignatorManager.SelectedStyle is { } style)
            {
                packet.flags |= ClientCursorPacket.FlagDesignationDrag;
                packet.designationDragX = (short)dragger.startDragCell.x;
                packet.designationDragZ = (short)dragger.startDragCell.z;
                packet.designationDragStyleHash = style.shortHash;
            }
        }

        Multiplayer.Client.Send(packet, reliable: false);
    }

    private void SendSelected()
    {
        if (Current.ProgramState != ProgramState.Playing) return;

        int mapId = Find.CurrentMap?.Index ?? -1;
        if (WorldRendererUtility.WorldSelected) mapId = -1;

        bool reset = false;

        if (mapId != lastMap)
        {
            reset = true;
            lastMap = mapId;
            lastSelected.Clear();
        }

        var selected = new HashSet<int>(Find.Selector.selected.OfType<Thing>().Select(t => t.thingIDNumber));

        var add = selected.Except(lastSelected).ToArray();
        var remove = lastSelected.Except(selected).ToArray();

        if (!reset && add.Length == 0 && remove.Length == 0) return;

        lastSelected = selected;

        Multiplayer.Client.Send(new ClientSelectedPacket
            { reset = reset, newlySelectedIds = add, unselectedIds = remove });
    }
}
