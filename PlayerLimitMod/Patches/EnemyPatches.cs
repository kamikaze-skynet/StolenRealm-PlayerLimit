using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace PlayerLimitMod.Patches
{
    /// <summary>
    /// More enemies for big parties. The game rolls each battle's enemies from a points budget:
    /// 12 base + 2 per hero after the first. That keeps growing past 6 heroes but more slowly per hero
    /// (a 16-hero party gets ~2.6 points per hero vs ~3.7 for a full 6). This adds
    /// <c>Enemies.ExtraPointsPerHeroAbove6</c> points for every hero beyond 6 when enemies are rolled.
    /// Only the budget handed to the roll is changed (nothing stored), and battles the game builds with a
    /// fixed lineup (bosses, scripted fights) are left as designed.
    /// </summary>
    [HarmonyPatch(typeof(GameLogic), nameof(GameLogic.SetupBattleInfo))]
    internal static class Enemies_MoreForBigParties
    {
        private static void Prefix(Burst2Flame.BattleInfo battle, ref int pointsToSpend)
        {
            if (pointsToSpend <= 0 || battle == null) return; // boss / no-fill battles
            var bonus = Bonus();
            if (bonus <= 0) return;
            pointsToSpend += bonus;
            PlayerLimitPlugin.Debug($"Battle points {pointsToSpend - bonus} -> {pointsToSpend} (+{bonus} for {Arrays.PartyCount()} heroes)");
        }

        public static int Bonus()
        {
            var perHero = PlayerLimitPlugin.ExtraEnemyPointsPerHero;
            var over = Arrays.PartyCount() - PlayerLimitPlugin.VanillaLimit;
            if (perHero <= 0f || over <= 0) return 0;
            return Mathf.RoundToInt(over * perHero);
        }
    }

    /// <summary>
    /// Enemies spawn within a radius of the battle centre. With a big party plus extra enemies that area can
    /// fill up, and the game would then hand a null hex to enemy setup. Fall back to the nearest free hex in
    /// the room instead.
    /// </summary>
    [HarmonyPatch(typeof(GameLogic), nameof(GameLogic.GetSpawnPoint))]
    internal static class Enemies_SpawnFallback
    {
        private static void Postfix(GameLogic __instance, System.Collections.Generic.List<HexCell> cells, ref HexCell __result)
        {
            if (__result != null) return;
            var room = WorldMapGenerator.instance ? WorldMapGenerator.instance.CurrentRoom : null;
            var hexes = room != null ? room.Hexes : null;
            if (hexes == null || hexes.Count == 0) return;

            // Prefer hexes close to the original spawn area so enemies still start near the fight.
            var centre = cells != null && cells.Count > 0 && cells[0] ? cells[0].transform.position : Vector3.zero;
            __result = hexes
                .Where(h => h && h.IsValid && h.IsWalkable && !__instance.SpawnHexUsed(h))
                .OrderBy(h => (h.transform.position - centre).sqrMagnitude)
                .FirstOrDefault();
            if (__result != null) PlayerLimitPlugin.Debug("Enemy spawn area full; used a nearby free hex");
            else PlayerLimitPlugin.Log.LogWarning("No free hex left in the room for an enemy spawn");
        }
    }
}
