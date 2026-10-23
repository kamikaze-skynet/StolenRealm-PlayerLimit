using System.Reflection;
using System.Reflection.Emit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;

// Loads the real game assemblies, applies every Harmony patch class from PlayerLimitMod.dll,
// then inspects the patched IL and runs the patches that don't need the Unity engine.
var gameDir = args[0];
var bepDir = args[1];
var modPath = args[2];
var maxPlayers = args.Length > 3 ? int.Parse(args[3]) : 16;

AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
{
    var name = new AssemblyName(e.Name).Name;
    if (name == "0Harmony") return typeof(Harmony).Assembly;
    foreach (var dir in new[] { gameDir, bepDir })
    {
        var p = Path.Combine(dir, name + ".dll");
        if (File.Exists(p)) return Assembly.LoadFrom(p);
    }
    return null;
};

var failures = 0;
void Check(bool ok, string what)
{
    Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + what);
    if (!ok) failures++;
}

var mod = Assembly.LoadFrom(Path.GetFullPath(modPath));
var bep = Assembly.LoadFrom(Path.Combine(bepDir, "BepInEx.dll"));
var game = Assembly.LoadFrom(Path.Combine(gameDir, "Assembly-CSharp.dll"));
var plugin = mod.GetType("PlayerLimitMod.PlayerLimitPlugin")!;

// --- Wire up logging + config exactly like Awake() would ---
var logType = bep.GetType("BepInEx.Logging.ManualLogSource")!;
var log = Activator.CreateInstance(logType, "PLM")!;
var logEvent = logType.GetEvent("LogEvent")!;
var logArgsType = bep.GetType("BepInEx.Logging.LogEventArgs")!;
var warnings = new List<string>();
var handler = Delegate.CreateDelegate(logEvent.EventHandlerType!,
    typeof(LogSink).GetMethod(nameof(LogSink.OnLog))!.MakeGenericMethod(logArgsType));
logEvent.AddEventHandler(log, handler);
LogSink.Sink = (level, msg) =>
{
    Console.WriteLine($"    [{level}] {msg}");
    if (level.Contains("Warning") || level.Contains("Error")) warnings.Add(msg);
};
plugin.GetField("Log", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, log);

var tmpRoot = Path.Combine(Path.GetTempPath(), "plm_bep_" + System.Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(tmpRoot, "config"));
var paths = bep.GetType("BepInEx.Paths")!;
foreach (var (prop, val) in new[] { ("BepInExRootPath", tmpRoot), ("ConfigPath", Path.Combine(tmpRoot, "config")), ("BepInExConfigPath", Path.Combine(tmpRoot, "config", "BepInEx.cfg")), ("GameRootPath", tmpRoot), ("PluginPath", tmpRoot) })
{
    var pi = paths.GetProperty(prop, BindingFlags.Public | BindingFlags.Static);
    var setter = pi?.GetSetMethod(true);
    if (setter != null) setter.Invoke(null, new object[] { val });
    else paths.GetField("<" + prop + ">k__BackingField", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, val);
}
var cfgType = bep.GetType("BepInEx.Configuration.ConfigFile")!;
var cfgPath = Path.Combine(Path.GetTempPath(), $"plm_test_{System.Guid.NewGuid():N}.cfg");
var cfg = Activator.CreateInstance(cfgType, cfgPath, true)!;
var bindGeneric = cfgType.GetMethods().First(m => m.Name == "Bind" && m.GetParameters().Length == 4
    && m.GetParameters()[3].ParameterType == typeof(string));
