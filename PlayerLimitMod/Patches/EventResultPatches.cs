using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace PlayerLimitMod.Patches
{
    /// <summary>
    /// "Everyone rolls" events list each hero's result in a fixed set of rows (6). With a bigger party the game
    /// runs out of rows: heroes past the 6th never show, and the event routine hits an empty row and throws,
    /// leaving everyone stuck on "Pending...". Give the list one row per hero before it is used.
    /// </summary>
    internal static class EventResultRows
    {
        public static void Ensure(EventWindow window)
        {
            if (!window) return;
            var rows = window.AllCharactersResultItems;
            if (rows == null || rows.Length == 0) return;
            var needed = NetworkingManager.Instance?.PartyCharacters?.Count ?? 0;
            if (rows.Length >= needed) return;

            var template = rows[0]; // copy a row the game is known to use
            var last = rows[rows.Length - 1];
            var parent = last.transform.parent;
            var grown = new AllCharactersResultItem[needed];
            System.Array.Copy(rows, grown, rows.Length);
            var insertAt = last.transform.GetSiblingIndex() + 1;
            for (var i = rows.Length; i < needed; i++)
            {
                var go = Object.Instantiate(template.gameObject, parent);
                go.name = template.gameObject.name + " (PLM " + (i + 1) + ")";
                go.SetActive(false);
                go.transform.SetSiblingIndex(insertAt++);
                var row = go.GetComponent<AllCharactersResultItem>();
                row.Character = null; // clear any copied text/state
                grown[i] = row;
            }
            window.AllCharactersResultItems = grown;
            PlayerLimitPlugin.Debug($"Event result rows grown {rows.Length} -> {needed}");
        }
    }

    /// <summary>Before an event starts (host and single player).</summary>
    [HarmonyPatch(typeof(EventWindow), "ExecuteStartEvent")]
    internal static class Event_ResultRows_Start
    {
        private static void Prefix(EventWindow __instance) => EventResultRows.Ensure(__instance);
    }

    /// <summary>When a hero's result arrives over the network (other players' screens).</summary>
    [HarmonyPatch(typeof(EventOptionSelectionItem), nameof(EventOptionSelectionItem.ShowAllCharacterResultEffectText))]
    internal static class Event_ResultRows_Show
    {
        private static void Prefix() => EventResultRows.Ensure(LoadableUIWindow<EventWindow>.Instance);
    }
}
