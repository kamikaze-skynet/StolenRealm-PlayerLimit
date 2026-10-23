using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlayerLimitMod.UI
{
    /// <summary>
    /// Hero portrait column (left side of the screen). Clones extra portraits as needed and pages them.
    /// A compact [▲ 1/3 ▼] bar sits just above the first visible portrait (outside the column's layout,
    /// so nothing gets pushed off-screen). Mouse wheel over the column flips pages; selecting a hero
    /// jumps to its page.
    /// </summary>
    internal static class PortraitPager
    {
        private static PortraitManager _owner;
        private static NavBar _nav;
        private static int _page;
        private static Character _lastSelected;
        private static int _lastPartyCount = -1;

        public static void Update(PortraitManager mgr, ref Portrait[] portraits, List<Character> party)
        {
            if (_owner != mgr) Reset(mgr);

            var count = party.Count;
            if (count > portraits.Length) portraits = Grow(portraits, count);

            var per = PlayerLimitPlugin.PortraitsPerPage;
            var pages = Mathf.Max(1, Mathf.CeilToInt(count / (float)per));

            if (count != _lastPartyCount)
            {
                _lastPartyCount = count;
                _page = Mathf.Clamp(_page, 0, pages - 1);
            }

            // Follow the selected hero (the game highlights it in the column).
            if (PlayerLimitPlugin.PortraitFollowSelectionEntry.Value && GameLogic.instance)
            {
                var selected = GameLogic.instance.CurrentlySelectedCharacter;
                if (selected != null && !ReferenceEquals(selected, _lastSelected))
                {
                    var idx = party.IndexOf(selected);
                    if (idx >= 0) _page = idx / per;
                }
                _lastSelected = selected;
            }

            _page = Mathf.Clamp(_page, 0, pages - 1);
            var first = _page * per;
            Portrait firstShown = null;
            for (var i = 0; i < portraits.Length; i++)
            {
                var p = portraits[i];
                if (!p) continue;
                var hasHero = i < count;
                if (hasHero && p.Character != party[i]) p.Character = party[i]; // never assign null: the setter dereferences it
                var show = hasHero && i >= first && i < first + per;
                if (p.gameObject.activeSelf != show) p.gameObject.SetActive(show);
                if (show && firstShown == null) firstShown = p;
            }

            UpdateNav(mgr, pages, firstShown);
        }

        private static void Reset(PortraitManager mgr)
        {
            _owner = mgr;
            _page = 0;
            _lastSelected = null;
            _lastPartyCount = -1;
            _nav = null;
        }

        private static Portrait[] Grow(Portrait[] portraits, int needed)
        {
            var template = portraits[0];
            if (!template) return portraits;
            var parent = template.transform.parent;
            var grown = new Portrait[needed];
            Array.Copy(portraits, grown, portraits.Length);

            var insertAt = portraits[portraits.Length - 1] ? portraits[portraits.Length - 1].transform.GetSiblingIndex() + 1 : parent.childCount;
            for (var i = portraits.Length; i < needed; i++)
            {
                var go = UnityEngine.Object.Instantiate(template.gameObject, parent);
                go.name = template.gameObject.name + " (PLM " + (i + 1) + ")";
                go.SetActive(false);
                go.transform.SetSiblingIndex(insertAt++);
                grown[i] = go.GetComponent<Portrait>();
            }
            PlayerLimitPlugin.Debug($"Portrait column grown {portraits.Length} -> {needed}");
            return grown;
        }

        private static void Step(int delta)
        {
            _page = Mathf.Max(0, _page + delta); // clamped on the next Update
            _lastSelected = GameLogic.instance ? GameLogic.instance.CurrentlySelectedCharacter : null; // manual paging wins until a new hero is selected
        }

        private static void UpdateNav(PortraitManager mgr, int pages, Portrait firstShown)
        {
            if (pages > 1 && (_nav == null || !_nav.Alive))
            {
                _nav = new NavBar("PortraitNav", mgr.transform, mgr.transform, vertical: true, step: Step);
                var wheel = mgr.GetComponent<WheelPager>();
                if (!wheel) wheel = mgr.gameObject.AddComponent<WheelPager>();
                wheel.OnStep = Step;
                PlayerLimitPlugin.Debug("Portrait nav bar created");
            }
            if (_nav == null || !_nav.Alive) return;

            _nav.Show(_page, pages);
            if (pages <= 1 || !firstShown) return;

            // Sit just above the top-left corner of the first visible portrait.
            var rt = (RectTransform)firstShown.transform;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners); // 0 bottom-left, 1 top-left, 2 top-right, 3 bottom-right
            _nav.PlaceAtWorld(corners[1], new Vector2(0, 0), new Vector2(0, 6));
        }
    }
}