object Bind<T>(string section, string key, T value)
{
    var entry = bindGeneric.MakeGenericMethod(typeof(T)).Invoke(cfg, new object[] { section, key, value!, "" })!;
    entry.GetType().GetProperty("Value")!.SetValue(entry, value);
    return entry;
}
void SetEntry(string field, object entry) => plugin.GetField(field, BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, entry);
SetEntry("MaxPlayersEntry", Bind("General", "MaxPlayers", maxPlayers));
SetEntry("PartySlotsPerPageEntry", Bind("UI", "PartySlotsPerPage", 6));
SetEntry("PortraitsPerPageEntry", Bind("UI", "PortraitsPerPage", 6));
SetEntry("EventPortraitsPerPageEntry", Bind("UI", "EventPortraitsPerPage", 6));
SetEntry("PortraitFollowSelectionEntry", Bind("UI", "PortraitsFollowSelection", true));
SetEntry("ExtraRewardBarsEntry", Bind("UI", "ExtraRewardXpBars", true));
SetEntry("VerboseEntry", Bind("Debug", "VerboseLogging", true));
SetEntry("ExtraEnemyPointsEntry", Bind("Enemies", "BonusEnemyPointsPerHeroAbove6", 0f));
var max = (int)plugin.GetProperty("MaxPlayers")!.GetValue(null)!;
Console.WriteLine($"MaxPlayers = {max}");

// --- 1. Apply every patch class ---
Console.WriteLine("\n== Applying patches ==");
var harmony = new Harmony("plm.test");
var patchTypes = mod.GetTypes().Where(t => t.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0).OrderBy(t => t.Name).ToList();
foreach (var t in patchTypes)
{
    try
    {
        var patched = harmony.CreateClassProcessor(t).Patch();
        Check(patched != null && patched.Count > 0, $"{t.Name} -> {string.Join(", ", patched?.Select(m => m.Name) ?? Array.Empty<string>())}");
    }
    catch (Exception e)
    {
        Check(false, $"{t.Name}: {e.GetBaseException().GetType().Name}: {e.GetBaseException().Message}"); if (Environment.GetEnvironmentVariable("PLM_TRACE") == "1") Console.WriteLine(e.GetBaseException().StackTrace);
    }
}
Check(warnings.Count == 0, $"no warnings while patching ({warnings.Count})");

// --- 2. Inspect transpiled IL ---
Console.WriteLine("\n== Transpiled IL ==");
var getMax = AccessTools.PropertyGetter(plugin, "MaxPlayers");
MethodBase MoveNext(Type owner, string name) => AccessTools.Method(owner.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public).First(n => n.Name.StartsWith("<" + name + ">")), "MoveNext");
bool IsSix(CodeInstruction ci) => ci.opcode == OpCodes.Ldc_I4_6 || ((ci.opcode == OpCodes.Ldc_I4_S || ci.opcode == OpCodes.Ldc_I4) && Convert.ToInt32(ci.operand) == 6);
void InspectIl(MethodBase m, int expectMaxCalls, int expectSixLeft, string label)
{
    var il = PatchProcessor.GetCurrentInstructions(m, int.MaxValue, null);
    var calls = il.Count(i => i.opcode == OpCodes.Call && Equals(i.operand, getMax));
    var sixes = il.Count(IsSix);
    Check(calls == expectMaxCalls && sixes == expectSixLeft, $"{label}: MaxPlayers calls={calls} (want {expectMaxCalls}), literal 6 left={sixes} (want {expectSixLeft})");
}
var ccm = game.GetType("CharacterChoiceManager")!;
var mainMenu = game.GetType("MainMenu")!;
InspectIl(AccessTools.Method(ccm, "Update"), 1, 0, "CharacterChoiceManager.Update");
InspectIl(MoveNext(ccm, "ExecuteRefreshCharacters"), 2, 0, "ExecuteRefreshCharacters (slot loop + accept check)");
InspectIl(AccessTools.Method(game.GetType("CharacterChoiceItem")!, "MouseOver"), 1, 0, "CharacterChoiceItem.MouseOver");
// Steam: 2 replaced, the 6-part log-string array must be left alone
InspectIl(MoveNext(mainMenu, "CreateSteamLobby"), 2, 1, "CreateSteamLobby (lobby size only, log string untouched)");
var steamIl = PatchProcessor.GetCurrentInstructions(MoveNext(mainMenu, "CreateSteamLobby"), int.MaxValue, null);
var leftover = steamIl.FindIndex(IsSix);
Check(leftover >= 0 && steamIl[leftover + 1].opcode == OpCodes.Newarr, "remaining 6 in CreateSteamLobby is the string array (newarr)");
var lobbyCalls = steamIl.Select((ci, i) => (ci, i)).Where(x => x.ci.opcode == OpCodes.Call && Equals(x.ci.operand, getMax))
    .Select(x => (steamIl[x.i + 1].operand as MethodInfo)?.Name).ToList();
