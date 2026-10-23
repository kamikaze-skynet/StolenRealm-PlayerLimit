using HarmonyLib;
using UnityEngine;

namespace PlayerLimitMod.UI
{
    /// <summary>
    /// Pages the character tiles in the event window. A centred [◀ Page 1/3 ▶] bar sits just below the
    /// tile row; the mouse wheel over the row (or the bar) also flips pages.
    /// </summary>
    internal static class EventPager
    {
        private static readonly AccessTools.FieldRef<EventWindow, EventCharacterPortrait[]> PortraitCache =
            AccessTools.FieldRefAccess<EventWindow, EventCharacterPortrait[]>("_EventCharacterPortraits");

        private static EventWindow _window;
        private static NavBar _nav;
        private static int _count, _page;

        public static void EnsureTiles(EventWindow window, int needed)
        {
            if (!window || !window.CharacterImageTileHolder) return;
            var holder = window.CharacterImageTileHolder.transform;

            var tiles = 0;
            Transform last = null;
            for (var i = 0; i < holder.childCount; i++)
            {
                var c = holder.GetChild(i);
                if (!c.GetComponent<EventCharacterPortrait>()) continue;
                tiles++;
                last = c;
            }
            if (last == null || needed <= tiles) return;

            var insertAt = last.GetSiblingIndex() + 1;
            for (var i = tiles; i < needed; i++)
            {
                var go = Object.Instantiate(last.gameObject, holder);
                go.name = last.gameObject.name + " (PLM " + (i + 1) + ")";
                go.SetActive(false);
                go.transform.SetSiblingIndex(insertAt++);
            }

            PortraitCache(window) = null; // the game caches its tile list; let it rebuild with the new tiles
            PlayerLimitPlugin.Debug($"Event character row grown {tiles} -> {needed}");
        }

        public static void NewRow(int count)
        {
            _count = count;
            _page = 0;
        }

        public static void Apply(EventWindow window)
        {
            if (!window || !window.CharacterImageTileHolder) return;
            _window = window;
            var holder = window.CharacterImageTileHolder.transform;

            var per = PlayerLimitPlugin.EventPortraitsPerPage;
            var pages = Mathf.Max(1, Mathf.CeilToInt(_count / (float)per));
            _page = Mathf.Clamp(_page, 0, pages - 1);
            var first = _page * per;

            var index = 0;
            for (var i = 0; i < holder.childCount; i++)
            {
                var c = holder.GetChild(i);
                if (!c.GetComponent<EventCharacterPortrait>()) continue;
                var show = index < _count && index >= first && index < first + per;
                if (c.gameObject.activeSelf != show) c.gameObject.SetActive(show);
                index++;
            }

            UpdateNav(holder, pages);
        }

        private static void Step(int delta)
        {
            if (!_window) return;
            var pages = Mathf.Max(1, Mathf.CeilToInt(_count / (float)PlayerLimitPlugin.EventPortraitsPerPage));
            var target = Mathf.Clamp(_page + delta, 0, pages - 1);
            if (target == _page) return;
            _page = target;
            Apply(_window);
        }

        private static void UpdateNav(Transform holder, int pages)
        {
            var parent = holder.parent ? holder.parent : holder;
            if (pages > 1 && (_nav == null || !_nav.Alive || _nav.Root.parent != parent))
            {
                _nav = new NavBar("EventNav", parent, holder, vertical: false, step: Step);
                var wheel = holder.GetComponent<WheelPager>();
                if (!wheel) wheel = holder.gameObject.AddComponent<WheelPager>();
                wheel.OnStep = Step;
                PlayerLimitPlugin.Debug("Event nav bar created");
            }
            if (_nav == null || !_nav.Alive) return;

            _nav.Show(_page, pages);
            if (pages <= 1 || !(holder is RectTransform h)) return;

            // Centre the bar just under the tile row.
            var corners = new Vector3[4];
            h.GetWorldCorners(corners);
            var bottomCentre = (corners[0] + corners[3]) * 0.5f;
            _nav.PlaceAtWorld(bottomCentre, new Vector2(0.5f, 1f), new Vector2(0, -6));
        }
    }
}
