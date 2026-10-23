using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace PlayerLimitMod
{
    [BepInPlugin(Guid, Name, Version)]
    public class PlayerLimitPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.stolenrealm.playerlimit";
        public const string Name = "Player Limit Mod";
        public const string Version = "2.1.3";

        /// <summary>The game's own party cap.</summary>
        public const int VanillaLimit = 6;

        internal static ManualLogSource Log;
        internal static ConfigEntry<int> MaxPlayersEntry;
        internal static ConfigEntry<int> PartySlotsPerPageEntry;
        internal static ConfigEntry<int> PortraitsPerPageEntry;
        internal static ConfigEntry<int> EventPortraitsPerPageEntry;
        internal static ConfigEntry<bool> PortraitFollowSelectionEntry;
        internal static ConfigEntry<bool> ExtraRewardBarsEntry;
        internal static ConfigEntry<bool> VerboseEntry;
        internal static ConfigEntry<float> ExtraEnemyPointsEntry;

        /// <summary>Configured party size. Read live; called from transpiled game code, so keep it cheap and public.</summary>
        public static int MaxPlayers => Mathf.Clamp(MaxPlayersEntry?.Value ?? VanillaLimit, 1, 30);

        public static float ExtraEnemyPointsPerHero => Mathf.Clamp(ExtraEnemyPointsEntry?.Value ?? 0f, 0f, 10f);

        public static int PartySlotsPerPage => Mathf.Clamp(PartySlotsPerPageEntry?.Value ?? 6, 1, 12);
        public static int PortraitsPerPage => Mathf.Clamp(PortraitsPerPageEntry?.Value ?? 6, 1, 12);
        public static int EventPortraitsPerPage => Mathf.Clamp(EventPortraitsPerPageEntry?.Value ?? 6, 1, 12);

        internal static void Debug(string msg)
        {
            if (VerboseEntry != null && VerboseEntry.Value) Log.LogInfo(msg);
        }

        private void Awake()
        {
            Log = Logger;

            MaxPlayersEntry = Config.Bind("General", "MaxPlayers", 16,
                new ConfigDescription(
                    "Maximum party size (heroes in the party) and lobby size. Vanilla is 6. " +
                    "Everyone in a multiplayer session needs the mod with the same value. Restart the game after changing.",
                    new AcceptableValueRange<int>(1, 30)));

            ExtraEnemyPointsEntry = Config.Bind("Enemies", "BonusEnemyPointsPerHeroAbove6", 0f,
                new ConfigDescription(
                    "OPTIONAL extra enemies for big parties. 0 (default) = the game's normal enemy counts. " +
                    "Each hero beyond 6 adds this many enemy points when a battle's enemies are rolled, on top of the game's own +2 per hero " +
                    "(2 = about the same enemies-per-hero as a full 6-party). The host's setting decides.",
                    new AcceptableValueRange<float>(0f, 10f)));

            PartySlotsPerPageEntry = Config.Bind("UI", "PartySlotsPerPage", 6,
                new ConfigDescription("Party slots shown per page on the character select screen.",
                    new AcceptableValueRange<int>(1, 12)));

            PortraitsPerPageEntry = Config.Bind("UI", "PortraitsPerPage", 6,
                new ConfigDescription("Hero portraits shown per page in the left-hand portrait column. " +
                                      "Values above 6 may run off small screens.",
                    new AcceptableValueRange<int>(1, 12)));

            EventPortraitsPerPageEntry = Config.Bind("UI", "EventPortraitsPerPage", 6,
                new ConfigDescription("Hero tiles shown per page in map events (Choose Character / Choose As Party). " +
                                      "The row is sized for 6; higher values may overflow it.",
                    new AcceptableValueRange<int>(1, 12)));

            PortraitFollowSelectionEntry = Config.Bind("UI", "PortraitsFollowSelection", true,
                "Automatically flip the portrait column to the page holding the hero you select.");

            ExtraRewardBarsEntry = Config.Bind("UI", "ExtraRewardXpBars", true,
                "Add XP bars on the adventure results screen for heroes beyond the game's 6.");

            VerboseEntry = Config.Bind("Debug", "VerboseLogging", false,
                "Log extra detail to BepInEx/LogOutput.log.");

            Log.LogInfo($"{Name} v{Version} - game version {Application.version} - MaxPlayers = {MaxPlayers}");

            var harmony = new Harmony(Guid);
            var ok = new List<string>();
            var failed = new List<string>();

            // Apply each patch class on its own so one game change can't take the whole mod down.
            foreach (var type in typeof(PlayerLimitPlugin).Assembly.GetTypes()
                         .Where(t => t.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0)
                         .OrderBy(t => t.Name))
            {
                try
                {
                    var patched = harmony.CreateClassProcessor(type).Patch();
                    if (patched == null || patched.Count == 0)
                        failed.Add(type.Name + " (no target found)");
                    else
                        ok.Add(type.Name);
                }
                catch (Exception e)
                {
                    failed.Add(type.Name);
                    Log.LogError($"Patch {type.Name} failed: {e.GetBaseException().Message}");
                    Debug(e.ToString());
                }
            }

            Log.LogInfo($"Patches applied: {ok.Count}/{ok.Count + failed.Count}");
            if (failed.Count > 0)
                Log.LogWarning("Patches NOT applied (the game may have changed): " + string.Join(", ", failed));
        }
    }
}
