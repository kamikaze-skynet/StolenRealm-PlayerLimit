using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlayerLimitMod.UI
{
    /// <summary>Small helpers to build UI that matches the game's look.</summary>
    internal static class UiKit
    {
        public static readonly Color BoxColor = new Color(0.08f, 0.07f, 0.06f, 0.85f);
        public static readonly Color TextColor = new Color(0.92f, 0.86f, 0.70f);
        public static readonly Color FullColor = new Color(0.95f, 0.45f, 0.35f);

        private static Sprite _left, _right;
        private static TMP_FontAsset _font;

        public static Sprite LeftArrow => _left ? _left : (_left = LoadSprite("arrow_left.png"));
        public static Sprite RightArrow => _right ? _right : (_right = LoadSprite("arrow_right.png"));

        /// <summary>Borrow a TextMeshPro font from the game's own UI under <paramref name="root"/>.</summary>
        public static TMP_FontAsset Font(Transform root)
        {
            if (_font) return _font;
            if (!root) return null;
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.font)
                {
                    _font = t.font;
                    break;
                }
            }
            return _font;
        }

        private static Sprite LoadSprite(string resource)
        {
            try
            {
                using (var s = typeof(UiKit).Assembly.GetManifestResourceStream(resource))
                {
                    if (s == null) return null;
                    var bytes = new byte[s.Length];
                    var read = 0;
                    while (read < bytes.Length) read += s.Read(bytes, read, bytes.Length - read);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                    tex.LoadImage(bytes);
                    UnityEngine.Object.DontDestroyOnLoad(tex);
                    var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    UnityEngine.Object.DontDestroyOnLoad(sprite);
                    return sprite;
                }
            }
            catch (Exception e)
            {
                PlayerLimitPlugin.Log.LogWarning($"Could not load {resource}: {e.Message}");
                return null;
            }
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject("PLM_" + name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 pos)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        /// <summary>Dark rounded-ish label box like the game's header plates.</summary>
        public static TextMeshProUGUI LabelBox(string name, Transform parent, Transform fontSource, out RectTransform box)
        {
            box = Rect(name, parent);
            var bg = box.gameObject.AddComponent<Image>();
            bg.color = BoxColor;
            bg.raycastTarget = false;

            var textRt = Rect("Text", box);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(6, 1);
            textRt.offsetMax = new Vector2(-6, -1);

            var text = textRt.gameObject.AddComponent<TextMeshProUGUI>();
            var font = Font(fontSource);
            if (font) text.font = font;
            text.fontSize = 13;
            text.color = TextColor;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Arrow button using the golden arrow sprite; rotationZ turns the right arrow into up/down.</summary>
        public static Button ArrowButton(string name, Transform parent, Sprite sprite, float rotationZ, Vector2 size, UnityAction onClick)
        {
            var rt = Rect(name, parent);
            rt.sizeDelta = size;

            var img = rt.gameObject.AddComponent<Image>();
            if (sprite)
            {
                img.sprite = sprite;
                img.preserveAspect = true;
            }
            else
            {
                img.color = BoxColor;
            }

            // Rotate only the graphic so layout groups still see an unrotated box.
            if (Math.Abs(rotationZ) > 0.01f)
            {
                var gfx = Rect("Gfx", rt);
                gfx.anchorMin = gfx.anchorMax = new Vector2(0.5f, 0.5f);
                gfx.sizeDelta = new Vector2(size.y, size.x);
                gfx.localRotation = Quaternion.Euler(0, 0, rotationZ);
                var gimg = gfx.gameObject.AddComponent<Image>();
                gimg.sprite = img.sprite;
                gimg.preserveAspect = true;
                gimg.raycastTarget = false;
                img.sprite = null;
                img.color = new Color(0, 0, 0, 0.001f); // invisible but still clickable
                img = gimg;
            }

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.9f, 0.7f);
            colors.pressedColor = new Color(0.7f, 0.6f, 0.4f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(onClick);
            return btn;
        }
    }

    /// <summary>
    /// Compact page navigator on a dark gold-edged plate: [prev] label [next].
    /// Vertical = up/down arrows (portrait column), otherwise left/right.
    /// The bar ignores parent layout groups; callers position it.
    /// </summary>
    internal class NavBar
    {
        public RectTransform Root;
        private readonly Button _prev, _next;
        private readonly TextMeshProUGUI _label;
        private readonly bool _vertical;

        public NavBar(string name, Transform parent, Transform fontSource, bool vertical, Action<int> step)
        {
            _vertical = vertical;
            var arrow = vertical ? new Vector2(24, 28) : new Vector2(52, 24);
            var labelW = vertical ? 44f : 84f;
            var h = vertical ? 32f : 30f;
            var w = 6 + arrow.x + 4 + labelW + 4 + arrow.x + 6;

            Root = UiKit.Rect(name, parent);
            Root.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Root.anchorMin = Root.anchorMax = new Vector2(0.5f, 0.5f);
            Root.sizeDelta = new Vector2(w, h);

            var plate = Root.gameObject.AddComponent<Image>();
            plate.color = new Color(0.06f, 0.05f, 0.04f, 0.92f);
            plate.raycastTarget = true; // swallow clicks so they don't fall through to the world
            var edge = Root.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0.87f, 0.67f, 0.27f, 0.9f);
            edge.effectDistance = new Vector2(1.5f, -1.5f);

            _prev = UiKit.ArrowButton(name + "Prev", Root, vertical ? UiKit.RightArrow : UiKit.LeftArrow, vertical ? 90f : 0f, arrow, () => step(-1));
            UiKit.Place((RectTransform)_prev.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), arrow, new Vector2(6, 0));

            _next = UiKit.ArrowButton(name + "Next", Root, UiKit.RightArrow, vertical ? -90f : 0f, arrow, () => step(1));
            UiKit.Place((RectTransform)_next.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), arrow, new Vector2(-6, 0));

            var labelRt = UiKit.Rect(name + "Label", Root);
            UiKit.Place(labelRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(labelW, h), Vector2.zero);
            _label = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
            var font = UiKit.Font(fontSource);
            if (font) _label.font = font;
            _label.fontSize = 15;
            _label.color = UiKit.TextColor;
            _label.alignment = TextAlignmentOptions.Center;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.overflowMode = TextOverflowModes.Overflow;
            _label.raycastTarget = false;

            // Mouse wheel over the bar itself also pages.
            Root.gameObject.AddComponent<WheelPager>().OnStep = step;
        }

        public bool Alive => Root;

        public void Show(int page, int pages)
        {
            if (!Root) return;
            var paged = pages > 1;
            if (Root.gameObject.activeSelf != paged) Root.gameObject.SetActive(paged);
            if (!paged) return;
            _label.text = _vertical ? $"{page + 1}/{pages}" : $"Page {page + 1}/{pages}";
            _prev.interactable = page > 0;
            _next.interactable = page < pages - 1;
            Root.SetAsLastSibling();
        }

        /// <summary>
        /// Place the bar so its pivot sits at a point given in world space, then keep it fully on screen:
        /// shrink it (down to <paramref name="minScale"/>) if it doesn't fit, then nudge it inside the edges.
        /// </summary>
        public void PlaceAtWorld(Vector3 world, Vector2 pivot, Vector2 offset, float minScale = 0.55f)
        {
            if (!Root || !Root.parent) return;
            Root.pivot = pivot;
            Root.localScale = Vector3.one;
            var local = Root.parent.InverseTransformPoint(world);
            Root.localPosition = new Vector3(local.x + offset.x, local.y + offset.y, 0);
            FitOnScreen(minScale);
        }

        private void FitOnScreen(float minScale)
        {
            const float margin = 4f;
            var canvas = Root.GetComponentInParent<Canvas>();
            var cam = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

            Rect ScreenRect()
            {
                var c = new Vector3[4];
                Root.GetWorldCorners(c);
                var a = RectTransformUtility.WorldToScreenPoint(cam, c[0]);
                var b = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
                return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            }

            var r = ScreenRect();
            if (r.height <= 0 || r.width <= 0) return;

            // Too tall for the space between its anchor point and the screen edge it runs into: shrink toward the pivot.
            float room;
            if (Root.pivot.y <= 0.01f) room = Screen.height - margin - r.yMin;      // grows upward
            else if (Root.pivot.y >= 0.99f) room = r.yMax - margin;                 // grows downward
            else room = Screen.height - 2 * margin;
            if (r.height > room && room > 0)
            {
                var s = Mathf.Clamp(room / r.height, minScale, 1f);
                Root.localScale = new Vector3(s, s, 1f);
                r = ScreenRect();
            }

            // Still poking out anywhere: slide it back inside.
            var dx = 0f;
            var dy = 0f;
            if (r.xMin < margin) dx = margin - r.xMin;
            else if (r.xMax > Screen.width - margin) dx = Screen.width - margin - r.xMax;
            if (r.yMax > Screen.height - margin) dy = Screen.height - margin - r.yMax;
            else if (r.yMin < margin) dy = margin - r.yMin;
            if (dx == 0f && dy == 0f) return;

            var wc = new Vector3[4];
            Root.GetWorldCorners(wc);
            var perPxX = (wc[2].x - wc[0].x) / r.width;
            var perPxY = (wc[2].y - wc[0].y) / r.height;
            Root.position += new Vector3(dx * perPxX, dy * perPxY, 0);
        }
    }

    /// <summary>Turns mouse-wheel movement over a panel into page flips.</summary>
    internal class WheelPager : MonoBehaviour, IScrollHandler
    {
        public Action<int> OnStep;

        public void OnScroll(PointerEventData eventData)
        {
            var dy = eventData.scrollDelta.y;
            if (Mathf.Abs(dy) < 0.01f) return;
            OnStep?.Invoke(dy > 0 ? -1 : 1); // wheel up = previous page
        }
    }
}
