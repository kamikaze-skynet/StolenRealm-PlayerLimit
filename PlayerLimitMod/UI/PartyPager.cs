using System.Collections.Generic;
using PlayerLimitMod.Patches;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayerLimitMod.UI
{
    /// <summary>
    /// Pages the "Current Party" slots on the character select screen and shows a party counter.
    /// Layout follows the original mod's in-game positions: counter top-left, page label top-right,
    /// arrows flanking the panel title.
    /// </summary>
    internal static class PartyPager
    {
        private static CharacterChoiceManager _mgr;
        private static Transform _panel;
        private static TextMeshProUGUI _counter, _pageLabel;
        private static RectTransform _counterBox, _pageBox;
        private static Button _prev, _next;
        private static int _page;
        private static int _lastCount = -1;

        public static void Apply(CharacterChoiceManager mgr)
        {
            if (!mgr || !mgr.partyHolder) return;
            var slots = PartySelect.PartySlots(mgr);
            if (slots == null) return;

            _mgr = mgr;
            EnsureUi(mgr.partyHolder);

            var count = Mathf.Min(PartySelect.PartyChars(mgr)?.Count ?? 0, slots.Count);
            var per = PlayerLimitPlugin.PartySlotsPerPage;
            var pages = Mathf.Max(1, Mathf.CeilToInt(count / (float)per));

            // A hero was just added: jump to the page it landed on.
            if (_lastCount >= 0 && count > _lastCount) _page = pages - 1;
            _lastCount = count;
            _page = Mathf.Clamp(_page, 0, pages - 1);

            var first = _page * per;
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (!slot) continue;
                var show = i < count && i >= first && i < first + per;
                if (slot.gameObject.activeSelf != show) slot.gameObject.SetActive(show);
            }

            UpdateUi(count, pages);
        }

        private static void Step(int delta)
        {
            if (!_mgr) return;
            var count = Mathf.Min(PartySelect.PartyChars(_mgr)?.Count ?? 0, PartySelect.PartySlots(_mgr)?.Count ?? 0);
            var pages = Mathf.Max(1, Mathf.CeilToInt(count / (float)PlayerLimitPlugin.PartySlotsPerPage));
            var target = Mathf.Clamp(_page + delta, 0, pages - 1);
            if (target == _page) return;
            _page = target;
            Apply(_mgr);
        }

        private static void EnsureUi(Transform partyHolder)
        {
            var panel = partyHolder.parent;
            if (!panel || (_panel == panel && _counter && _prev)) return;
            _panel = panel;

            _counter = UiKit.LabelBox("PartyCounter", panel, panel, out _counterBox);
            _counter.alignment = TextAlignmentOptions.Left;
            UiKit.Place(_counterBox, new Vector2(0, 1), new Vector2(0, 1), new Vector2(104, 22), new Vector2(5, -2));

            _pageLabel = UiKit.LabelBox("PartyPage", panel, panel, out _pageBox);
            _pageLabel.alignment = TextAlignmentOptions.Right;
            UiKit.Place(_pageBox, new Vector2(1, 1), new Vector2(1, 1), new Vector2(90, 22), new Vector2(-5, -2));

            _prev = UiKit.ArrowButton("PartyPrev", panel, UiKit.LeftArrow, 0, new Vector2(56, 22), () => Step(-1));
            UiKit.Place((RectTransform)_prev.transform, new Vector2(0.35f, 1), new Vector2(1, 1), new Vector2(56, 22), new Vector2(-5, -2));

            _next = UiKit.ArrowButton("PartyNext", panel, UiKit.RightArrow, 0, new Vector2(56, 22), () => Step(1));
            UiKit.Place((RectTransform)_next.transform, new Vector2(0.65f, 1), new Vector2(0, 1), new Vector2(56, 22), new Vector2(5, -2));

            var wheel = partyHolder.GetComponent<WheelPager>();
            if (!wheel) wheel = partyHolder.gameObject.AddComponent<WheelPager>();
            wheel.OnStep = Step;

            PlayerLimitPlugin.Debug("Party pager UI created");
        }

        private static void UpdateUi(int count, int pages)
        {
            var max = PlayerLimitPlugin.MaxPlayers;
            if (_counter)
            {
                _counter.text = $"Party: {count}/{max}";
                _counter.color = count >= max ? UiKit.FullColor : UiKit.TextColor;
            }

            var paged = pages > 1;
            if (_pageBox) _pageBox.gameObject.SetActive(paged);
            if (_pageLabel) _pageLabel.text = $"Page {_page + 1}/{pages}";
            if (_prev)
            {
                _prev.gameObject.SetActive(paged);
                _prev.interactable = _page > 0;
            }
            if (_next)
            {
                _next.gameObject.SetActive(paged);
                _next.interactable = _page < pages - 1;
            }
        }
    }
}
