using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using PlayerLimitMod.UI;

namespace PlayerLimitMod.Patches
{
    internal static class PartySelect
    {
        internal static readonly AccessTools.FieldRef<CharacterChoiceManager, int> PartyMax =
            AccessTools.FieldRefAccess<CharacterChoiceManager, int>("partyMax");

        internal static readonly AccessTools.FieldRef<CharacterChoiceManager, List<CharacterChoiceItem>> PartySlots =
            AccessTools.FieldRefAccess<CharacterChoiceManager, List<CharacterChoiceItem>>("partySlots");

        internal static readonly AccessTools.FieldRef<CharacterChoiceManager, List<Character>> PartyChars =
            AccessTools.FieldRefAccess<CharacterChoiceManager, List<Character>>("partyChars");

        internal static void ApplyPartyMax(CharacterChoiceManager mgr)
        {
            if (mgr != null) PartyMax(mgr) = PlayerLimitPlugin.MaxPlayers;
        }
    }

    /// <summary>partyMax drives CanAddCharacter; keep it at the configured size.</summary>
    [HarmonyPatch]
    internal static class PartySelect_PartyMax
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(CharacterChoiceManager), "Start");
            yield return AccessTools.Method(typeof(CharacterChoiceManager), nameof(CharacterChoiceManager.RefreshCharacters));
            yield return AccessTools.Method(typeof(CharacterChoiceManager), nameof(CharacterChoiceManager.ToggleSelectedCharacter));
        }

        private static void Prefix(CharacterChoiceManager __instance) => PartySelect.ApplyPartyMax(__instance);
    }

    /// <summary>Accept button: "party count &lt;= 6" check in Update.</summary>
    [HarmonyPatch(typeof(CharacterChoiceManager), "Update")]
    internal static class PartySelect_UpdateAcceptButton
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code) =>
            Il.ReplaceSix(code, Il.FeedsComparison, 1, "CharacterChoiceManager.Update");
    }

    /// <summary>
    /// RefreshCharacters runs as a coroutine. Its body creates exactly 6 party slots and disables Accept above 6.
    /// We swap both 6s for MaxPlayers, then paginate once the coroutine has finished laying out the slots.
    /// </summary>
    [HarmonyPatch]
    internal static class PartySelect_RefreshCoroutine
    {
        private static FieldInfo _thisField;

        private static MethodBase TargetMethod() =>
            Il.StateMachineMoveNext(typeof(CharacterChoiceManager), "ExecuteRefreshCharacters");

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code) =>
            Il.ReplaceSix(code, Il.FeedsComparison, 2, "CharacterChoiceManager.ExecuteRefreshCharacters");

        private static void Postfix(object __instance, bool __result)
        {
            if (__result) return; // still running (waiting on network-loaded characters)
            if (_thisField == null) _thisField = AccessTools.Field(__instance.GetType(), "<>4__this");
            var mgr = _thisField?.GetValue(__instance) as CharacterChoiceManager;
            if (mgr != null) PartyPager.Apply(mgr);
        }
    }

    /// <summary>The "Add To Party" mouse hint is hidden once 6 heroes are in the party.</summary>
    [HarmonyPatch(typeof(CharacterChoiceItem), nameof(CharacterChoiceItem.MouseOver))]
    internal static class PartySelect_MouseOverHint
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code) =>
            Il.ReplaceSix(code, Il.FeedsComparison, 1, "CharacterChoiceItem.MouseOver");
    }

    /// <summary>Make sure one player can own enough heroes to fill the party.</summary>
    [HarmonyPatch(typeof(GlobalSettings), nameof(GlobalSettings.CharacterLimit), MethodType.Getter)]
    internal static class Roster_CharacterLimit
    {
        private static void Postfix(ref int __result)
        {
            if (__result < PlayerLimitPlugin.MaxPlayers) __result = PlayerLimitPlugin.MaxPlayers;
        }
    }
}
