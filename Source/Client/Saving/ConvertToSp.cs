using Verse;
using RimWorld;
using Verse.Profile;

namespace Multiplayer.Client.Saving;

public class ConvertToSp
{
    public static void DoConvert()
    {
        LongEventHandler.QueueLongEvent(() =>
        {
            SaveReplay();
            PrepareSingleplayer();
            PrepareLoading();
        }, "Play", "MpConvertingToSp", true, null);
    }

    private static void SaveReplay()
    {
        const string suffix = "-preconvert";
        var saveName = $"{GenFile.SanitizedFileName(Multiplayer.session.gameName)}{suffix}";
        Autosaving.SaveGameToFile_Overwrite(saveName, false);
    }

    // Used after losing the connection, when the multiplayer session is already stopped but the game is still loaded.
    // Lets the client keep the game instead of losing everything since the host's last autosave (#937).
    public static void DoConvertAfterDisconnect(Faction spectator)
    {
        LongEventHandler.QueueLongEvent(() =>
        {
            PrepareSingleplayer(spectator);
            PrepareLoading();
        }, "Play", "MpConvertingToSp", true, null);
    }

    private static void PrepareSingleplayer() => PrepareSingleplayer(Multiplayer.WorldComp.spectatorFaction);

    private static void PrepareSingleplayer(Faction spectator)
    {
        Find.GameInfo.permadeathMode = false;

        // Remove spectator faction
        if (spectator != null)
        {
            spectator.RemoveAllRelations();
            Find.FactionManager.allFactions.Remove(spectator);
        }
    }

    private static void PrepareLoading()
    {
        Multiplayer.StopMultiplayer();

        var doc = SaveLoad.SaveGameToDoc();
        MemoryUtility.ClearAllMapsAndWorld();

        Current.Game = new Game
        {
            InitData = new GameInitData
            {
                gameToLoad = "play"
            }
        };

        LoadPatch.gameToLoad = new TempGameData(doc, new byte[0]);
    }
}