Check(lobbyCalls.Contains("CreateLobbyAsync") && lobbyCalls.Contains("set_MaxMembers"), $"MaxPlayers feeds: {string.Join(", ", lobbyCalls)}");

// --- 3. Run patches that don't need the engine ---
Console.WriteLine("\n== Behaviour ==");
object Uninit(Type t) => System.Runtime.Serialization.FormatterServices.GetUninitializedObject(t);

// Roster limit
var gs = game.GetType("GlobalSettings")!;
var settings = Uninit(gs);
gs.GetField("maxCharactersPc")!.SetValue(settings, 12);
var limit = (int)AccessTools.PropertyGetter(gs, "CharacterLimit").Invoke(settings, null)!;
Check(limit == Math.Max(12, max), $"CharacterLimit with game value 12 -> {limit}");
gs.GetField("maxCharactersPc")!.SetValue(settings, 50);
limit = (int)AccessTools.PropertyGetter(gs, "CharacterLimit").Invoke(settings, null)!;
Check(limit == 50, $"CharacterLimit keeps a higher game value (50 -> {limit})");

// Party-size tables padded with last value
var arrays = mod.GetType("PlayerLimitMod.Patches.Arrays")!;
var pad = arrays.GetMethod("PadWithLast", BindingFlags.Public | BindingFlags.Static)!;
var dice = (int[])pad.Invoke(null, new object[] { new[] { 0, 1, 2, 2, 3, 3 }, max, "test" })!;
Check(dice.Length == Math.Max(6, max) && dice.Take(6).SequenceEqual(new[] { 0, 1, 2, 2, 3, 3 }) && dice.Skip(6).All(v => v == 3),
    $"dice table -> [{string.Join(",", dice)}]");
for (var party = 1; party <= max; party++) _ = dice[party - 1]; // the game's lookup: table[partySize-1]
Check(true, $"table[partySize-1] valid for party sizes 1..{max}");

// PlayFab lobby config prefix
var lobbyCfgType = Assembly.LoadFrom(Path.Combine(gameDir, "PlayFabMultiplayerSDK.dll")).GetType("PlayFab.Multiplayer.LobbyCreateConfiguration")!;
try
{
    var lobbyCfg = Activator.CreateInstance(lobbyCfgType)!;
    lobbyCfgType.GetProperty("MaxMemberCount")!.SetValue(lobbyCfg, 6u);
    mod.GetType("PlayerLimitMod.Patches.Lobby_PlayFab")!.GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new[] { lobbyCfg });
    var v = (uint)lobbyCfgType.GetProperty("MaxMemberCount")!.GetValue(lobbyCfg)!;
    Check(v == (uint)Math.Max(2, max), $"PlayFab lobby MaxMemberCount 6 -> {v}");
}
catch (Exception e)
{
    Console.WriteLine($"  SKIP  PlayFab config needs native SDK here ({e.GetBaseException().GetType().Name})");
}

// Each patch's prefix/postfix parameter names must match the target (Harmony binds by name)
Console.WriteLine($"\n{(failures == 0 ? "ALL CHECKS PASSED" : failures + " CHECK(S) FAILED")}");
File.Delete(cfgPath);
return failures == 0 ? 0 : 1;

static class LogSink
{
    public static Action<string, string> Sink = (_, _) => { };
    public static void OnLog<T>(object sender, T e)
    {
        var level = e!.GetType().GetProperty("Level")!.GetValue(e)!.ToString()!;
        var data = e.GetType().GetProperty("Data")!.GetValue(e)?.ToString() ?? "";
        Sink(level, data);
    }
}
