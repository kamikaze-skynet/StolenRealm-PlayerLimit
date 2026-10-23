using System.Linq;
using HarmonyLib;
using PlayerLimitMod.UI;

namespace PlayerLimitMod.Patches
{
    /// <summary>
    /// The game binds exactly 6 portraits every frame. Replace that with a version that keeps
    /// one portrait per hero and shows them a page at a time.
    /// </summary>
    [HarmonyPatch(typeof(PortraitManager), "UpdatePortraits")]
    internal static class Portraits_Update
    {
        internal static readonly AccessTools.FieldRef<PortraitManager, Portrait[]> Portraits =
            AccessTools.FieldRefAccess<PortraitManager, Portrait[]>("portraits");

        private static bool Prefix(PortraitManager __instance)
        {
            var nm = NetworkingManager.Instance;
            var party = nm?.PartyCharacters;
            if (party == null) return false; // same early-out as the game

            ref var portraits = ref Portraits(__instance);
            if (portraits == null || portraits.Length == 0) return true;

            PortraitPager.Update(__instance, ref portraits, party);
            return false;
        }
    }

    internal static class XpBars
    {
        /// <summary>Clone XP bars under <paramref name="root"/> until there is one per controlled hero. Returns true if any were added.</summary>
        public static bool Ensure(UnityEngine.Component root, string where)
        {
            if (!PlayerLimitPlugin.ExtraRewardBarsEntry.Value || !GameLogic.instance || !root) return false;
            var heroes = GameLogic.instance.ControlledCharacters?.Count() ?? 0;
            var meters = root.GetComponentsInChildren<CharXpMeter>(true);
            if (meters.Length == 0 || heroes <= meters.Length) return false;

            var template = meters[0]; // copy a bar the game is known to use
            var last = meters[meters.Length - 1];
            var parent = last.transform.parent;
            // Only clone into a layout group; otherwise the copies would stack on top of each other.
            if (!parent || !parent.GetComponent<UnityEngine.UI.LayoutGroup>())
            {
                PlayerLimitPlugin.Debug($"{where}: XP bars are not in a layout group; leaving them alone");
                return false;
            }

            for (var i = meters.Length; i < heroes; i++)
            {
                var copy = UnityEngine.Object.Instantiate(template.gameObject, parent);
                copy.name = template.gameObject.name + " (PLM " + (i + 1) + ")";
                copy.transform.SetSiblingIndex(last.transform.GetSiblingIndex() + (i - meters.Length) + 1);
            }
            PlayerLimitPlugin.Debug($"{where}: added {heroes - meters.Length} XP bars");
            return true;
        }
    }

    /// <summary>Extra XP bars on the adventure results screen for heroes 7+.</summary>
    [HarmonyPatch(typeof(AdventureRewards), "ExecuteOpenAdventureRewards")]
    internal static class Rewards_ExtraXpBars
    {
        private static void Prefix(AdventureRewards __instance) =>
            XpBars.Ensure((UnityEngine.Component)(object)__instance, "Adventure results");
    }

    /// <summary>Extra XP bars on the post-battle screen for heroes 7+ (the game caches its bar list on load).</summary>
    [HarmonyPatch(typeof(PostBattleManager), nameof(PostBattleManager.OpenPostBattleMenu))]
    internal static class PostBattle_ExtraXpBars
    {
        private static readonly AccessTools.FieldRef<PostBattleManager, CharXpMeter[]> Meters =
            AccessTools.FieldRefAccess<PostBattleManager, CharXpMeter[]>("xpMeters");

        private static void Prefix(PostBattleManager __instance)
        {
            var root = (UnityEngine.Component)(object)__instance;
            if (XpBars.Ensure(root, "Post-battle"))
                Meters(__instance) = root.GetComponentsInChildren<CharXpMeter>(true);
        }
    }
}
