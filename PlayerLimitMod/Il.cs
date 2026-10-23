using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace PlayerLimitMod
{
    /// <summary>Helpers for swapping the game's hardcoded "6" for the configured party size.</summary>
    internal static class Il
    {
        private static readonly MethodInfo MaxPlayersGetter =
            AccessTools.PropertyGetter(typeof(PlayerLimitPlugin), nameof(PlayerLimitPlugin.MaxPlayers));

        private static readonly HashSet<OpCode> Comparisons = new HashSet<OpCode>
        {
            OpCodes.Cgt, OpCodes.Cgt_Un, OpCodes.Clt, OpCodes.Clt_Un, OpCodes.Ceq,
            OpCodes.Blt, OpCodes.Blt_S, OpCodes.Blt_Un, OpCodes.Blt_Un_S,
            OpCodes.Ble, OpCodes.Ble_S, OpCodes.Ble_Un, OpCodes.Ble_Un_S,
            OpCodes.Bgt, OpCodes.Bgt_S, OpCodes.Bgt_Un, OpCodes.Bgt_Un_S,
            OpCodes.Bge, OpCodes.Bge_S, OpCodes.Bge_Un, OpCodes.Bge_Un_S,
        };

        public static bool IsSix(CodeInstruction ci) =>
            ci.opcode == OpCodes.Ldc_I4_6 ||
            ((ci.opcode == OpCodes.Ldc_I4_S || ci.opcode == OpCodes.Ldc_I4) && Convert.ToInt32(ci.operand) == 6);

        /// <summary>A "6" that is immediately compared against (e.g. count &lt;= 6, i &lt; 6).</summary>
        public static bool FeedsComparison(List<CodeInstruction> code, int i) =>
            i + 1 < code.Count && Comparisons.Contains(code[i + 1].opcode);

        /// <summary>A "6" passed straight into a call to the named method.</summary>
        public static Func<List<CodeInstruction>, int, bool> FeedsCall(params string[] methodNames) =>
            (code, i) => i + 1 < code.Count && code[i + 1].operand is MethodInfo m && methodNames.Contains(m.Name);

        /// <summary>
        /// Replace matching "6" constants with a call to <see cref="PlayerLimitPlugin.MaxPlayers"/>.
        /// Edits in place so jump labels and exception blocks on the instruction survive.
        /// </summary>
        public static IEnumerable<CodeInstruction> ReplaceSix(IEnumerable<CodeInstruction> instructions,
            Func<List<CodeInstruction>, int, bool> filter, int expected, string where)
        {
            var code = instructions.ToList();
            var replaced = 0;
            for (var i = 0; i < code.Count; i++)
            {
                if (!IsSix(code[i]) || !filter(code, i)) continue;
                code[i].opcode = OpCodes.Call;
                code[i].operand = MaxPlayersGetter;
                replaced++;
            }

            if (replaced != expected)
                PlayerLimitPlugin.Log.LogWarning($"{where}: expected to replace {expected} party-size constant(s) but found {replaced}. The game may have changed.");
            else
                PlayerLimitPlugin.Debug($"{where}: replaced {replaced} party-size constant(s)");
            return code;
        }

        /// <summary>Find the compiler-generated MoveNext for an iterator/async method by its source name.</summary>
        public static MethodBase StateMachineMoveNext(Type owner, string sourceMethodName)
        {
            var nested = owner.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(t => t.Name.StartsWith("<" + sourceMethodName + ">", StringComparison.Ordinal));
            return nested == null ? null : AccessTools.Method(nested, "MoveNext");
        }
    }
}
