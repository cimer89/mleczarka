using System.Collections.Generic;
using System.Linq;
using Mleczarka.Player;
using Mleczarka.Vehicles;
using Mleczarka.Vehicles.Model;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Mleczarka.UI
{
    /// <summary>
    /// Garaż: personalizacja pojazdu stojącego obok gracza (albo tego, którym jedzie).
    /// G otwiera i zamyka. Części zmienia się strzałkami, lakier — próbkami kolorów.
    /// Zmiany idą do VehicleLoadout, więc trafią do zapisu i (w przyszłości) do synchronizacji.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GarageUI : MonoBehaviour
    {
        public FarmerController player;
        public float reach = 8f;

        static readonly Color Panel = new Color(1f, 0.97f, 0.90f, 0.94f);
        static readonly Color Ink = new Color(0.22f, 0.17f, 0.12f);
        static readonly Color Brand = new Color(0.16f, 0.42f, 0.80f);
        static readonly Rgb[] Palette =
        {
            new Rgb(0.95f, 0.95f, 0.92f), new Rgb(0.10f, 0.10f, 0.12f), new Rgb(0.90f, 0.20f, 0.16f), new Rgb(0.98f, 0.55f, 0.05f),
            new Rgb(0.98f, 0.80f, 0.10f), new Rgb(0.25f, 0.65f, 0.25f), new Rgb(0.15f, 0.40f, 0.85f), new Rgb(0.45f, 0.25f, 0.70f),
            new Rgb(0.95f, 0.55f, 0.70f), new Rgb(0.55f, 0.35f, 0.20f), new Rgb(0.60f, 0.62f, 0.65f), new Rgb(0.30f, 0.80f, 0.85f),
        };
        static readonly PartSlot[] Slots = { PartSlot.Kabina, PartSlot.Zabudowa, PartSlot.Kola, PartSlot.ZderzakPrzedni, PartSlot.ZderzakTylny, PartSlot.Dach, PartSlot.Spoiler, PartSlot.Wydech };
        static readonly HashSet<PartSlot> Optional = new HashSet<PartSlot> { PartSlot.Dach, PartSlot.Spoiler };

        VisualElement _root, _panel;
        Label _hint;
        VehicleAssembler _target;
        float _hintTimer;

        void OnEnable()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            _root.Clear();
            _hint = new Label("G — garaż (przy pojeździe)   ·   E — wsiądź / wysiądź");
            Style(_hint, 18, new Color(1, 1, 1, 0.95f), true);
            _hint.style.position = Position.Absolute; _hint.style.left = 24; _hint.style.bottom = 20;
            _hint.style.backgroundColor = new Color(0.1f, 0.12f, 0.16f, 0.55f);
            Pad(_hint, 8, 14); Round(_hint, 10);
            _hint.style.display = DisplayStyle.None;   // stałe podpowiedzi pokazuje HudUI; tu tylko komunikaty garażu
            _root.Add(_hint);
        }

        void Update()
        {
            bool toggle = (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
                       || (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame);
            if (toggle) { if (_panel != null) Close(); else Open(); }
            if (_panel != null && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
            if (_hintTimer > 0 && (_hintTimer -= Time.unscaledDeltaTime) <= 0)
                _hint.style.display = DisplayStyle.None;
        }

        void Open()
        {
            _target = FindTarget();
            if (!_target)
            {
                _hint.text = "Podejdź bliżej do cysterny albo dostawczaka, żeby otworzyć garaż.";
                _hint.style.display = DisplayStyle.Flex;
                _hintTimer = 3f;
                return;
            }
            if (player)
            {
                player.orbitWhenBlocked = true;
                player.InputBlocked = true;
                var wv = _target.GetComponent<WheeledVehicle>();
                player.FocusCamera(wv && wv.cameraTarget ? wv.cameraTarget : _target.transform);
            }
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            Rebuild();
        }

        void Close()
        {
            _panel?.RemoveFromHierarchy();
            _panel = null;
            if (player) { player.InputBlocked = false; player.RestoreCamera(); }
        }

        VehicleAssembler FindTarget()
        {
            if (player && player.CurrentVehicle) return player.CurrentVehicle.GetComponent<VehicleAssembler>();
            Vector3 p = player ? player.transform.position : Vector3.zero;
            return FindObjectsByType<VehicleAssembler>(FindObjectsSortMode.None)
                .Where(a => Vector3.Distance(a.transform.position, p) < reach)
                .OrderBy(a => Vector3.Distance(a.transform.position, p))
                .FirstOrDefault();
        }

        // ---------- budowa panelu ----------

        void Rebuild()
        {
            _panel?.RemoveFromHierarchy();
            var a = _target;
            _panel = new VisualElement();
            _panel.style.position = Position.Absolute; _panel.style.right = 28; _panel.style.top = 28; _panel.style.bottom = 28;
            _panel.style.width = 520;
            _panel.style.backgroundColor = Panel;
            Round(_panel, 22); Pad(_panel, 20, 24);
            _panel.style.borderBottomWidth = 6; _panel.style.borderBottomColor = Brand;
            _root.Add(_panel);

            var title = new Label("Garaż mleczarni"); Style(title, 30, Brand, true); _panel.Add(title);
            var sub = new Label(a.definition ? a.definition.displayName : a.name); Style(sub, 20, Ink, false); _panel.Add(sub);

            // parametry
            var s = a.Stats;
            var stats = Row(); stats.style.flexWrap = Wrap.Wrap; stats.style.marginTop = 10; stats.style.marginBottom = 8;
            stats.Add(Chip($"{s.maxSpeedKmh:0} km/h"));
            stats.Add(Chip($"{s.motorTorque:0} Nm"));
            stats.Add(Chip($"{s.massKg:0} kg"));
            stats.Add(Chip($"przyczepność ×{s.grip:0.00}"));
            if (s.capacityL > 0) stats.Add(Chip($"{s.capacityL:0} l mleka"));
            int cost = a.loadout.parts.Select(c => a.FindPart(c.partId)).Where(p => p).Sum(p => p.price);
            stats.Add(Chip($"wartość części: {cost} zł"));
            _panel.Add(stats);

            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.style.flexGrow = 1;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden; _panel.Add(scroll);
            foreach (var slot in Slots)
            {
                var options = a.catalog.Where(p => p && p.slot == slot && p.fitsTypes.Contains(a.loadout.type)).OrderBy(p => p.tier).ThenBy(p => p.price).ToList();
                if (options.Count == 0) continue;
                scroll.Add(SlotRow(slot, options));
            }

            scroll.Add(Header("Lakier"));
            scroll.Add(PaintRow("Główny", () => a.loadout.paintPrimary, c => a.loadout.paintPrimary = c));
            scroll.Add(PaintRow("Dodatkowy", () => a.loadout.paintSecondary, c => a.loadout.paintSecondary = c));
            scroll.Add(PaintRow("Akcent", () => a.loadout.paintAccent, c => a.loadout.paintAccent = c));

            var close = new Button(Close) { text = "Gotowe (G)" };
            StyleButton(close, Brand, Color.white, 22); close.style.marginTop = 12; close.style.height = 48;
            _panel.Add(close);
        }

        VisualElement SlotRow(PartSlot slot, List<VehiclePartDefinition> options)
        {
            var a = _target;
            var ids = options.Select(o => o.id).ToList();
            if (Optional.Contains(slot)) ids.Insert(0, null);
            string cur = a.loadout.Get(slot);
            int idx = Mathf.Max(0, ids.IndexOf(cur));
            var part = cur != null ? a.FindPart(cur) : null;

            var box = new VisualElement(); box.style.marginTop = 8;
            var name = new Label(SlotName(slot)); Style(name, 15, new Color(Ink.r, Ink.g, Ink.b, 0.7f), true); box.Add(name);
            var row = Row(); row.style.alignItems = Align.Center;
            var prev = new Button(() => Cycle(slot, ids, idx - 1)) { text = "◀" }; StyleButton(prev, new Color(0.88f, 0.84f, 0.76f), Ink, 20); prev.style.width = 46;
            var next = new Button(() => Cycle(slot, ids, idx + 1)) { text = "▶" }; StyleButton(next, new Color(0.88f, 0.84f, 0.76f), Ink, 20); next.style.width = 46;
            var label = new Label(part ? part.displayName : "— brak —"); Style(label, 17, Ink, true);
            label.style.flexGrow = 1; label.style.flexShrink = 1; label.style.whiteSpace = WhiteSpace.Normal;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            row.Add(prev); row.Add(label); row.Add(next);
            if (part) row.Add(TierBadge(part.tier));
            box.Add(row);
            if (part && part.price > 0) { var price = new Label($"{part.price} zł"); Style(price, 14, new Color(Ink.r, Ink.g, Ink.b, 0.6f), false); price.style.unityTextAlign = TextAnchor.MiddleCenter; box.Add(price); }
            return box;
        }

        void Cycle(PartSlot slot, List<string> ids, int i)
        {
            i = (i % ids.Count + ids.Count) % ids.Count;
            _target.loadout.Set(slot, ids[i]);
            _target.Rebuild();
            Rebuild();
        }

        VisualElement PaintRow(string label, System.Func<Rgb> get, System.Action<Rgb> set)
        {
            var box = new VisualElement(); box.style.marginTop = 6;
            var l = new Label(label); Style(l, 15, new Color(Ink.r, Ink.g, Ink.b, 0.7f), true); box.Add(l);
            var row = Row(); row.style.flexWrap = Wrap.Wrap;
            var cur = get();
            foreach (var c in Palette)
            {
                var col = new Color(c.r, c.g, c.b);
                var b = new Button(() => { set(c); _target.ApplyPaint(); Rebuild(); }) { text = "" };
                b.style.width = 34; b.style.height = 34; b.style.marginRight = 6; b.style.marginBottom = 6;
                b.style.backgroundColor = col; Round(b, 17);
                bool on = Mathf.Abs(cur.r - c.r) + Mathf.Abs(cur.g - c.g) + Mathf.Abs(cur.b - c.b) < 0.02f;
                float w = on ? 4 : 1; var bc = on ? Ink : new Color(0, 0, 0, 0.25f);
                b.style.borderTopWidth = w; b.style.borderBottomWidth = w; b.style.borderLeftWidth = w; b.style.borderRightWidth = w;
                b.style.borderTopColor = bc; b.style.borderBottomColor = bc; b.style.borderLeftColor = bc; b.style.borderRightColor = bc;
                row.Add(b);
            }
            box.Add(row);
            return box;
        }

        // ---------- drobne elementy i style ----------

        static string SlotName(PartSlot s) => s switch
        {
            PartSlot.Kabina => "KABINA", PartSlot.Zabudowa => "ZABUDOWA", PartSlot.Kola => "KOŁA",
            PartSlot.ZderzakPrzedni => "ZDERZAK PRZEDNI", PartSlot.ZderzakTylny => "ZDERZAK TYLNY",
            PartSlot.Dach => "DACH", PartSlot.Spoiler => "SPOILER", _ => "WYDECH",
        };

        static VisualElement TierBadge(PartTier t)
        {
            var (txt, col) = t switch
            {
                PartTier.Tuning => ("TUNING", new Color(0.25f, 0.62f, 0.30f)),
                PartTier.Wyscigowa => ("WYŚCIG", new Color(0.92f, 0.45f, 0.08f)),
                _ => ("ZWYKŁA", new Color(0.55f, 0.55f, 0.55f)),
            };
            var l = new Label(txt); Style(l, 13, Color.white, true);
            l.style.backgroundColor = col; Round(l, 8); Pad(l, 3, 8); l.style.marginLeft = 8;
            return l;
        }

        static Label Header(string t)
        {
            var l = new Label(t); Style(l, 22, Brand, true); l.style.marginTop = 16; return l;
        }

        static VisualElement Chip(string t)
        {
            var l = new Label(t); Style(l, 15, Ink, true);
            l.style.backgroundColor = new Color(0.90f, 0.86f, 0.78f); Round(l, 10); Pad(l, 4, 10);
            l.style.marginRight = 6; l.style.marginBottom = 6;
            return l;
        }

        static VisualElement Row() { var r = new VisualElement(); r.style.flexDirection = FlexDirection.Row; return r; }

        static void Style(Label l, int size, Color c, bool bold)
        {
            l.style.fontSize = size; l.style.color = c;
            l.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
        }

        static void StyleButton(Button b, Color bg, Color fg, int size)
        {
            b.style.backgroundColor = bg; b.style.color = fg; b.style.fontSize = size;
            b.style.unityFontStyleAndWeight = FontStyle.Bold; Round(b, 12);
            b.style.borderTopWidth = 0; b.style.borderBottomWidth = 0; b.style.borderLeftWidth = 0; b.style.borderRightWidth = 0;
            b.style.height = 42;
        }

        static void Round(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r; e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
        }

        static void Pad(VisualElement e, float v, float h)
        {
            e.style.paddingTop = v; e.style.paddingBottom = v; e.style.paddingLeft = h; e.style.paddingRight = h;
        }
    }
}
