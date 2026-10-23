using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using PlayerLimitMod.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PlayerLimitMod.Patches
{
    /// <summary>
    /// Map events ("Choose Character", "Choose As Party", "Choose For Your Characters") fill a row that holds
    /// exactly 6 character tiles, so heroes 7+ never appear. Add tiles as needed and page the row.
    /// </summary>
    [HarmonyPatch]
    internal static class Event_CharacterRow
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(EventWindow), nameof(EventWindow.ChooseCharacter));
            yield return AccessTools.Method(typeof(EventWindow), nameof(EventWindow.ChooseOptionAsParty));
        }

        // Both targets are (PartyEvent, List<Character>) but name the list differently, so use __args.
        private static void Prefix(EventWindow __instance, object[] __args)
        {
            var count = (__args.Length > 1 && __args[1] is IList list) ? list.Count : 0;
            EventPager.EnsureTiles(__instance, count);
            EventPager.NewRow(count);
        }

        private static void Postfix(EventWindow __instance) => EventPager.Apply(__instance);
    }

    /// <summary>The small vote-colour dots on each tile stop at 6 players; add dots for players 7+.</summary>
    [HarmonyPatch(typeof(VotingColorTiles), nameof(VotingColorTiles.UpdateTiles))]
    internal static class Event_VoteDots
    {
        private static void Prefix(VotingColorTiles __instance, List<int> activeIndicies)
        {
            if (activeIndicies == null || activeIndicies.Count == 0) return;
            var need = activeIndicies.Max() + 1;
            var holder = __instance.transform;
            if (holder.childCount == 0 || need <= holder.childCount) return;

            Palette.Ensure();
            var colors = GUIManager.instance ? GUIManager.instance.ConnectionColors : null;
            var template = holder.GetChild(holder.childCount - 1).gameObject;
            for (var i = holder.childCount; i < need; i++)
            {
                var dot = Object.Instantiate(template, holder);
                dot.name = template.name + " (PLM " + (i + 1) + ")";
                var img = dot.GetComponent<Image>();
                if (img && colors != null && i < colors.Count) img.color = colors[i];
            }
        }
    }
}
