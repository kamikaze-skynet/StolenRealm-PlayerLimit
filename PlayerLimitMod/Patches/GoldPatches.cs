using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PlayerLimitMod.UI;
using UnityEngine;

namespace PlayerLimitMod.Patches
{
    /// <summary>
    /// Send Gold window: the recipient list has a fixed set of slots (one per other hero in a 6-party) and the
    /// game caches it, so recipients past those slots never appear. Add a slot per recipient, then page the list
    /// at the window's original size with a [◀ Page 1/3 ▶] bar under it.
    /// </summary>
    [HarmonyPatch(typeof(GoldSendManager), nameof(GoldSendManager.ShowGoldSendWindow))]
    internal static class Gold_SendWindow
    {
        private static readonly AccessTools.FieldRef<GoldSendManager, GoldSendItem[]> Cache =
            AccessTools.FieldRefAccess<GoldSendManager, GoldSendItem[]>("_GoldSendItems");

        private static bool _reported;

        /// <summary>A row the game can fill: it needs its name label.</summary>
        private static bool Usable(GoldSendItem item) => item && item.CharacterName;

        private static void Prefix(GoldSendManager __instance)
        {
            var all = __instance.GoldSendItems;
            if (all == null || all.Length == 0) return;

            // The window ships with a spare row that has no name label (the base game never fills it with 6
            // heroes). Filling it throws and the window never opens, so keep it out of the list entirely.
            var rows = all.Where(Usable).ToList();
            foreach (var broken in all.Where(i => i && !Usable(i)))
                if (broken.gameObject.activeSelf) broken.gameObject.SetActive(false);
            if (!_reported)
            {
                _reported = true;
                PlayerLimitPlugin.Log.LogInfo($"Send Gold window: {all.Length} recipient rows, {all.Length - rows.Count} unusable (skipped)");
            }
            if (rows.Count == 0) return;
            GoldPager.RememberPageSize(rows.Count);

            var sender = GameLogic.instance ? GameLogic.instance.CurrentlySelectedCharacter : null;
            var recipients = NetworkingManager.Instance?.PartyCharacters?.Count(c => c != null && c != sender) ?? 0;

            if (recipients > rows.Count)
            {
                var template = rows[0]; // a known-good row
                var last = rows[rows.Count - 1];
                var parent = last.transform.parent;
                var insertAt = last.transform.GetSiblingIndex() + 1;
                for (var i = rows.Count; i < recipients; i++)
                {
                    var go = Object.Instantiate(template.gameObject, parent);
                    go.name = template.gameObject.name + " (PLM " + (i + 1) + ")";
                    go.SetActive(false);
                    go.transform.SetSiblingIndex(insertAt++);
                    var row = go.GetComponent<GoldSendItem>();
                    if (Usable(row)) rows.Add(row);
                }
                PlayerLimitPlugin.Debug($"Gold recipient list grown to {rows.Count} rows");
            }

            if (rows.Count != all.Length || recipients > all.Length)
                Cache(__instance) = rows.ToArray(); // the game reads its cached list; give it only usable rows
        }

        private static void Postfix(GoldSendManager __instance)
        {
            var sender = GameLogic.instance ? GameLogic.instance.CurrentlySelectedCharacter : null;
            var recipients = NetworkingManager.Instance?.PartyCharacters?.Count(c => c != sender) ?? 0;
            GoldPager.Open(__instance, recipients);
        }
    }
}

namespace PlayerLimitMod.UI
{
    internal static class GoldPager
    {
        private static int _pageSize; // the window's original slot count
        private static GoldSendManager _mgr;
        private static NavBar _nav;
        private static int _count, _page;

        public static void RememberPageSize(int vanillaSlots)
        {
            if (_pageSize == 0) _pageSize = vanillaSlots;
        }

        public static void Open(GoldSendManager mgr, int recipients)
        {
            _mgr = mgr;
            _count = recipients;
            _page = 0;
            Apply();
        }

        private static void Step(int delta)
        {
            if (!_mgr) return;
            var pages = Pages();
            var target = Mathf.Clamp(_page + delta, 0, pages - 1);
            if (target == _page) return;
            _page = target;
            Apply();
        }

        private static int Pages() => Mathf.Max(1, Mathf.CeilToInt(_count / (float)Mathf.Max(1, _pageSize)));

        private static void Apply()
        {
            var items = _mgr ? _mgr.GoldSendItems : null;
            if (items == null || items.Length == 0 || _pageSize <= 0) return;

            var pages = Pages();
            _page = Mathf.Clamp(_page, 0, pages - 1);
            var first = _page * _pageSize;
            for (var i = 0; i < items.Length; i++)
            {
                var show = i < _count && i >= first && i < first + _pageSize;
                if (items[i].gameObject.activeSelf != show) items[i].gameObject.SetActive(show);
            }

            var list = items[0].transform.parent;
            if (!list) return;
            var host = list.parent ? list.parent : list;
            if (pages > 1 && (_nav == null || !_nav.Alive || _nav.Root.parent != host))
            {
                _nav = new NavBar("GoldNav", host, list, vertical: false, step: Step);
                var wheel = list.GetComponent<WheelPager>();
                if (!wheel) wheel = list.gameObject.AddComponent<WheelPager>();
                wheel.OnStep = Step;
            }
            if (_nav == null || !_nav.Alive) return;
            _nav.Show(_page, pages);
            if (pages <= 1 || !(list is RectTransform rt)) return;

            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            _nav.PlaceAtWorld((corners[0] + corners[3]) * 0.5f, new Vector2(0.5f, 1f), new Vector2(0, -6));
        }
    }
}
