using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace PlayerLimitMod.Patches
{
    /// <summary>
    /// Right-clicking an inventory item builds a menu (Equip, Link In Chat, "Send to [hero]" for every other hero,
    /// Send to Stash, Stack, Split, Destroy) into a fixed array of option slots. With a big party it runs out of
    /// slots and stops partway, so not every hero is listed. Add slots as needed and keep the menu on screen.
    /// </summary>
    [HarmonyPatch(typeof(ItemRightClick), nameof(ItemRightClick.ShowRightClickTooltip), typeof(Character), typeof(ItemSlot))]
    internal static class Inventory_RightClickMenu
    {
        // Non-hero entries the game can add: Equip, Link In Chat, Send to Stash, Stack, Split, Destroy.
        private const int FixedEntries = 6;

        private static void Prefix(ItemRightClick __instance)
        {
            var options = __instance.Options;
            if (options == null || options.Length == 0) return;
            var heroes = NetworkingManager.Instance?.PartyCharacters?.Count ?? 0;
            var needed = heroes + FixedEntries;
            if (options.Length >= needed) return;

            var template = options[0]; // copy an option the game is known to use
            var last = options[options.Length - 1];
            var parent = last.transform.parent;
            var grown = new ItemRightClickOption[needed];
            System.Array.Copy(options, grown, options.Length);

            // If the menu has no layout group, space new rows the same way the game's own rows are spaced.
            var laidOut = parent && parent.GetComponent<LayoutGroup>();
            var lastPos = ((RectTransform)last.transform).anchoredPosition;
            var stepPos = options.Length > 1
                ? lastPos - ((RectTransform)options[options.Length - 2].transform).anchoredPosition
                : new Vector2(0, -((RectTransform)last.transform).rect.height);

            var insertAt = last.transform.GetSiblingIndex() + 1;
            for (var i = options.Length; i < needed; i++)
            {
                var go = Object.Instantiate(template.gameObject, parent);
                go.name = template.gameObject.name + " (PLM " + (i + 1) + ")";
                go.SetActive(false);
                go.transform.SetSiblingIndex(insertAt++);
                if (!laidOut) ((RectTransform)go.transform).anchoredPosition = lastPos + stepPos * (i - options.Length + 1);
                grown[i] = go.GetComponent<ItemRightClickOption>();
            }
            __instance.Options = grown;
            PlayerLimitPlugin.Debug($"Item right-click menu grown {options.Length} -> {needed} options");
        }

        private static void Postfix(ItemRightClick __instance) => OnScreen.Clamp((RectTransform)__instance.transform);
    }

    internal static class OnScreen
    {
        /// <summary>Nudge a freshly opened popup so it isn't cut off by the bottom/top/right of the screen.</summary>
        public static void Clamp(RectTransform rt)
        {
            if (!rt || !rt.gameObject.activeInHierarchy) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            var canvas = rt.GetComponentInParent<Canvas>();
            var cam = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

            var world = new Vector3[4];
            rt.GetWorldCorners(world); // 0 bottom-left, 1 top-left, 2 top-right, 3 bottom-right
            var bl = RectTransformUtility.WorldToScreenPoint(cam, world[0]);
            var tr = RectTransformUtility.WorldToScreenPoint(cam, world[2]);
            var sw = tr.x - bl.x;
            var sh = tr.y - bl.y;
            if (sw <= 0 || sh <= 0) return;

            const float margin = 8f;
            var dx = 0f;
            var dy = 0f;
            if (bl.y < margin) dy = margin - bl.y;                                  // off the bottom: move up
            else if (tr.y > Screen.height - margin) dy = Screen.height - margin - tr.y; // off the top: move down
            if (tr.x > Screen.width - margin) dx = Screen.width - margin - tr.x;      // off the right: move left
            if (dx == 0f && dy == 0f) return;

            // Convert the screen-pixel shift into world units for this canvas.
            var worldPerPixelX = (world[2].x - world[0].x) / sw;
            var worldPerPixelY = (world[2].y - world[0].y) / sh;
            rt.position += new Vector3(dx * worldPerPixelX, dy * worldPerPixelY, 0);
        }
    }
}
