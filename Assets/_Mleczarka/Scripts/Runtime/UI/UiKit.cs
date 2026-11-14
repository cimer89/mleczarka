using UnityEngine;
using UnityEngine.UIElements;

namespace Mleczarka.UI
{
    /// <summary>Wspólny wygląd interfejsu (kremowe karty, granatowo-niebieskie akcenty, zaokrąglenia).</summary>
    public static class UiKit
    {
        public static readonly Color Panel = new Color(1f, 0.97f, 0.90f, 0.95f);
        public static readonly Color Ink = new Color(0.22f, 0.17f, 0.12f);
        public static readonly Color Muted = new Color(0.22f, 0.17f, 0.12f, 0.65f);
        public static readonly Color Brand = new Color(0.16f, 0.42f, 0.80f);
        public static readonly Color Good = new Color(0.25f, 0.62f, 0.30f);
        public static readonly Color Bad = new Color(0.85f, 0.25f, 0.20f);
        public static readonly Color Chip = new Color(0.90f, 0.86f, 0.78f);
        public static readonly Color Dark = new Color(0.10f, 0.12f, 0.16f, 0.62f);

        public static Label L(string t, int size, Color c, bool bold = false)
        {
            var l = new Label(t); l.style.fontSize = size; l.style.color = c;
            l.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal; return l;
        }
        public static VisualElement Row(float gap = 0)
        {
            var r = new VisualElement(); r.style.flexDirection = FlexDirection.Row; r.style.alignItems = Align.Center; return r;
        }
        public static VisualElement Card(Color bg, float radius = 18, float padV = 12, float padH = 16)
        {
            var c = new VisualElement(); c.style.backgroundColor = bg; Round(c, radius); Pad(c, padV, padH); return c;
        }
        public static Button Btn(string text, System.Action onClick, Color bg, Color fg, int size = 16, float h = 34)
        {
            var b = new Button(onClick) { text = text };
            b.style.backgroundColor = bg; b.style.color = fg; b.style.fontSize = size; b.style.unityFontStyleAndWeight = FontStyle.Bold;
            b.style.height = h; Round(b, 10); NoBorder(b); b.style.marginLeft = 3; b.style.marginRight = 3;
            return b;
        }
        public static VisualElement Pill(string t, Color bg, Color fg, int size = 14)
        {
            var l = L(t, size, fg, true); l.style.backgroundColor = bg; Round(l, 9); Pad(l, 3, 9); l.style.marginRight = 6; l.style.marginBottom = 4; return l;
        }
        public static VisualElement Bar(float fill, Color col, float width = 160, float height = 10)
        {
            var bg = new VisualElement(); bg.style.width = width; bg.style.height = height; bg.style.backgroundColor = new Color(0, 0, 0, 0.12f); Round(bg, height / 2);
            var fg = new VisualElement(); fg.style.width = Length.Percent(Mathf.Clamp01(fill) * 100f); fg.style.height = height; fg.style.backgroundColor = col; Round(fg, height / 2);
            bg.Add(fg); return bg;
        }
        public static void Round(VisualElement e, float r)
        { e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r; e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r; }
        public static void Pad(VisualElement e, float v, float h)
        { e.style.paddingTop = v; e.style.paddingBottom = v; e.style.paddingLeft = h; e.style.paddingRight = h; }
        public static void NoBorder(VisualElement e)
        { e.style.borderTopWidth = 0; e.style.borderBottomWidth = 0; e.style.borderLeftWidth = 0; e.style.borderRightWidth = 0; }
    }
}
