using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using PlayFab.Multiplayer;

namespace PlayerLimitMod.Patches
{
    /// <summary>
    /// Steam friends lobby: MainMenu.CreateSteamLobby calls CreateLobbyAsync(6) and sets MaxMembers = 6.
    /// Only those two constants are touched; the same method also builds a 6-part log string.
    /// </summary>
    [HarmonyPatch]
    internal static class Lobby_Steam
    {
        private static MethodBase TargetMethod() => Il.StateMachineMoveNext(typeof(MainMenu), "CreateSteamLobby");

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code) =>
            Il.ReplaceSix(code, Il.FeedsCall("CreateLobbyAsync", "set_MaxMembers"), 2, "MainMenu.CreateSteamLobby");
    }

    /// <summary>
    /// Public PlayFab lobby: the game creates it with MaxMemberCount = 6 inside a generic async method,
    /// so we adjust the configuration where it enters the PlayFab SDK (both overloads).
    /// </summary>
    [HarmonyPatch]
    internal static class Lobby_PlayFab
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
            typeof(PlayFabMultiplayer).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == nameof(PlayFabMultiplayer.CreateAndJoinLobby)
                            && m.GetParameters().Any(p => p.ParameterType == typeof(LobbyCreateConfiguration)));

        private static void Prefix(LobbyCreateConfiguration createConfiguration)
        {
            if (createConfiguration == null) return;
            var want = (uint)PlayerLimitPlugin.MaxPlayers;

            // Respect PlayFab's documented bounds if the SDK exposes them.
            var lower = ReadLimit("LobbyMaxMemberCountLowerLimit", 2u);
            var upper = ReadLimit("LobbyMaxMemberCountUpperLimit", 128u);
            if (want < lower) want = lower;
            if (want > upper) want = upper;

            createConfiguration.MaxMemberCount = want;
            PlayerLimitPlugin.Debug($"PlayFab lobby MaxMemberCount -> {want}");
        }

        private static uint ReadLimit(string field, uint fallback)
        {
            try
            {
                var f = AccessTools.Field(typeof(PlayFabMultiplayer), field);
                if (f != null && f.GetValue(null) is uint v && v > 0) return v;
            }
            catch
            {
                // fall through
            }
            return fallback;
        }
    }
}
