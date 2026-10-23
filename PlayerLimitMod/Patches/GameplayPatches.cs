using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace PlayerLimitMod.Patches
{
    internal static class Arrays
    {
        /// <summary>Grow an int table indexed by party size, repeating its last value (same balance as a full 6-party).</summary>
        public static int[] PadWithLast(int[] table, int length, string what)
        {
            if (table == null || table.Length == 0 || table.Length >= length) return table;
            var grown = new int[length];
            Array.Copy(table, grown, table.Length);
            for (var i = table.Length; i < length; i++) grown[i] = table[table.Length - 1];
            PlayerLimitPlugin.Debug($"{what}: extended {table.Length} -> {length} entries");
            return grown;
        }

        public static int PartyCount()
        {
            var nm = NetworkingManager.Instance;
            return nm?.PartyCharacters?.Count ?? 0;
        }
    }

    /// <summary>
    /// Map events look up PartyCountRollModifiers[partySize - 1] (6 entries) - a 7th hero crashes event dice rolls.
    /// </summary>
    [HarmonyPatch(typeof(EventResultSetting), nameof(EventResultSetting.RollLimitWithCharacterCountModifier), MethodType.Getter)]
    internal static class Event_DiceModifierTable
    {
        private static void Prefix()
        {
            var dice = DiceRollingManager.instance;
            if (!dice) return;
            var need = Mathf.Max(PlayerLimitPlugin.MaxPlayers, Arrays.PartyCount());
            dice.PartyCountRollModifiers = Arrays.PadWithLast(dice.PartyCountRollModifiers, need, "PartyCountRollModifiers");
        }
    }

    /// <summary>Battle pickups look up BattleDropSpawnCountPerPlayer[partySize - 1].</summary>
    [HarmonyPatch(typeof(BattleManager), nameof(BattleManager.CheckBattleDrops))]
    internal static class Battle_DropTable
    {
        private static void Prefix(BattleManager __instance)
        {
            var need = Mathf.Max(PlayerLimitPlugin.MaxPlayers, Arrays.PartyCount());
            __instance.BattleDropSpawnCountPerPlayer = Arrays.PadWithLast(__instance.BattleDropSpawnCountPerPlayer, need, "BattleDropSpawnCountPerPlayer");
        }
    }

    /// <summary>
    /// Player colors: the palette holds 6 slots per connection for 6 connections and anything past that
    /// falls back to a blank color. Extend the palette with distinct hues and allow 7+ connections.
    /// </summary>
    internal static class Palette
    {
        public static void Ensure()
        {
            var gui = GUIManager.instance;
            if (!gui || gui.ConnectionColors == null || gui.ConnectionColors.Count == 0) return;
            var need = Mathf.Max(36, PlayerLimitPlugin.MaxPlayers * 6);
            var list = gui.ConnectionColors;
            if (list.Count >= need) return;

            for (var i = list.Count; i < need; i++)
            {
                // Golden-ratio hue walk gives well-separated colors; offset so they differ from the game's first picks.
                var hue = (0.13f + i * 0.61803398f) % 1f;
                var sat = (i / 6) % 2 == 0 ? 0.70f : 0.50f;
                list.Add(Color.HSVToRGB(hue, sat, 0.95f));
            }
            PlayerLimitPlugin.Debug($"ConnectionColors extended to {list.Count}");
        }
    }

    [HarmonyPatch(typeof(Root), nameof(Root.GetPlayerControllerColor))]
    internal static class Colors_PlayerControllerColor
    {
        private static bool Prefix(Root __instance, int owner, int controllerPlayerId, ref Color __result)
        {
            Palette.Ensure();
            if (controllerPlayerId >= 6) return true; // per-PC controller slots are still 6; let the game handle it

            var connectionIndex = __instance.ActiveConnections.Count(kv => kv.Key < owner);
            if (connectionIndex < 6) return true; // unchanged vanilla path

            var colors = GUIManager.instance.ConnectionColors;
            var index = connectionIndex * 6 + controllerPlayerId;
            __result = index < colors.Count ? colors[index] : GUIManager.instance.NoCharacterColor;
            return false;
        }
    }

    [HarmonyPatch(typeof(Root), nameof(Root.PartyCharactersColor), MethodType.Getter)]
    internal static class Colors_PartyCharactersColor
    {
        private static void Prefix() => Palette.Ensure();
    }
}
